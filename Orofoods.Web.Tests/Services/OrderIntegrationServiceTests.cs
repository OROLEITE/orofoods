using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Integrations.Erp;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Integrations;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Tests.Infrastructure;
using System.Net;

namespace Orofoods.Web.Tests.Services;

public class OrderIntegrationServiceTests
{
    [Fact]
    public async Task Loads_customer_and_products_before_sending_the_order_to_the_erp()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Paes", Slug = "paes", IsActive = true };
        var product = new Product { Sku = "BIM-001", WmcCode = "610601552", Name = "Pao", ProductCategory = category, Brand = "BIMBO", Unit = "caixa", BasePrice = 50m };
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99", WmcCode = "107072" };
        var user = new ApplicationUser { Id = "user-1", UserName = "user@orofoods.local", NormalizedUserName = "USER@OROFOODS.LOCAL", Email = "user@orofoods.local", NormalizedEmail = "USER@OROFOODS.LOCAL" };
        var order = new Order { Number = "ORO-2026-000777", Customer = customer, CreatedByUser = user, Total = 100m, Status = OrderStatus.Approved };
        order.Items.Add(new OrderItem { Product = product, ProductNameSnapshot = "Pao", SkuSnapshot = "BIM-001", Quantity = 2, UnitPrice = 50m, Subtotal = 100m });
        db.Add(order);
        await db.SaveChangesAsync();
        var erp = new CapturingErpOrderIntegration();

        await new OrderIntegrationService(db, erp).SendAsync(order.Id);

