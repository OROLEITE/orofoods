using Microsoft.EntityFrameworkCore;
using System.Data;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Delivery;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Payments;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public sealed class PointPaymentOrchestrationServiceTests
{
    [Fact]
    public void Server_side_point_orchestration_service_exists()
    {
        var serviceType = typeof(IPointPaymentProvider).Assembly.GetType("Orofoods.Web.Services.Payments.PointPaymentOrchestrationService");

        Assert.NotNull(serviceType);
    }

    [Fact]
    public async Task StartChargeAsync_persists_external_ids_and_assignment_snapshot_without_approving_created_order()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seed = await AddPendingOrderAsync(db);
        var provider = new FakePointProvider();
        var sut = CreateService(db, provider);

        var result = await sut.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, RequestKey);

        Assert.True(result.Succeeded);
        var payment = Assert.IsType<Payment>(result.Payment);
        Assert.Equal(seed.Assignment.Id, payment.DriverPaymentTerminalAssignmentId);
        Assert.Equal("MercadoPagoPoint", payment.Gateway);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Null(payment.PaidAt);
        Assert.Equal("MP-POINT-ORDER-1", payment.GatewayOrderId);
        Assert.Equal("MP-POINT-PAYMENT-1", payment.GatewayPaymentId);
        Assert.Equal($"oro-order-{seed.Order.Id}-attempt-{payment.Id}", payment.ExternalReference);
        Assert.Equal(seed.Order.Total, payment.Amount);
        Assert.Equal(1, provider.CreateCalls);
        Assert.Equal(payment.IdempotencyKey, provider.Requests.Single().IdempotencyKey);
    }

    [Fact]
    public async Task StartChargeAsync_persists_sanitized_admin_audit_with_assignment_and_external_order()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seed = await AddPendingOrderAsync(db);
        db.Users.Add(new ApplicationUser { Id = "point-admin", UserName = "point-admin@test.invalid" });
        await db.SaveChangesAsync();
        var sut = CreateService(db, new FakePointProvider());

        var result = await sut.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, RequestKey, CancellationToken.None, "point-admin");

        Assert.True(result.Succeeded);
        var audit = await db.PointPaymentAuditEvents.Include(x => x.AdminUser)
            .SingleAsync(x => x.ResultCode == "POINT_ORDER_IDENTIFIERS_RECEIVED");
        Assert.Equal(seed.Order.Id, audit.OrderId);
        Assert.Equal(result.Payment!.Id, audit.PaymentId);
        Assert.Equal(seed.Assignment.DriverId, audit.DriverId);
        Assert.Equal(seed.Assignment.PaymentTerminalId, audit.PaymentTerminalId);
        Assert.Equal(seed.Assignment.Id, audit.AssignmentId);
        Assert.Equal("point-admin", audit.AdminUserId);
        Assert.Equal("point-admin@test.invalid", audit.AdminUser!.UserName);
        Assert.Equal("MP-POINT-ORDER-1", audit.ExternalOrderId);
        Assert.Equal(PaymentStatus.Pending, audit.NewStatus);
        Assert.Equal(1, audit.AttemptNumber);
        Assert.Equal("POINT_ORDER_IDENTIFIERS_RECEIVED", audit.ResultCode);
        Assert.DoesNotContain("token", audit.ResultSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_response_without_aggregate_amount_persists_order_id_and_retry_polls_instead_of_creating_again()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seed = await AddPendingOrderAsync(db);
        var provider = new FakePointProvider { OmitTotalAmountOnCreate = true };
        var sut = CreateService(db, provider);

        var create = await sut.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, RequestKey);
        var persisted = await db.Payments.SingleAsync(payment => payment.OrderId == seed.Order.Id);

        Assert.False(create.Succeeded);
        Assert.Equal("POINT_AMOUNT_MISSING", create.ErrorCode);
        Assert.Equal("MP-POINT-ORDER-1", persisted.GatewayOrderId);
        Assert.Equal(PaymentStatus.Pending, persisted.Status);
        Assert.Equal(1, provider.CreateCalls);
        provider.StatusResult = CreatedResult with
        {
            ExternalReference = persisted.ExternalReference,
            TotalAmount = seed.Order.Total
        };

        var retry = await sut.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, RequestKey);

        Assert.True(retry.Succeeded, retry.ErrorCode);
        Assert.Equal(PaymentStatus.Pending, retry.Payment!.Status);
        Assert.Equal(1, provider.CreateCalls);
        Assert.Equal(1, provider.GetCalls);
        Assert.Equal(persisted.IdempotencyKey, retry.Payment.IdempotencyKey);
    }

    [Fact]
    public async Task StartChargeAsync_repeated_request_nonce_reuses_the_same_active_attempt()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seed = await AddPendingOrderAsync(db);
        var provider = new FakePointProvider();
        var sut = CreateService(db, provider);

        var first = await sut.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, RequestKey);
        var duplicate = await sut.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, RequestKey);

        Assert.Equal(first.Payment!.Id, duplicate.Payment!.Id);
        Assert.Equal(1, provider.CreateCalls);
        Assert.Single(await db.Payments.Where(payment => payment.OrderId == seed.Order.Id).ToListAsync());
    }

    [Fact]
    public async Task Simultaneous_duplicate_requests_share_one_persisted_idempotency_key_and_one_provider_order()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seed = await AddPendingOrderAsync(db);
        var provider = new FakePointProvider { BlockCreateUntilReleased = true, ExpectedBlockedCreateCalls = 2 };
        var sut = CreateService(db, provider);

        var firstTask = sut.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, RequestKey);
        await provider.FirstCreateEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var secondTask = sut.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, Guid.NewGuid().ToString("N"));
        await provider.ExpectedCreateCallsEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        provider.ReleaseCreate.TrySetResult();
        var results = await Task.WhenAll(firstTask, secondTask);

        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Single(results.Select(result => result.Payment!.Id).Distinct());
        Assert.Single(provider.Requests.Select(request => request.IdempotencyKey).Distinct());
        Assert.Single(provider.Requests.Distinct());
        Assert.Single(provider.ExternalOrdersByIdempotencyKey);
        Assert.Single(await db.Payments.Where(payment => payment.OrderId == seed.Order.Id).ToListAsync());
    }

    [Fact]
    public async Task Ten_simultaneous_requests_for_same_order_use_one_attempt_and_one_provider_key()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seed = await AddPendingOrderAsync(db);
        var provider = new FakePointProvider { BlockCreateUntilReleased = true, ExpectedBlockedCreateCalls = 10 };
        var sut = CreateService(db, provider);

        var calls = Enumerable.Range(0, 10)
            .Select(_ => sut.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, Guid.NewGuid().ToString("N")))
            .ToArray();
        await provider.ExpectedCreateCallsEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        provider.ReleaseCreate.TrySetResult();
        var results = await Task.WhenAll(calls);

        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Single(results.Select(result => result.Payment!.Id).Distinct());
        Assert.Single(provider.Requests.Select(request => request.IdempotencyKey).Distinct());
        Assert.Single(provider.Requests.Distinct());
        Assert.Single(provider.ExternalOrdersByIdempotencyKey);
        Assert.Single(await db.Payments.Where(payment => payment.OrderId == seed.Order.Id).ToListAsync());
    }

    [Fact]
    public async Task StartChargeAsync_timeout_keeps_attempt_and_retry_reuses_provider_idempotency_key()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seed = await AddPendingOrderAsync(db);
        var provider = new FakePointProvider { FailFirstCreate = true };
        var sut = CreateService(db, provider);

        var first = await sut.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, RequestKey);
        var persisted = await db.Payments.SingleAsync(payment => payment.OrderId == seed.Order.Id);
        Assert.False(first.Succeeded);
        Assert.Equal("POINT_GATEWAY_UNAVAILABLE", first.ErrorCode);
        Assert.Equal(PaymentStatus.Pending, persisted.Status);
        Assert.Null(persisted.GatewayOrderId);
        var failureAudit = await db.PointPaymentAuditEvents.SingleAsync(x => x.ResultCode == "POINT_GATEWAY_UNAVAILABLE");
        Assert.Equal(PaymentStatus.Pending, failureAudit.PreviousStatus);
        Assert.Equal(PaymentStatus.Pending, failureAudit.NewStatus);
        Assert.DoesNotContain("authorization", failureAudit.ResultSummary, StringComparison.OrdinalIgnoreCase);
        var originalKey = persisted.IdempotencyKey;
        var retry = await sut.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, RequestKey);

        Assert.True(retry.Succeeded);
        Assert.Equal(2, provider.CreateCalls);
        Assert.Equal(provider.Requests[0].IdempotencyKey, provider.Requests[1].IdempotencyKey);
        Assert.Equal(originalKey, provider.Requests[1].IdempotencyKey);
    }

    [Fact]
    public async Task Cancellation_after_commit_before_provider_processing_keeps_attempt_for_restarted_service()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seed = await AddPendingOrderAsync(db);
        var provider = new FakePointProvider { BlockCreateUntilReleased = true, ExpectedBlockedCreateCalls = 1 };
        using var processLifetime = new CancellationTokenSource();
        var firstProcessService = CreateService(db, provider);

        var firstProcessTask = firstProcessService.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, RequestKey, processLifetime.Token);
        await provider.FirstCreateEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var committedAttempt = await db.Payments.SingleAsync(payment => payment.OrderId == seed.Order.Id);
        processLifetime.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstProcessTask);
        Assert.NotNull(committedAttempt.IdempotencyKey);
        Assert.Null(committedAttempt.GatewayOrderId);
        Assert.Empty(provider.ExternalOrdersByIdempotencyKey);

        provider.ReleaseCreate.TrySetResult();
        var restartedService = CreateService(db, provider);
        var retry = await restartedService.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, Guid.NewGuid().ToString("N"));

        Assert.True(retry.Succeeded);
        Assert.Equal(committedAttempt.IdempotencyKey, provider.Requests[1].IdempotencyKey);
        Assert.Single(provider.ExternalOrdersByIdempotencyKey);
        Assert.Single(await db.Payments.Where(payment => payment.OrderId == seed.Order.Id).ToListAsync());
    }

    [Fact]
    public async Task Lost_create_response_and_service_recreation_reuse_the_persisted_key_and_one_logical_order()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seed = await AddPendingOrderAsync(db);
        var provider = new FakePointProvider { LoseFirstCreateResponse = true };
        var firstProcessService = CreateService(db, provider);

        var first = await firstProcessService.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, RequestKey);
        var persisted = await db.Payments.SingleAsync(payment => payment.OrderId == seed.Order.Id);
        var firstRequest = Assert.Single(provider.Requests);
        Assert.False(first.Succeeded);
        Assert.Equal("POINT_GATEWAY_UNAVAILABLE", first.ErrorCode);
        Assert.Null(persisted.GatewayOrderId);
        Assert.Equal(firstRequest.IdempotencyKey, persisted.IdempotencyKey);
        Assert.Single(provider.ExternalOrdersByIdempotencyKey);

        // A new service and gate represent a restarted process; the database and provider retain their state.
        var restartedService = CreateService(db, provider);
        var retry = await restartedService.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, Guid.NewGuid().ToString("N"));
        var retryRequest = provider.Requests[1];

        Assert.True(retry.Succeeded);
        Assert.Equal(firstRequest.IdempotencyKey, retryRequest.IdempotencyKey);
        Assert.Equal(firstRequest.OrderId, retryRequest.OrderId);
        Assert.Equal(firstRequest.Amount, retryRequest.Amount);
        Assert.Equal(firstRequest.ExternalReference, retryRequest.ExternalReference);
        Assert.Equal(firstRequest.DeviceId, retryRequest.DeviceId);
        Assert.Equal(2, provider.CreateCalls);
        Assert.Single(provider.ExternalOrdersByIdempotencyKey);
        Assert.Single(await db.Payments.Where(payment => payment.OrderId == seed.Order.Id).ToListAsync());
    }

    [Fact]
    public async Task Different_orders_can_enter_claim_independently_with_the_shared_singleton_gate()
    {
        await using var firstDb = await TestDbContextFactory.CreateAsync();
        var firstSeed = await AddPendingOrderAsync(firstDb);
        await using var secondDb = await TestDbContextFactory.CreateAsync();
        var secondTerminalSeed = await AddPendingOrderAsync(secondDb);
        var secondSeed = await AddOrderUsingAssignmentAsync(secondDb, secondTerminalSeed.Assignment);
        var sharedGate = new PointPaymentAttemptGate();
        var firstLock = new BlockingOrderLock(firstDb, firstSeed.Order.Id);
        var provider = new FakePointProvider();
        var firstService = CreateService(firstDb, provider, orderLock: firstLock, attemptGate: sharedGate);
        var secondService = CreateService(secondDb, provider, attemptGate: sharedGate);

        var firstTask = firstService.StartChargeAsync(firstSeed.Order.Id, firstSeed.Assignment.Id, RequestKey);
        await firstLock.BlockedOrderReached.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var secondTask = secondService.StartChargeAsync(secondSeed.Order.Id, secondSeed.Assignment.Id, RequestKey);
        var secondResult = await secondTask.WaitAsync(TimeSpan.FromSeconds(5));
        firstLock.ReleaseBlockedOrder.TrySetResult();
        var firstResult = await firstTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(firstResult.Succeeded);
        Assert.True(secondResult.Succeeded);
        Assert.Equal(2, provider.CreateCalls);
        Assert.Equal(2, provider.ExternalOrdersByIdempotencyKey.Count);
    }

    [Fact]
    public async Task Refresh_while_provider_create_is_in_flight_returns_retryable_not_created_result()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seed = await AddPendingOrderAsync(db);
        var provider = new FakePointProvider { BlockCreateUntilReleased = true, ExpectedBlockedCreateCalls = 1 };
        var sut = CreateService(db, provider);

        var createTask = sut.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, RequestKey);
        await provider.FirstCreateEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var payment = await db.Payments.SingleAsync(candidate => candidate.OrderId == seed.Order.Id);

        var webhookRace = await sut.RefreshAsync(payment.Id);

        Assert.False(webhookRace.Succeeded);
        Assert.Equal("POINT_ORDER_NOT_CREATED", webhookRace.ErrorCode);
        Assert.Equal(0, provider.GetCalls);
        provider.ReleaseCreate.TrySetResult();
        Assert.True((await createTask.WaitAsync(TimeSpan.FromSeconds(5))).Succeeded);
    }

    [Fact]
    public async Task StartChargeAsync_rejects_assignment_change_for_an_existing_active_attempt()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seed = await AddPendingOrderAsync(db);
        var secondDriver = new Driver { Name = "Outro motorista" };
        db.Add(secondDriver);
        await db.SaveChangesAsync();
        var provider = new FakePointProvider();
        var sut = CreateService(db, provider);

        var first = await sut.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, RequestKey);
        seed.Assignment.EndedAt = DateTime.UtcNow;
        var secondAssignment = new DriverPaymentTerminalAssignment
        {
            DriverId = secondDriver.Id,
            PaymentTerminalId = seed.Assignment.PaymentTerminalId,
            StartedAt = DateTime.UtcNow
        };
        db.DriverPaymentTerminalAssignments.Add(secondAssignment);
        await db.SaveChangesAsync();
        var conflict = await sut.StartChargeAsync(seed.Order.Id, secondAssignment.Id, Guid.NewGuid().ToString("N"));

        Assert.True(first.Succeeded);
        Assert.False(conflict.Succeeded);
        Assert.Equal("POINT_ACTIVE_ATTEMPT_CONFLICT", conflict.ErrorCode);
        Assert.Equal(1, provider.CreateCalls);
        Assert.Equal(seed.Assignment.Id, (await db.Payments.SingleAsync(payment => payment.OrderId == seed.Order.Id)).DriverPaymentTerminalAssignmentId);
    }

    [Theory]
    [InlineData(false, "Test", "POINT_INTEGRATION_DISABLED")]
    [InlineData(true, "Production", "POINT_TEST_ENVIRONMENT_REQUIRED")]
    public async Task StartChargeAsync_enforces_feature_and_test_environment(bool enabled, string environment, string errorCode)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seed = await AddPendingOrderAsync(db);
        var provider = new FakePointProvider();
        var sut = CreateService(db, provider, pointEnabled: enabled, environmentName: environment);

        var result = await sut.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, RequestKey);

        Assert.False(result.Succeeded);
        Assert.Equal(errorCode, result.ErrorCode);
        Assert.Equal(0, provider.CreateCalls);
    }

    [Fact]
    public async Task StartChargeAsync_without_test_token_returns_controlled_failure_before_provider_call()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seed = await AddPendingOrderAsync(db);
        var provider = new FakePointProvider();
        var sut = CreateService(db, provider, tokenConfigured: false);

        var result = await sut.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, RequestKey);

        Assert.False(result.Succeeded);
        Assert.Equal("POINT_TEST_CREDENTIALS_REQUIRED", result.ErrorCode);
        Assert.Equal(0, provider.CreateCalls);
    }

    [Fact]
    public async Task StartChargeAsync_rejects_non_card_on_delivery_order_before_provider_call()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seed = await AddPendingOrderAsync(db, paymentCode: "CASH", method: PaymentMethodType.Cash);
        var provider = new FakePointProvider();
        var sut = CreateService(db, provider);

        var result = await sut.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, RequestKey);

        Assert.False(result.Succeeded);
        Assert.Equal("POINT_ORDER_METHOD_INVALID", result.ErrorCode);
        Assert.Equal(0, provider.CreateCalls);
    }

    [Theory]
    [InlineData(false, true, true, "POINT_ASSIGNMENT_INVALID")]
    [InlineData(true, false, true, "POINT_ASSIGNMENT_INVALID")]
    [InlineData(true, true, false, "POINT_ASSIGNMENT_INVALID")]
    public async Task StartChargeAsync_rejects_inactive_driver_terminal_or_assignment(
        bool driverActive, bool terminalActive, bool assignmentActive, string errorCode)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seed = await AddPendingOrderAsync(db, driverActive, terminalActive, assignmentActive);
        var provider = new FakePointProvider();
        var sut = CreateService(db, provider);

        var result = await sut.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, RequestKey);

        Assert.False(result.Succeeded);
        Assert.Equal(errorCode, result.ErrorCode);
        Assert.Equal(0, provider.CreateCalls);
    }

    [Fact]
    public async Task StartChargeAsync_refuses_an_already_approved_payment()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seed = await AddPendingOrderAsync(db);
        seed.Payment.Status = PaymentStatus.Approved;
        seed.Payment.PaidAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var provider = new FakePointProvider();
        var sut = CreateService(db, provider);

        var result = await sut.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, RequestKey);

        Assert.False(result.Succeeded);
        Assert.Equal("POINT_PAYMENT_ALREADY_APPROVED", result.ErrorCode);
        Assert.Equal(0, provider.CreateCalls);
    }

    [Fact]
    public async Task Concurrent_status_refreshes_approve_once_and_late_created_status_cannot_regress_payment()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seed = await AddPendingOrderAsync(db);
        var provider = new FakePointProvider();
        var approval = new RecordingApprovalHandler();
        var sut = CreateService(db, provider, approval: approval);
        var started = await sut.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, RequestKey);
        Assert.True(started.Succeeded);

        provider.StatusResult = new PointPaymentResult(
            PointPaymentState.Approved, "", "Approved", PaymentStatus.Approved,
            started.Payment!.GatewayOrderId, "MP-POINT-PAYMENT-1", started.Payment.ExternalReference,
            started.Payment.Amount, "processed", "accredited", ValidFinancials(started.Payment.Amount));
        var results = await Task.WhenAll(
            sut.RefreshAsync(started.Payment.Id),
            sut.RefreshAsync(started.Payment.Id));
        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Equal(PaymentStatus.Approved, seed.Payment.Status);
        Assert.Equal(1, approval.ApprovalCalls);

        provider.StatusResult = CreatedResult with
        {
            GatewayOrderId = started.Payment.GatewayOrderId,
            ExternalReference = started.Payment.ExternalReference,
            TotalAmount = started.Payment.Amount
        };
        var oldEventResult = await sut.RefreshAsync(started.Payment.Id);

        Assert.True(oldEventResult.Succeeded);
        Assert.Equal(PaymentStatus.Approved, seed.Payment.Status);
        Assert.Equal(1, approval.ApprovalCalls);
    }

    [Fact]
    public async Task Late_provider_processing_result_cannot_regress_approval_after_another_scope_commits()
    {
        await using var firstDb = await TestDbContextFactory.CreateAsync();
        var seed = await AddPendingOrderAsync(firstDb);
        var secondOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(firstDb.Database.GetDbConnection())
            .Options;
        await using var secondDb = new ApplicationDbContext(secondOptions);
        var provider = new FakePointProvider
        {
            BlockFirstCreateUntilReleased = true,
            FirstCreateResult = CreatedResult with
            {
                State = PointPaymentState.Processing,
                PaymentStatus = PaymentStatus.Processing,
                ProviderStatus = "in_process",
                ProviderStatusDetail = "waiting_for_terminal"
            },
            SecondCreateResult = CreatedResult with
            {
                State = PointPaymentState.Approved,
                PaymentStatus = PaymentStatus.Approved,
                ProviderStatus = "processed",
                ProviderStatusDetail = "accredited",
                Financials = ValidFinancials(125.50m)
            }
        };
        var approvalOne = new RecordingApprovalHandler();
        var approvalTwo = new RecordingApprovalHandler();
        var sharedGate = new PointPaymentAttemptGate();
        var firstService = CreateService(firstDb, provider, approval: approvalOne, attemptGate: sharedGate);
        var secondService = CreateService(secondDb, provider, approval: approvalTwo, attemptGate: sharedGate);

        var firstTask = firstService.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, RequestKey);
        await provider.FirstCreateEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var secondResult = await secondService.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, Guid.NewGuid().ToString("N"));
        Assert.True(secondResult.Succeeded);
        Assert.Equal(PaymentStatus.Approved, secondResult.Payment!.Status);

        provider.ReleaseCreate.TrySetResult();
        var firstResult = await firstTask.WaitAsync(TimeSpan.FromSeconds(5));
        await secondDb.Entry(secondResult.Payment).ReloadAsync();

        Assert.True(firstResult.Succeeded);
        Assert.Equal(PaymentStatus.Approved, secondResult.Payment.Status);
        Assert.NotNull(secondResult.Payment.PaidAt);
        Assert.Equal(0, approvalOne.ApprovalCalls);
        Assert.Equal(1, approvalTwo.ApprovalCalls);
    }

    [Theory]
    [InlineData("wrong-reference", "125.50", "POINT_REFERENCE_MISMATCH")]
    [InlineData("matching-reference", "999.99", "POINT_AMOUNT_MISMATCH")]
    [InlineData(null, "125.50", "POINT_REFERENCE_MISSING")]
    [InlineData("matching-reference", null, "POINT_AMOUNT_MISSING")]
    public async Task Status_refresh_rejects_missing_or_mismatched_reference_and_amount_before_approval(string? reference, string? amount, string expectedErrorCode)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seed = await AddPendingOrderAsync(db);
        var provider = new FakePointProvider();
        var approval = new RecordingApprovalHandler();
        var sut = CreateService(db, provider, approval: approval);
        var started = await sut.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, RequestKey);
        Assert.True(started.Succeeded);
        provider.StatusResult = new PointPaymentResult(
            PointPaymentState.Approved, "", "Approved", PaymentStatus.Approved,
            started.Payment!.GatewayOrderId, "MP-POINT-PAYMENT-1", reference == "matching-reference" ? started.Payment.ExternalReference : reference,
            amount is null ? null : decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), "processed", "accredited",
            ValidFinancials(started.Payment.Amount));

        var result = await sut.RefreshAsync(started.Payment.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.NotEqual(PaymentStatus.Approved, seed.Payment.Status);
        Assert.Equal(0, approval.ApprovalCalls);
    }

    [Theory]
    [InlineData("missing-transaction", "POINT_TRANSACTION_MISSING")]
    [InlineData("total-paid-mismatch", "POINT_TOTAL_PAID_AMOUNT_MISMATCH")]
    [InlineData("transaction-amount-mismatch", "POINT_TRANSACTION_AMOUNT_MISMATCH")]
    [InlineData("paid-amount-mismatch", "POINT_PAID_AMOUNT_MISMATCH")]
    [InlineData("transaction-status", "POINT_TRANSACTION_STATUS_INVALID")]
    [InlineData("transaction-id-missing", "POINT_TRANSACTION_MISSING")]
    [InlineData("multiple-transactions", "POINT_TRANSACTION_AMBIGUOUS")]
    [InlineData("malformed-total-paid", "POINT_TOTAL_PAID_AMOUNT_MISMATCH")]
    [InlineData("malformed-transaction-amount", "POINT_TRANSACTION_AMOUNT_MISMATCH")]
    [InlineData("malformed-paid-amount", "POINT_PAID_AMOUNT_MISMATCH")]
    public async Task Processed_result_with_incomplete_or_inconsistent_transaction_is_not_approved(string scenario, string expectedErrorCode)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seed = await AddPendingOrderAsync(db);
        var provider = new FakePointProvider();
        var approval = new RecordingApprovalHandler();
        var sut = CreateService(db, provider, approval: approval);
        var started = await sut.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, RequestKey);
        Assert.True(started.Succeeded);

        var valid = ValidFinancials(started.Payment!.Amount);
        provider.StatusResult = ApprovedResult(started.Payment, valid with
        {
            TotalPaidAmount = scenario == "total-paid-mismatch" ? 124m : scenario == "malformed-total-paid" ? null : valid.TotalPaidAmount,
            TotalPaidAmountPresent = scenario is "total-paid-mismatch" or "malformed-total-paid" || valid.TotalPaidAmountPresent,
            TotalPaidAmountInvalid = scenario == "malformed-total-paid",
            Transactions = scenario switch
            {
                "missing-transaction" => [],
                "multiple-transactions" => [valid.Transactions[0], valid.Transactions[0]],
                _ => [valid.Transactions[0] with
                {
                    Id = scenario == "transaction-id-missing" ? null : valid.Transactions[0].Id,
                    Status = scenario == "transaction-status" ? "pending" : valid.Transactions[0].Status,
                    Amount = scenario == "transaction-amount-mismatch" ? 124m : scenario == "malformed-transaction-amount" ? null : valid.Transactions[0].Amount,
                    AmountPresent = scenario == "malformed-transaction-amount" ? true : valid.Transactions[0].AmountPresent,
                    AmountInvalid = scenario == "malformed-transaction-amount",
                    PaidAmount = scenario == "paid-amount-mismatch" ? 124m : scenario == "malformed-paid-amount" ? null : valid.Transactions[0].PaidAmount,
                    PaidAmountPresent = scenario is "paid-amount-mismatch" or "malformed-paid-amount" || valid.Transactions[0].PaidAmountPresent,
                    PaidAmountInvalid = scenario == "malformed-paid-amount"
                }]
            }
        });

        var result = await sut.RefreshAsync(started.Payment.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.Equal(PaymentStatus.Pending, seed.Payment.Status);
        Assert.Null(seed.Payment.PaidAt);
        Assert.Equal(0, approval.ApprovalCalls);
    }

    [Theory]
    [InlineData("created", "created", "Pending")]
    [InlineData("pending", "pending", "Pending")]
    [InlineData("at_terminal", "at_terminal", "Processing")]
    [InlineData("action_required", "check_on_terminal", "ActionRequired")]
    [InlineData("failed", "insufficient_amount", "Rejected")]
    [InlineData("canceled", "canceled_on_terminal", "Cancelled")]
    [InlineData("refunded", "refunded", "Refunded")]
    public async Task Non_final_or_negative_provider_status_never_approves(string providerStatus, string detail, string mappedStatus)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seed = await AddPendingOrderAsync(db);
        var provider = new FakePointProvider();
        var approval = new RecordingApprovalHandler();
        var sut = CreateService(db, provider, approval: approval);
        var started = await sut.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, RequestKey);
        Assert.True(started.Succeeded);

        var mapped = Enum.Parse<PaymentStatus>(mappedStatus);
        provider.StatusResult = new PointPaymentResult(
            mapped == PaymentStatus.Pending ? PointPaymentState.Created : PointPaymentState.Processing,
            "", "Status consultado", mapped,
            started.Payment!.GatewayOrderId, "MP-POINT-PAYMENT-1", started.Payment.ExternalReference,
            started.Payment.Amount, providerStatus, detail, ValidFinancials(started.Payment.Amount));

        var result = await sut.RefreshAsync(started.Payment.Id);

        Assert.NotEqual(PaymentStatus.Approved, seed.Payment.Status);
        Assert.Equal(0, approval.ApprovalCalls);
    }

    [Fact]
    public async Task Claim_and_status_apply_acquire_database_order_lock_inside_each_short_transaction()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seed = await AddPendingOrderAsync(db);
        var orderLock = new RecordingOrderLock(db);
        var provider = new FakePointProvider { ObservedDb = db };
        var sut = CreateService(db, provider, orderLock: orderLock);

        var result = await sut.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, RequestKey);

        Assert.True(result.Succeeded);
        Assert.Equal([seed.Order.Id, seed.Order.Id], orderLock.OrderIds);
        Assert.Equal(2, orderLock.CallsInsideTransaction);
        Assert.Equal(1, provider.CreateCalls);
        Assert.False(provider.ProviderCallObservedActiveTransaction);
    }

    [Theory]
    [InlineData(Npgsql.PostgresErrorCodes.SerializationFailure)]
    [InlineData(Npgsql.PostgresErrorCodes.UniqueViolation)]
    [InlineData(Npgsql.PostgresErrorCodes.DeadlockDetected)]
    public async Task Expected_postgres_write_conflict_returns_controlled_failure_before_provider_call(string sqlState)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seed = await AddPendingOrderAsync(db);
        var provider = new FakePointProvider();
        var orderLock = new RecordingOrderLock(db)
        {
            Failure = new Npgsql.PostgresException("database write conflict", "ERROR", "ERROR", sqlState)
        };
        var sut = CreateService(db, provider, orderLock: orderLock);

        var result = await sut.StartChargeAsync(seed.Order.Id, seed.Assignment.Id, RequestKey);

        Assert.False(result.Succeeded);
        Assert.Equal("POINT_CHARGE_CONFLICT", result.ErrorCode);
        Assert.Equal(0, provider.CreateCalls);
    }

    private static PointPaymentOrchestrationService CreateService(
        ApplicationDbContext db,
        IPointPaymentProvider provider,
        bool pointEnabled = true,
        string environmentName = "Test",
        bool tokenConfigured = true,
        RecordingApprovalHandler? approval = null,
        IPointPaymentOrderConcurrencyLock? orderLock = null,
        PointPaymentAttemptGate? attemptGate = null) => new(
        db,
        provider,
        new PaymentTerminalEligibilityService(),
        Options.Create(new MercadoPagoPointOptions { Enabled = pointEnabled, Environment = environmentName, AccessToken = tokenConfigured ? "fake-test-token" : "" }),
        Options.Create(new PaymentEligibilityOptions { CardOnDeliveryEnabled = true }),
        new TestHostEnvironment(environmentName),
        approval ?? new RecordingApprovalHandler(),
        TimeProvider.System,
        attemptGate ?? new PointPaymentAttemptGate(),
        orderLock ?? new PointPaymentOrderConcurrencyLock(db));

    private static async Task<(Order Order, Payment Payment, DriverPaymentTerminalAssignment Assignment)> AddPendingOrderAsync(
        ApplicationDbContext db,
        bool driverActive = true,
        bool terminalActive = true,
        bool assignmentActive = true,
        string paymentCode = "CARD_ON_DELIVERY",
        PaymentMethodType method = PaymentMethodType.CardOnDelivery)
    {
        var customer = new Customer
        {
            LegalName = "Point Test Customer Ltda",
            TradeName = "Point Test Customer",
            Cnpj = Guid.NewGuid().ToString("N")[..14],
            Email = "point@test.invalid"
        };
        var term = new Orofoods.Web.Models.Pricing.PaymentTerm { Code = paymentCode, Name = paymentCode, IsActive = true };
        var order = new Order { Customer = customer, PaymentTerm = term, PaymentMethod = paymentCode, Total = 125.50m, Status = OrderStatus.OutForDelivery };
        var payment = new Payment
        {
            Customer = customer,
            Order = order,
            PaymentMethod = paymentCode,
            Method = method,
            Amount = order.Total,
            Status = PaymentStatus.Pending
        };
        order.Payments.Add(payment);
        var driver = new Driver { Name = "Motorista teste", IsActive = driverActive };
        var terminal = new PaymentTerminal
        {
            Provider = PaymentTerminalProvider.MercadoPago,
            DeviceId = "SBX0000001",
            StoreId = "store-test",
            PosId = "pos-test",
            IsActive = terminalActive
        };
        var assignment = new DriverPaymentTerminalAssignment
        {
            Driver = driver,
            PaymentTerminal = terminal,
            StartedAt = DateTime.UtcNow,
            EndedAt = assignmentActive ? null : DateTime.UtcNow
        };
        db.AddRange(order, driver, terminal, assignment);
        await db.SaveChangesAsync();
        return (order, payment, assignment);
    }

    private static async Task<(Order Order, Payment Payment, DriverPaymentTerminalAssignment Assignment)> AddOrderUsingAssignmentAsync(
        ApplicationDbContext db,
        DriverPaymentTerminalAssignment assignment)
    {
        var customer = new Customer
        {
            LegalName = "Second Point Test Customer Ltda",
            TradeName = "Second Point Test Customer",
            Cnpj = Guid.NewGuid().ToString("N")[..14],
            Email = "second-point@test.invalid"
        };
        var term = new Orofoods.Web.Models.Pricing.PaymentTerm { Code = "CARD_ON_DELIVERY", Name = "CARD_ON_DELIVERY", IsActive = true };
        var order = new Order { Customer = customer, PaymentTerm = term, PaymentMethod = term.Code, Total = 125.50m, Status = OrderStatus.OutForDelivery };
        var payment = new Payment
        {
            Customer = customer,
            Order = order,
            PaymentMethod = term.Code,
            Method = PaymentMethodType.CardOnDelivery,
            Amount = order.Total,
            Status = PaymentStatus.Pending
        };
        order.Payments.Add(payment);
        db.AddRange(order);
        await db.SaveChangesAsync();
        return (order, payment, assignment);
    }

    private const string RequestKey = "e33399d1378d4bcb8219bb7a99cdedcf";

    private static PointPaymentFinancials ValidFinancials(decimal amount) => new(
        amount,
        TotalPaidAmountPresent: true,
        TotalPaidAmountInvalid: false,
        [new PointPaymentTransaction(
            "MP-POINT-PAYMENT-1", "processed", "accredited", amount,
            AmountPresent: true, AmountInvalid: false,
            PaidAmount: amount, PaidAmountPresent: true, PaidAmountInvalid: false)]);

    private static PointPaymentResult ApprovedResult(Payment payment, PointPaymentFinancials financials) => new(
        PointPaymentState.Approved, "", "Approved", PaymentStatus.Approved,
        payment.GatewayOrderId, "MP-POINT-PAYMENT-1", payment.ExternalReference,
        payment.Amount, "processed", "accredited", financials);

    private sealed class FakePointProvider : IPointPaymentProvider
    {
        private readonly object _sync = new();
        private readonly List<PointPaymentRequest> _requests = [];
        private readonly HashSet<string> _externalOrdersByIdempotencyKey = [];
        private int _createCalls;

        public int CreateCalls => Volatile.Read(ref _createCalls);
        public bool FailFirstCreate { get; init; }
        public bool LoseFirstCreateResponse { get; init; }
        public bool OmitTotalAmountOnCreate { get; init; }
        public bool BlockCreateUntilReleased { get; init; }
        public bool BlockFirstCreateUntilReleased { get; init; }
        public PointPaymentResult? FirstCreateResult { get; init; }
        public PointPaymentResult? SecondCreateResult { get; init; }
        public int ExpectedBlockedCreateCalls { get; init; } = 1;
        public IReadOnlyList<PointPaymentRequest> Requests { get { lock (_sync) return _requests.ToArray(); } }
        public IReadOnlyCollection<string> ExternalOrdersByIdempotencyKey { get { lock (_sync) return _externalOrdersByIdempotencyKey.ToArray(); } }
        public TaskCompletionSource FirstCreateEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ExpectedCreateCallsEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseCreate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public PointPaymentResult StatusResult { get; set; } = CreatedResult;
        public int GetCalls { get; private set; }
        public ApplicationDbContext? ObservedDb { get; init; }
        public bool ProviderCallObservedActiveTransaction { get; private set; }

        public async Task<PointPaymentResult> CreateTerminalPaymentAsync(PointPaymentRequest request, CancellationToken cancellationToken = default)
        {
            ProviderCallObservedActiveTransaction |= ObservedDb?.Database.CurrentTransaction is not null;
            var call = Interlocked.Increment(ref _createCalls);
            lock (_sync) _requests.Add(request);
            FirstCreateEntered.TrySetResult();
            if (call >= ExpectedBlockedCreateCalls) ExpectedCreateCallsEntered.TrySetResult();
            if (BlockCreateUntilReleased || (BlockFirstCreateUntilReleased && call == 1))
            {
                await ReleaseCreate.Task.WaitAsync(cancellationToken);
            }
            if (FailFirstCreate && call == 1) throw new HttpRequestException("simulated network loss");
            lock (_sync) _externalOrdersByIdempotencyKey.Add(request.IdempotencyKey);
            if (LoseFirstCreateResponse && call == 1) throw new HttpRequestException("simulated response lost after provider processing");
            var configuredResult = (call == 1 ? FirstCreateResult : SecondCreateResult) ?? CreatedResult;
            return configuredResult with
            {
                GatewayOrderId = "MP-POINT-ORDER-1",
                GatewayPaymentId = "MP-POINT-PAYMENT-1",
                ExternalReference = request.ExternalReference,
                TotalAmount = OmitTotalAmountOnCreate ? null : request.Amount
            };
        }

        public Task<PointPaymentResult> GetPaymentStatusAsync(string externalPaymentId, CancellationToken cancellationToken = default)
        {
            ProviderCallObservedActiveTransaction |= ObservedDb?.Database.CurrentTransaction is not null;
            GetCalls++;
            return Task.FromResult(StatusResult with { GatewayOrderId = externalPaymentId });
        }

        public Task<PointPaymentResult> CancelPendingPaymentAsync(string externalPaymentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(StatusResult with { GatewayOrderId = externalPaymentId });
    }

    private static readonly PointPaymentResult CreatedResult = new(
        PointPaymentState.Created, "", "Created", PaymentStatus.Pending,
        "MP-POINT-ORDER-1", "MP-POINT-PAYMENT-1", "", 125.50m, "created", "created", ValidFinancials(125.50m));

    private sealed class RecordingApprovalHandler : IPaymentApprovalHandler
    {
        public int ApprovalCalls { get; private set; }
        public Task PaymentApprovedAsync(Payment payment, CancellationToken cancellationToken = default)
        {
            ApprovalCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOrderLock(ApplicationDbContext db) : IPointPaymentOrderConcurrencyLock
    {
        public List<int> OrderIds { get; } = [];
        public int CallsInsideTransaction { get; private set; }
        public Exception? Failure { get; init; }
        public IsolationLevel TransactionIsolationLevel => IsolationLevel.Serializable;

        public Task<bool> AcquireAsync(int orderId, CancellationToken cancellationToken = default)
        {
            OrderIds.Add(orderId);
            if (db.Database.CurrentTransaction is not null) CallsInsideTransaction++;
            if (Failure is not null) throw Failure;
            return Task.FromResult(true);
        }
    }

    private sealed class BlockingOrderLock(ApplicationDbContext db, int blockedOrderId) : IPointPaymentOrderConcurrencyLock
    {
        private int _blockOnce;
        public TaskCompletionSource BlockedOrderReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseBlockedOrder { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IsolationLevel TransactionIsolationLevel => IsolationLevel.Serializable;

        public async Task<bool> AcquireAsync(int orderId, CancellationToken cancellationToken = default)
        {
            Assert.NotNull(db.Database.CurrentTransaction);
            if (orderId == blockedOrderId && Interlocked.Exchange(ref _blockOnce, 1) == 0)
            {
                BlockedOrderReached.TrySetResult();
                await ReleaseBlockedOrder.Task.WaitAsync(cancellationToken);
            }
            return true;
        }
    }

    private sealed class TestHostEnvironment(string environmentName) : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Orofoods.Web.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