        Assert.True(erp.ReceivedMappedOrder);
        Assert.NotNull(erp.AttemptId);
        Assert.NotEqual(Guid.Empty, erp.AttemptId);
        var persisted = await db.Orders.SingleAsync(item => item.Id == order.Id);
        Assert.Equal("WMC_ORO2026000777.txt", persisted.ExternalOrderId);
    }

    [Fact]
    public async Task Marks_the_order_as_failed_when_the_erp_adapter_throws()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        var user = new ApplicationUser { Id = "user-2", UserName = "user2@orofoods.local", NormalizedUserName = "USER2@OROFOODS.LOCAL", Email = "user2@orofoods.local", NormalizedEmail = "USER2@OROFOODS.LOCAL" };
        var order = new Order { Number = "ORO-2026-000778", Customer = customer, CreatedByUser = user, Status = OrderStatus.Approved };
        db.Add(order);
        await db.SaveChangesAsync();

        await new OrderIntegrationService(db, new ThrowingErpOrderIntegration()).SendAsync(order.Id);

        var persisted = await db.Orders.SingleAsync(item => item.Id == order.Id);
        Assert.Equal(IntegrationStatus.Failed, persisted.IntegrationStatus);
        Assert.Equal("Falha inesperada ao enviar o pedido ao ERP.", persisted.IntegrationError);
    }

    [Theory]
    [InlineData(OrderStatus.Draft)]
    [InlineData(OrderStatus.Received)]
    [InlineData(OrderStatus.UnderReview)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Picking)]
    [InlineData(OrderStatus.Invoiced)]
    [InlineData(OrderStatus.OutForDelivery)]
    [InlineData(OrderStatus.Delivered)]
    public async Task Does_not_start_or_consume_an_attempt_for_an_order_that_is_not_approved(OrderStatus status)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        var user = new ApplicationUser { Id = "user-ineligible", UserName = "ineligible@orofoods.local", NormalizedUserName = "INELIGIBLE@OROFOODS.LOCAL", Email = "ineligible@orofoods.local", NormalizedEmail = "INELIGIBLE@OROFOODS.LOCAL" };
        var order = new Order
        {
            Number = $"ORO-2026-{(int)status:000000}",
            Customer = customer,
            CreatedByUser = user,
            Status = status,
            IntegrationError = "erro anterior"
        };
        db.Add(order);
        await db.SaveChangesAsync();
        var erp = new CapturingErpOrderIntegration();

        await new OrderIntegrationService(db, erp).SendAsync(order.Id);

        Assert.Equal(0, erp.SendCount);
        Assert.Equal(IntegrationStatus.Pending, order.IntegrationStatus);
        Assert.Null(order.LastIntegrationAttempt);
        Assert.Equal("erro anterior", order.IntegrationError);
    }

    [Fact]
    public async Task Direct_send_does_not_change_an_order_already_imported_manually_in_wmc()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var attemptedAt = DateTime.UtcNow.AddMinutes(-10);
        var importedAt = DateTime.UtcNow.AddMinutes(-5);
        var order = CreateOrderWithWmcState(
            "ORO-2026-000779",
            IntegrationStatus.Failed,
            WmcOrderExportStatus.Imported);
        order.IntegrationError = "Falha anterior do ERP";
        order.LastIntegrationAttempt = attemptedAt;
        order.ExternalOrderId = "WMC_ORO2026000779.txt";
        order.ErpOrderNumber = "ERP-779";
        order.WmcOrderExportState!.ImportedAt = importedAt;
        order.WmcExportAudits.Add(new WmcExportAudit
        {
            Outcome = WmcExportOutcome.Imported,
            Succeeded = true,
            FileName = "WMC_ORO2026000779.txt",
            ExportedAt = importedAt
        });
        db.Add(order);
        await db.SaveChangesAsync();
        var erp = new CountingErpOrderIntegration();

        await new OrderIntegrationService(db, erp).SendAsync(order.Id);

        Assert.Equal(0, erp.SendCount);
        var persisted = await db.Orders.Include(item => item.WmcOrderExportState).SingleAsync(item => item.Id == order.Id);
        Assert.Equal(IntegrationStatus.Failed, persisted.IntegrationStatus);
        Assert.Equal("Falha anterior do ERP", persisted.IntegrationError);
        Assert.Equal(attemptedAt, persisted.LastIntegrationAttempt);
        Assert.Equal("WMC_ORO2026000779.txt", persisted.ExternalOrderId);
        Assert.Equal("ERP-779", persisted.ErpOrderNumber);
        Assert.Equal(WmcOrderExportStatus.Imported, persisted.WmcOrderExportState!.Status);
        Assert.Equal(importedAt, persisted.WmcOrderExportState.ImportedAt);
        var audit = await db.WmcExportAudits.SingleAsync(item => item.OrderId == order.Id);
        Assert.Equal(WmcExportOutcome.Imported, audit.Outcome);
        Assert.Equal("WMC_ORO2026000779.txt", audit.FileName);
    }

    [Fact]
    public async Task Concurrent_direct_sends_do_not_reprocess_an_imported_wmc_order()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"order-integration-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        }.ToString();
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connectionString).Options;
        int orderId;
        await using (var setup = new ApplicationDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            var order = CreateOrderWithWmcState("ORO-2026-000780", IntegrationStatus.Failed, WmcOrderExportStatus.Imported);
            order.IntegrationError = "Falha histórica";
            order.LastIntegrationAttempt = DateTime.UtcNow.AddMinutes(-3);
            order.WmcExportAudits.Add(new WmcExportAudit
            {
                Outcome = WmcExportOutcome.Imported,
                Succeeded = true,
                FileName = "WMC_ORO2026000780.txt"
            });
            setup.Add(order);
            await setup.SaveChangesAsync();
            orderId = order.Id;
        }

        await using var firstDb = new ApplicationDbContext(options);
        await using var secondDb = new ApplicationDbContext(options);
        var erp = new CountingErpOrderIntegration();

        await Task.WhenAll(
            new OrderIntegrationService(firstDb, erp).SendAsync(orderId),
            new OrderIntegrationService(secondDb, erp).SendAsync(orderId));

        Assert.Equal(0, erp.SendCount);
        await using var verification = new ApplicationDbContext(options);
        var persisted = await verification.Orders.Include(item => item.WmcOrderExportState).SingleAsync(item => item.Id == orderId);
        Assert.Equal(IntegrationStatus.Failed, persisted.IntegrationStatus);
        Assert.Equal("Falha histórica", persisted.IntegrationError);
        Assert.NotNull(persisted.LastIntegrationAttempt);
        Assert.Equal(WmcOrderExportStatus.Imported, persisted.WmcOrderExportState!.Status);
        Assert.Equal("WMC_ORO2026000780.txt", await verification.WmcExportAudits
            .Where(item => item.OrderId == orderId)
            .Select(item => item.FileName)
            .SingleAsync());
    }

    [Fact]
    public async Task Reprocessing_remains_available_before_manual_wmc_import_is_confirmed()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateOrderWithWmcState(
            "ORO-2026-000781",
            IntegrationStatus.Pending,
            WmcOrderExportStatus.Exported);
        db.Add(order);
        await db.SaveChangesAsync();
        var erp = new CountingErpOrderIntegration();

        await new OrderIntegrationService(db, erp).SendAsync(order.Id);

        Assert.Equal(1, erp.SendCount);
        Assert.Equal(IntegrationStatus.Succeeded, order.IntegrationStatus);
        Assert.Equal(WmcOrderExportStatus.Exported, order.WmcOrderExportState!.Status);
    }

    [Fact]
    public async Task Selected_send_returns_result_and_blocks_a_second_json_attempt_after_success()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateOrderWithWmcState("ORO-2026-000782", IntegrationStatus.Pending, WmcOrderExportStatus.Pending);
        db.Add(order);
        await db.SaveChangesAsync();
        var erp = new CountingErpOrderIntegration();
        var service = new OrderIntegrationService(db, erp);

        var first = await service.SendSelectedAsync(order.Id, erp);
        var second = await service.SendSelectedAsync(order.Id, erp);

        Assert.True(first?.Succeeded);
        Assert.Null(second);
        Assert.Equal(1, erp.SendCount);
    }

    [Theory]
    [InlineData(WmcExportOutcome.Started)]
    [InlineData(WmcExportOutcome.Unknown)]
    [InlineData(WmcExportOutcome.Succeeded)]
    [InlineData(WmcExportOutcome.Imported)]
    [InlineData(WmcExportOutcome.Generated)]
    [InlineData(WmcExportOutcome.Available)]
    [InlineData(WmcExportOutcome.Regenerated)]
    public async Task Selected_send_does_not_reclaim_json_attempts_that_are_active_uncertain_or_succeeded(WmcExportOutcome outcome)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateOrderWithWmcState($"ORO-2026-{(int)outcome:000000}", IntegrationStatus.Pending, WmcOrderExportStatus.Exported);
        order.WmcExportAudits.Add(new WmcExportAudit
        {
            AttemptId = Guid.NewGuid(),
            Source = WmcExportSource.Automatic,
            Outcome = outcome,
            Succeeded = outcome == WmcExportOutcome.Succeeded,
            ExportedAt = DateTime.UtcNow
        });
        db.Add(order);
        await db.SaveChangesAsync();
        var erp = new CountingErpOrderIntegration();

        var result = await new OrderIntegrationService(db, erp).SendSelectedAsync(order.Id, erp);

        Assert.Null(result);
        Assert.Equal(0, erp.SendCount);
    }

    [Fact]
    public async Task Selected_send_does_not_reclaim_an_order_with_a_generated_txt_artifact()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateOrderWithWmcState("ORO-2026-000783", IntegrationStatus.Pending, WmcOrderExportStatus.Exported);
        db.Add(order);
        await db.SaveChangesAsync();
        var erp = new CountingErpOrderIntegration();

        var result = await new OrderIntegrationService(db, erp).SendSelectedAsync(order.Id, erp);

        Assert.Null(result);
        Assert.Equal(0, erp.SendCount);
    }

    [Fact]
    public async Task Concurrent_selected_sends_reserve_the_order_for_only_one_transmission()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"selected-order-import-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        }.ToString();
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connectionString).Options;
        int orderId;
        await using (var setup = new ApplicationDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            var order = CreateOrderWithWmcState("ORO-2026-000784", IntegrationStatus.Pending, WmcOrderExportStatus.Pending);
            setup.Add(order);
            await setup.SaveChangesAsync();
            orderId = order.Id;
        }

        await using var firstDb = new ApplicationDbContext(options);
        await using var secondDb = new ApplicationDbContext(options);
        var erp = new CountingErpOrderIntegration();
        var results = await Task.WhenAll(
            new OrderIntegrationService(firstDb, erp).SendSelectedAsync(orderId, erp),
            new OrderIntegrationService(secondDb, erp).SendSelectedAsync(orderId, erp));

        Assert.Single(results, result => result?.Succeeded == true);
        Assert.Single(results, result => result is null);
        Assert.Equal(1, erp.SendCount);
    }

    [Fact]
    public async Task Automatic_json_worker_sends_a_general_pending_order_and_marks_it_imported()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateApprovedJsonOrder("ORO-2026-000785");
        db.Add(order);
        await db.SaveChangesAsync();

        var handler = new AutomaticJsonHandler();
        var (service, jsonIntegration) = CreateAutomaticJsonService(db, handler);

        await service.SendAutomaticJsonAsync(order.Id, jsonIntegration);

        var persisted = await db.Orders.Include(item => item.WmcOrderExportState).SingleAsync(item => item.Id == order.Id);
        Assert.Equal(IntegrationStatus.Succeeded, persisted.IntegrationStatus);
        Assert.Equal("9001", persisted.ExternalOrderId);
        Assert.Equal("776655", persisted.ErpOrderNumber);
        Assert.Equal(WmcOrderExportStatus.Imported, persisted.WmcOrderExportState!.Status);
        Assert.NotNull(persisted.WmcOrderExportState.ImportedAt);
        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith("/exportar.php?imei=device-test&serial=serial-test", handler.Requests[0].AbsolutePath + handler.Requests[0].Query, StringComparison.Ordinal);
        Assert.EndsWith("/nropedido.php?idpedido=9001&codvendedor=804", handler.Requests[1].AbsolutePath + handler.Requests[1].Query, StringComparison.Ordinal);
        var audit = await db.WmcExportAudits.SingleAsync(item => item.OrderId == order.Id);
        Assert.Equal(WmcExportSource.Automatic, audit.Source);
        Assert.Equal(WmcExportOutcome.Succeeded, audit.Outcome);
        Assert.Equal(9001, audit.WmcTransmissionId);
    }

    [Fact]
    public async Task Automatic_json_service_does_not_claim_an_order_at_the_activation_cutoff()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateApprovedJsonOrder("ORO-2026-000784");
        db.Add(order);
        await db.SaveChangesAsync();

        var handler = new AutomaticJsonHandler();
        var (service, jsonIntegration) = CreateAutomaticJsonService(
            db,
            handler,
            serviceCutoff: order.Id,
            transmitterCutoff: 0);

        var result = await service.SendAutomaticJsonAsync(order.Id, jsonIntegration);

        Assert.NotNull(result);
        Assert.True(result.AtomicReservationRejected);
        var persisted = await db.Orders.SingleAsync(item => item.Id == order.Id);
        Assert.Equal(IntegrationStatus.Pending, persisted.IntegrationStatus);
        Assert.Null(persisted.LastIntegrationAttempt);
        Assert.Empty(handler.Requests);
        Assert.Empty(db.WmcExportAudits);
    }

    [Fact]
    public async Task Automatic_json_service_logs_a_safe_reason_when_atomic_reservation_matches_no_row()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateApprovedJsonOrder("ORO-2026-000791");
        order.WmcOrderExportState = new WmcOrderExportState { Status = WmcOrderExportStatus.Processing };
        db.Add(order);
        await db.SaveChangesAsync();
        var logger = new CapturingLogger<OrderIntegrationService>();
        var (service, jsonIntegration) = CreateAutomaticJsonService(db, new AutomaticJsonHandler(), logger);

        var result = await service.SendAutomaticJsonAsync(order.Id, jsonIntegration);

        Assert.NotNull(result);
        Assert.True(result.AtomicReservationRejected);
        Assert.False(result.Succeeded);
        Assert.Equal(IntegrationStatus.Pending, (await db.Orders.SingleAsync(candidate => candidate.Id == order.Id)).IntegrationStatus);
        Assert.Empty(db.WmcExportAudits);
        Assert.Contains(logger.Messages, message => message.Contains("atomic reservation", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logger.Messages, message => message.Contains("EligibilityPredicatesMatchedNoRows", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("private", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Automatic_json_service_logs_safe_metadata_when_atomic_reservation_throws()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateApprovedJsonOrder("ORO-2026-000792");
        db.Add(order);
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER FailAutomaticReservation BEFORE UPDATE OF IntegrationStatus ON Orders " +
            "BEGIN SELECT RAISE(ABORT, 'private customer detail'); END;");
        var handler = new AutomaticJsonHandler();
        var logger = new CapturingLogger<OrderIntegrationService>();
        var (service, jsonIntegration) = CreateAutomaticJsonService(db, handler, logger);

        await Assert.ThrowsAnyAsync<Exception>(() => service.SendAutomaticJsonAsync(order.Id, jsonIntegration));

        Assert.Contains(logger.Messages, message => message.Contains("atomic reservation failed", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logger.Messages, message => message.Contains($"OrderId={order.Id}", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("private customer detail", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message => message.Contains(order.Number, StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Exceptions, exception => exception is not null);
        db.ChangeTracker.Clear();
        Assert.Equal(IntegrationStatus.Pending, (await db.Orders.SingleAsync(candidate => candidate.Id == order.Id)).IntegrationStatus);
        Assert.Empty(db.WmcExportAudits);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void Automatic_json_exception_log_contains_only_order_id_and_exception_type()
    {
        var logger = new CapturingLogger<OrderIntegrationService>();

        OrderIntegrationService.LogSafeAutomaticJsonFailure(
            logger,
            orderId: 17,
            new InvalidOperationException("private customer data and credential"));

        var message = Assert.Single(logger.Messages);
        Assert.Contains("OrderId=17", message, StringComparison.Ordinal);
        Assert.Contains("ErrorType=InvalidOperationException", message, StringComparison.Ordinal);
        Assert.DoesNotContain("private customer data", message, StringComparison.Ordinal);
        Assert.DoesNotContain("credential", message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(Assert.Single(logger.Exceptions));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    public async Task Automatic_json_service_does_not_claim_an_order_without_a_valid_activation_cutoff(int? activationCutoff)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateApprovedJsonOrder("ORO-2026-000785");
        db.Add(order);
        await db.SaveChangesAsync();

        var handler = new AutomaticJsonHandler();
        var (service, jsonIntegration) = CreateAutomaticJsonService(
            db,
            handler,
            serviceCutoff: activationCutoff,
            transmitterCutoff: 0);

        var result = await service.SendAutomaticJsonAsync(order.Id, jsonIntegration);

        Assert.Null(result);
        var persisted = await db.Orders.SingleAsync(item => item.Id == order.Id);
        Assert.Equal(IntegrationStatus.Pending, persisted.IntegrationStatus);
        Assert.Null(persisted.LastIntegrationAttempt);
        Assert.Empty(handler.Requests);
        Assert.Empty(db.WmcExportAudits);
    }

    [Fact]
    public async Task Concurrent_automatic_json_workers_claim_a_pending_order_for_only_one_transmission()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"automatic-json-order-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        }.ToString();
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connectionString).Options;
        int orderId;
        await using (var setup = new ApplicationDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            var order = CreateApprovedJsonOrder("ORO-2026-000790");
            setup.Add(order);
            await setup.SaveChangesAsync();
            orderId = order.Id;
        }

        await using var firstDb = new ApplicationDbContext(options);
        await using var secondDb = new ApplicationDbContext(options);
        var handler = new AutomaticJsonHandler();
        var (firstService, firstIntegration) = CreateAutomaticJsonService(firstDb, handler);
        var (secondService, secondIntegration) = CreateAutomaticJsonService(secondDb, handler);
        var results = await Task.WhenAll(
            firstService.SendAutomaticJsonAsync(orderId, firstIntegration),
            secondService.SendAutomaticJsonAsync(orderId, secondIntegration));

        Assert.Single(results, result => result?.Succeeded == true);
        Assert.Single(results, result => result?.AtomicReservationRejected == true);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Single(firstDb.WmcExportAudits);
    }

    [Fact]
    public async Task Automatic_json_worker_does_not_send_an_order_with_a_generated_txt_artifact()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateApprovedJsonOrder("ORO-2026-000786");
        order.WmcOrderExportState = new WmcOrderExportState { Status = WmcOrderExportStatus.Exported };
        db.Add(order);
        await db.SaveChangesAsync();
        var handler = new QueuedJsonHandler();
        var (service, jsonIntegration) = CreateAutomaticJsonService(db, handler);

        await service.SendAutomaticJsonAsync(order.Id, jsonIntegration);

        Assert.Empty(handler.Requests);
        Assert.Equal(IntegrationStatus.Pending, order.IntegrationStatus);
        Assert.Equal(WmcOrderExportStatus.Exported, order.WmcOrderExportState!.Status);
    }

    [Theory]
    [InlineData(WmcExportOutcome.Started)]
    [InlineData(WmcExportOutcome.Unknown)]
    [InlineData(WmcExportOutcome.Succeeded)]
    [InlineData(WmcExportOutcome.Imported)]
    [InlineData(WmcExportOutcome.Generated)]
    [InlineData(WmcExportOutcome.Available)]
    [InlineData(WmcExportOutcome.Regenerated)]
    public async Task Automatic_json_worker_does_not_reclaim_started_uncertain_or_succeeded_attempts(WmcExportOutcome outcome)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateApprovedJsonOrder($"ORO-2026-{(int)outcome:000000}");
        order.WmcOrderExportState = new WmcOrderExportState { Status = WmcOrderExportStatus.Pending };
        order.WmcExportAudits.Add(new WmcExportAudit
        {
            AttemptId = Guid.NewGuid(),
            Source = WmcExportSource.Automatic,
            Outcome = outcome,
            Succeeded = outcome == WmcExportOutcome.Succeeded,
            ExportedAt = DateTime.UtcNow
        });
        db.Add(order);
        await db.SaveChangesAsync();
        var handler = new QueuedJsonHandler();
        var (service, jsonIntegration) = CreateAutomaticJsonService(db, handler);

        await service.SendAutomaticJsonAsync(order.Id, jsonIntegration);

        Assert.Empty(handler.Requests);
        Assert.Equal(IntegrationStatus.Pending, order.IntegrationStatus);
        Assert.Single(db.WmcExportAudits);
    }

    [Fact]
    public async Task Automatic_json_worker_never_reexports_after_an_ambiguous_result_even_if_status_is_reset()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateApprovedJsonOrder("ORO-2026-000787");
        db.Add(order);
        await db.SaveChangesAsync();
        var handler = new QueuedJsonHandler(
            (HttpStatusCode.ServiceUnavailable, "importado com sucesso"),
            (HttpStatusCode.ServiceUnavailable, "consulta ainda indisponivel"));
        var (service, jsonIntegration) = CreateAutomaticJsonService(db, handler);

        await service.SendAutomaticJsonAsync(order.Id, jsonIntegration);

        var persisted = await db.Orders.SingleAsync(item => item.Id == order.Id);
        Assert.Equal(IntegrationStatus.Unknown, persisted.IntegrationStatus);
        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith("/exportar.php", handler.Requests[0].AbsolutePath, StringComparison.Ordinal);
        Assert.EndsWith("/nropedido.php", handler.Requests[1].AbsolutePath, StringComparison.Ordinal);
        Assert.Equal(WmcExportOutcome.Unknown, (await db.WmcExportAudits.SingleAsync()).Outcome);

        persisted.IntegrationStatus = IntegrationStatus.Pending;
        await db.SaveChangesAsync();
        await service.SendAutomaticJsonAsync(order.Id, jsonIntegration);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Single(db.WmcExportAudits);
        Assert.Equal(IntegrationStatus.Pending, persisted.IntegrationStatus);
    }

    [Fact]
    public async Task Ambiguous_json_export_is_reconciled_by_nropedido_without_a_second_export()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateApprovedJsonOrder("ORO-2026-000789");
        db.Add(order);
        await db.SaveChangesAsync();
        var handler = new QueuedJsonHandler(
            (HttpStatusCode.ServiceUnavailable, "gateway timeout"),
            (HttpStatusCode.OK, "[{\"NROPEDIDO\":776656}]"));
        var (service, jsonIntegration) = CreateAutomaticJsonService(db, handler);

        await service.SendAutomaticJsonAsync(order.Id, jsonIntegration);

        var persisted = await db.Orders.Include(item => item.WmcOrderExportState).SingleAsync(item => item.Id == order.Id);
        Assert.Equal(IntegrationStatus.Succeeded, persisted.IntegrationStatus);
        Assert.Equal("776656", persisted.ErpOrderNumber);
        Assert.Equal(WmcOrderExportStatus.Imported, persisted.WmcOrderExportState!.Status);
        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith("/exportar.php", handler.Requests[0].AbsolutePath, StringComparison.Ordinal);
        Assert.EndsWith("/nropedido.php", handler.Requests[1].AbsolutePath, StringComparison.Ordinal);
        var audit = await db.WmcExportAudits.SingleAsync();
        Assert.Equal(WmcExportOutcome.Unknown, audit.Outcome);
    }

    [Fact]
    public async Task Confirmed_json_import_reconciles_the_wmc_number_without_repeating_exportar()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateApprovedJsonOrder("ORO-2026-000788");
        db.Add(order);
        await db.SaveChangesAsync();
        var handler = new QueuedJsonHandler(
            (HttpStatusCode.OK, "importado com sucesso"),
            (HttpStatusCode.ServiceUnavailable, "consulta temporariamente indisponível"),
            (HttpStatusCode.OK, "[{\"NROPEDIDO\":776655}]"));
        var (service, jsonIntegration) = CreateAutomaticJsonService(db, handler);

        await service.SendAutomaticJsonAsync(order.Id, jsonIntegration);

        var persisted = await db.Orders.Include(item => item.WmcOrderExportState).SingleAsync(item => item.Id == order.Id);
        Assert.Equal(IntegrationStatus.Succeeded, persisted.IntegrationStatus);
        Assert.Equal(WmcOrderExportStatus.Imported, persisted.WmcOrderExportState!.Status);
        Assert.Null(persisted.ErpOrderNumber);
        Assert.Equal(2, handler.Requests.Count);

        var reconciliation = await jsonIntegration.ReconcileNroPedidoAsync(order.Id);

        Assert.True(reconciliation.Recovered);
        Assert.Equal("776655", reconciliation.ErpOrderNumber);
        Assert.Equal(3, handler.Requests.Count);
        Assert.EndsWith("/exportar.php", handler.Requests[0].AbsolutePath, StringComparison.Ordinal);
        Assert.EndsWith("/nropedido.php", handler.Requests[1].AbsolutePath, StringComparison.Ordinal);
        Assert.EndsWith("/nropedido.php", handler.Requests[2].AbsolutePath, StringComparison.Ordinal);
    }

    private static Order CreateOrderWithWmcState(
        string orderNumber,
        IntegrationStatus integrationStatus,
        WmcOrderExportStatus wmcStatus)
    {
        var userId = $"creator-{Guid.NewGuid():N}";
        return new Order
        {
            Number = orderNumber,
            Customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" },
            CreatedByUser = new ApplicationUser
            {
                Id = userId,
                UserName = $"{userId}@test.local",
                NormalizedUserName = $"{userId}@TEST.LOCAL",
                Email = $"{userId}@test.local",
                NormalizedEmail = $"{userId}@TEST.LOCAL"
            },
            Status = OrderStatus.Approved,
            IntegrationStatus = integrationStatus,
            WmcOrderExportState = new WmcOrderExportState { Status = wmcStatus }
        };
    }

    private static Order CreateApprovedJsonOrder(string orderNumber) => new()
    {
        Number = orderNumber,
        Status = OrderStatus.Approved,
        Customer = new Customer
        {
            LegalName = "Cliente teste",
            TradeName = "Cliente teste",
            Cnpj = "00000000000000",
            WmcCode = "107072",
            SalesRepresentativeId = 27,
            SalesRepresentative = new SalesRepresentative { Id = 27, Name = "Vendedor teste", Email = "seller@example.invalid" }
        },
        CreatedByUser = TestDbContextFactory.CreateOrderCreator(),
        PaymentTerm = new PaymentTerm { Code = "CASH", Name = "A vista", DaysUntilDue = 0 },
        Items =
        [
            new OrderItem
            {
                Product = new Product
                {
                    Sku = "TEST-1",
                    WmcCode = "610601552",
                    Name = "Produto teste",
                    Unit = "caixa",
                    Brand = "Teste",
                    ProductCategory = new ProductCategory { Name = "Teste", Slug = "teste" }
                },
                ProductNameSnapshot = "Produto teste",
                Quantity = 1,
                UnitPrice = 10m,
                Subtotal = 10m
            }
        ]
    };

    private static (OrderIntegrationService Service, WmcJsonOrderErpIntegration JsonIntegration) CreateAutomaticJsonService(
        ApplicationDbContext db,
        HttpMessageHandler handler,
        ILogger<OrderIntegrationService>? logger = null,
        int? serviceCutoff = 0,
        int? transmitterCutoff = null)
    {
        var jsonOptions = CreateAutomaticJsonOptions();
        jsonOptions.AutomaticWorkerStartAfterOrderId = transmitterCutoff ?? serviceCutoff;
        var jsonIntegration = new WmcJsonOrderErpIntegration(
            new HttpClient(handler),
            Options.Create(jsonOptions),
            new WmcMobileOrderPayloadBuilder(),
            new WmcExportAuditService(db),
            new FixedTransmissionIdAllocator());
        var serviceOptions = CreateAutomaticJsonOptions();
        serviceOptions.AutomaticWorkerStartAfterOrderId = serviceCutoff;
        return (new OrderIntegrationService(
            db,
            new UnusedAutomaticJsonIntegration(),
            logger: logger,
            jsonOrderOptions: Options.Create(serviceOptions)), jsonIntegration);
    }

    private static WmcJsonOrderOptions CreateAutomaticJsonOptions() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WmcJsonOrder:Enabled"] = "true",
            ["WmcJsonOrder:AutomaticWorkerEnabled"] = "true",
            ["WmcJsonOrder:AutomaticWorkerStartAfterOrderId"] = "0",
            ["WmcJsonOrder:BaseUrl"] = "https://wmc.example.invalid/",
            ["WmcJsonOrder:DeviceId"] = "device-test",
            ["WmcJsonOrder:SalesRepresentativeCodes:27"] = "804",
            ["WmcJsonOrder:SalesRepresentativeSerials:27"] = "serial-test"
        })
        .Build()
        .GetSection(WmcJsonOrderOptions.SectionName)
        .Get<WmcJsonOrderOptions>()!;

    private sealed class CountingErpOrderIntegration : IErpOrderIntegration
    {
        private int sendCount;
        public int SendCount => Volatile.Read(ref sendCount);

        public Task<ErpOrderResult> SendOrderAsync(Order order, CancellationToken cancellationToken = default, Guid? attemptId = null)
        {
            Interlocked.Increment(ref sendCount);
            return Task.FromResult(new ErpOrderResult(true, $"ERP-{order.Number}"));
        }
    }

    private sealed class UnusedAutomaticJsonIntegration : IErpOrderIntegration
    {
        public Task<ErpOrderResult> SendOrderAsync(Order order, CancellationToken cancellationToken = default, Guid? attemptId = null) =>
            throw new InvalidOperationException("The automatic JSON path must use its dedicated integration method.");
    }

    private sealed class CapturingErpOrderIntegration : IErpOrderIntegration
    {
        public bool ReceivedMappedOrder { get; private set; }
        public int SendCount { get; private set; }
        public Guid? AttemptId { get; private set; }

        public Task<ErpOrderResult> SendOrderAsync(Order order, CancellationToken cancellationToken = default, Guid? attemptId = null)
        {
            SendCount++;
            AttemptId = attemptId;
            ReceivedMappedOrder = order.Customer?.WmcCode == "107072" && order.Items.Single().Product?.WmcCode == "610601552";
            return Task.FromResult(new ErpOrderResult(true, "WMC_ORO2026000777.txt"));
        }
    }

    private sealed class ThrowingErpOrderIntegration : IErpOrderIntegration
    {
        public Task<ErpOrderResult> SendOrderAsync(Order order, CancellationToken cancellationToken = default, Guid? attemptId = null) =>
            throw new IOException("Diretorio indisponivel.");
    }

    private sealed class AutomaticJsonHandler : HttpMessageHandler
    {
        private readonly object sync = new();
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (sync)
            {
                Requests.Add(request.RequestUri!);
            }
            var body = request.RequestUri!.AbsolutePath.EndsWith("/exportar.php", StringComparison.Ordinal)
                ? "importado com sucesso"
                : "[{\"NROPEDIDO\":776655}]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            });
        }
    }

    private sealed class QueuedJsonHandler(params (HttpStatusCode StatusCode, string ResponseBody)[] responses) : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode StatusCode, string ResponseBody)> responses = new(responses);

        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            if (!responses.TryDequeue(out var response))
            {
                throw new InvalidOperationException("Unexpected HTTP request in test.");
            }

            return Task.FromResult(new HttpResponseMessage(response.StatusCode)
            {
                Content = new StringContent(response.ResponseBody)
            });
        }
    }

    private sealed class FixedTransmissionIdAllocator : IWmcTransmissionIdAllocator
    {
        public Task<int> GetOrAllocateAsync(Order order, CancellationToken cancellationToken = default)
        {
            order.WmcTransmissionId = 9001;
            return Task.FromResult(9001);
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public List<Exception?> Exceptions { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            Exceptions.Add(exception);
        }
    }
}
