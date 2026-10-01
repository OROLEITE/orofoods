using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Services.Customers;

namespace Orofoods.Web.Services.Payments;

public sealed class PointPaymentOrchestrationService(
    ApplicationDbContext db,
    IPointPaymentProvider provider,
    IPaymentTerminalEligibilityService terminalEligibility,
    IOptions<MercadoPagoPointOptions> pointOptions,
    IOptions<PaymentEligibilityOptions> paymentOptions,
    IHostEnvironment hostEnvironment,
    IPaymentApprovalHandler approvalHandler,
    TimeProvider timeProvider,
    PointPaymentAttemptGate attemptGate,
    IPointPaymentOrderConcurrencyLock orderConcurrencyLock) : IPointPaymentOrchestrationService
{
    private const string PointGateway = "MercadoPagoPoint";
    private const string VirtualDeviceId = "SBX0000001";
    private static readonly PaymentStatus[] ActiveStatuses =
    [PaymentStatus.Pending, PaymentStatus.Processing, PaymentStatus.ActionRequired];

    public async Task<PointPaymentOperationResult> StartChargeAsync(
        int orderId,
        int assignmentId,
        string requestKey,
        CancellationToken cancellationToken = default,
        string? adminUserId = null)
    {
        var unavailable = ValidateEnabledEnvironment();
        if (unavailable is not null) return unavailable;
        if (!Guid.TryParse(requestKey, out var requestGuid))
        {
            return PointPaymentOperationResult.Failure("POINT_REQUEST_KEY_INVALID", "Atualize a página e tente novamente.");
        }

        var providerKey = $"point-{orderId}-{requestGuid:N}";
        Payment payment;
        DriverPaymentTerminalAssignment assignment;
        var shouldCreateOrder = false;
        PaymentStatus? statusBeforeStart = null;
        try
        {
            using (await attemptGate.AcquireAsync(orderId, cancellationToken))
            await using (var transaction = await db.Database.BeginTransactionAsync(orderConcurrencyLock.TransactionIsolationLevel, cancellationToken))
            {
                if (!await orderConcurrencyLock.AcquireAsync(orderId, cancellationToken))
                {
                    return PointPaymentOperationResult.Failure("POINT_ORDER_NOT_FOUND", "Pedido não encontrado.");
                }
                var order = await db.Orders
                    .Include(x => x.PaymentTerm)
                    .Include(x => x.Payments)
                    .SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken);
                if (order is null) return PointPaymentOperationResult.Failure("POINT_ORDER_NOT_FOUND", "Pedido não encontrado.");
                if (order.PaymentTerm?.Code != "CARD_ON_DELIVERY")
                {
                    return PointPaymentOperationResult.Failure("POINT_ORDER_METHOD_INVALID", "Este pedido não utiliza Cartão na Entrega.");
                }
                if (order.Status is OrderStatus.Delivered or OrderStatus.Cancelled)
                {
                    return PointPaymentOperationResult.Failure("POINT_ORDER_STATE_INVALID", "Este pedido não aceita uma nova cobrança.");
                }
                if (order.Total <= 0m)
                {
                    return PointPaymentOperationResult.Failure("POINT_AMOUNT_INVALID", "O valor do pedido não permite cobrança.");
                }

                var approvedPayment = order.Payments
                    .Where(x => x.Method == PaymentMethodType.CardOnDelivery)
                    .FirstOrDefault(x => x.Status is PaymentStatus.Approved or PaymentStatus.Paid);
                if (approvedPayment is not null)
                {
                    return PointPaymentOperationResult.Failure("POINT_PAYMENT_ALREADY_APPROVED", "O pagamento deste pedido já foi aprovado.");
                }

                var selectedAssignment = await db.DriverPaymentTerminalAssignments
                    .Include(x => x.Driver)
                    .Include(x => x.PaymentTerminal)
                    .SingleOrDefaultAsync(x => x.Id == assignmentId, cancellationToken);
                if (selectedAssignment is null || !terminalEligibility.CanBeUsedForPointPayment(selectedAssignment)
                    || selectedAssignment.PaymentTerminal?.Provider != PaymentTerminalProvider.MercadoPago
                    || !string.Equals(selectedAssignment.PaymentTerminal.DeviceId, VirtualDeviceId, StringComparison.Ordinal))
                {
                    return PointPaymentOperationResult.Failure("POINT_ASSIGNMENT_INVALID", "Selecione um motorista e terminal de teste ativos.");
                }
                assignment = selectedAssignment;

                var requestAttempt = await db.Payments.SingleOrDefaultAsync(
                    x => x.OrderId == orderId && x.Method == PaymentMethodType.CardOnDelivery && x.IdempotencyKey == providerKey,
                    cancellationToken);
                if (requestAttempt is not null)
                {
                    payment = requestAttempt;
                    statusBeforeStart = payment.Status;
                    if (payment.Amount != order.Total || payment.DriverPaymentTerminalAssignmentId != assignmentId)
                    {
                        return PointPaymentOperationResult.Failure("POINT_ATTEMPT_CONFLICT", "A tentativa existente não corresponde ao pedido ou terminal atual.");
                    }
                    shouldCreateOrder = payment.GatewayOrderId is null;
                }
                else
                {
                    var activeAttempt = order.Payments
                        .Where(x => x.Method == PaymentMethodType.CardOnDelivery && ActiveStatuses.Contains(x.Status))
                        .OrderByDescending(x => x.CreatedAt)
                        .ThenByDescending(x => x.Id)
                        .FirstOrDefault();

                    if (activeAttempt is not null)
                    {
                        statusBeforeStart = activeAttempt.Status;
                        if (activeAttempt.Gateway is not null && activeAttempt.Gateway != PointGateway)
                        {
                            return PointPaymentOperationResult.Failure("POINT_ACTIVE_ATTEMPT_CONFLICT", "A tentativa ativa pertence a outro meio de pagamento.");
                        }
                        if (activeAttempt.DriverPaymentTerminalAssignmentId is not null
                            && activeAttempt.DriverPaymentTerminalAssignmentId != assignmentId)
                        {
                            return PointPaymentOperationResult.Failure("POINT_ACTIVE_ATTEMPT_CONFLICT", "A cobrança ativa está vinculada a outro terminal.");
                        }
                        if (activeAttempt.IdempotencyKey is null)
                        {
                            activeAttempt.IdempotencyKey = providerKey;
                            activeAttempt.ExternalReference = ExternalReference(orderId, activeAttempt.Id);
                            activeAttempt.DriverPaymentTerminalAssignmentId = assignmentId;
                            activeAttempt.Gateway = PointGateway;
                            activeAttempt.Amount = order.Total;
                            activeAttempt.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
                        }
                        activeAttempt.ExternalReference ??= ExternalReference(orderId, activeAttempt.Id);
                        payment = activeAttempt;
                    }
                    else
                    {
                        payment = new Payment
                        {
                            OrderId = order.Id,
                            CustomerId = order.CustomerId,
                            PaymentMethod = "CARD_ON_DELIVERY",
                            Method = PaymentMethodType.CardOnDelivery,
                            Amount = order.Total,
                            Status = PaymentStatus.Pending,
                            Gateway = PointGateway,
                            DriverPaymentTerminalAssignmentId = assignmentId,
                            IdempotencyKey = providerKey,
                            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
                            UpdatedAt = timeProvider.GetUtcNow().UtcDateTime
                        };
                        order.Payments.Add(payment);
                        statusBeforeStart = null;
                        await db.SaveChangesAsync(cancellationToken);
                        payment.ExternalReference = ExternalReference(orderId, payment.Id);
                    }

                    if (payment.Amount != order.Total)
                    {
                        return PointPaymentOperationResult.Failure("POINT_AMOUNT_CHANGED", "O valor do pedido mudou. Atualize a tela antes de cobrar.");
                    }
                    payment.DriverPaymentTerminalAssignmentId = assignmentId;
                    payment.Gateway = PointGateway;
                    payment.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
                    await db.SaveChangesAsync(cancellationToken);
                    await AddAuditEventAsync(payment, assignment.Id, adminUserId, statusBeforeStart, payment.Status,
                        "POINT_CHARGE_REQUESTED", "Cobrança solicitada no terminal de teste.", cancellationToken);
                    shouldCreateOrder = payment.GatewayOrderId is null;
                }
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (Exception exception) when (DatabaseWriteConflict.IsExpected(exception))
        {
            return PointPaymentOperationResult.Failure("POINT_CHARGE_CONFLICT", "Outra operação iniciou a cobrança. Atualize o pedido e confira o status.");
        }

        try
        {
            var pointRequest = new PointPaymentRequest(
                orderId,
                payment.Id,
                assignment.Id,
                payment.Amount,
                assignment.PaymentTerminal!.DeviceId!,
                assignment.PaymentTerminal.StoreId,
                assignment.PaymentTerminal.PosId,
                payment.IdempotencyKey!,
                payment.ExternalReference);
            var result = shouldCreateOrder
                ? await provider.CreateTerminalPaymentAsync(pointRequest, cancellationToken)
                : await provider.GetPaymentStatusAsync(payment.GatewayOrderId!, cancellationToken);
            var applied = await ApplyProviderResultWithGateAsync(payment.Id, orderId, result, cancellationToken, adminUserId);
            if (!applied.Succeeded && applied.Payment is not null)
            {
                await RecordSanitizedFailureAsync(applied.Payment, adminUserId, applied.ErrorCode ?? "POINT_RECONCILIATION_FAILED",
                    applied.ErrorMessage ?? "A resposta Point não foi conciliada.", cancellationToken);
            }
            return applied.Succeeded
                ? applied
                : PointPaymentOperationResult.Failure(applied.ErrorCode!, applied.ErrorMessage!, applied.Payment);
        }
        catch (Exception exception) when (exception is PaymentGatewayException or HttpRequestException or TimeoutException)
        {
            await RecordSanitizedFailureAsync(payment, adminUserId, "POINT_GATEWAY_UNAVAILABLE", "A comunicação com o terminal expirou ou falhou.", cancellationToken);
            return PointPaymentOperationResult.Failure("POINT_GATEWAY_UNAVAILABLE", "Não foi possível confirmar a cobrança. A tentativa foi preservada para consulta segura.", payment);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await RecordSanitizedFailureAsync(payment, adminUserId, "POINT_GATEWAY_UNAVAILABLE", "A comunicação com o terminal expirou ou falhou.", cancellationToken);
            return PointPaymentOperationResult.Failure("POINT_GATEWAY_UNAVAILABLE", "A comunicação com o terminal expirou. A tentativa foi preservada para consulta segura.", payment);
        }
    }

    public async Task<PointPaymentOperationResult> RefreshAsync(int paymentId, CancellationToken cancellationToken = default, string? adminUserId = null)
    {
        var unavailable = ValidateEnabledEnvironment();
        if (unavailable is not null) return unavailable;
        var payment = await db.Payments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == paymentId && x.Method == PaymentMethodType.CardOnDelivery && x.Gateway == PointGateway, cancellationToken);
        if (payment?.GatewayOrderId is null) return PointPaymentOperationResult.Failure("POINT_ORDER_NOT_CREATED", "A cobrança ainda não possui uma order confirmada.", payment);

        try
        {
            var result = await provider.GetPaymentStatusAsync(payment.GatewayOrderId, cancellationToken);
            var applied = await ApplyProviderResultWithGateAsync(payment.Id, payment.OrderId, result, cancellationToken, adminUserId);
            if (!applied.Succeeded && applied.Payment is not null)
            {
                await RecordSanitizedFailureAsync(applied.Payment, adminUserId, applied.ErrorCode ?? "POINT_RECONCILIATION_FAILED",
                    applied.ErrorMessage ?? "A resposta Point não foi conciliada.", cancellationToken);
            }
            return applied;
        }
        catch (Exception exception) when (exception is PaymentGatewayException or HttpRequestException or TimeoutException)
        {
            await RecordSanitizedFailureAsync(payment, adminUserId, "POINT_GATEWAY_UNAVAILABLE", "Não foi possível consultar o status no terminal virtual.", cancellationToken);
            return PointPaymentOperationResult.Failure("POINT_GATEWAY_UNAVAILABLE", "Não foi possível consultar o status no Mercado Pago.", payment);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await RecordSanitizedFailureAsync(payment, adminUserId, "POINT_GATEWAY_UNAVAILABLE", "A consulta ao terminal virtual expirou.", cancellationToken);
            return PointPaymentOperationResult.Failure("POINT_GATEWAY_UNAVAILABLE", "A consulta do status no Mercado Pago expirou.", payment);
        }
    }

    public async Task<PointPaymentOperationResult> CancelAsync(int paymentId, CancellationToken cancellationToken = default, string? adminUserId = null)
    {
        var unavailable = ValidateEnabledEnvironment();
        if (unavailable is not null) return unavailable;
        var payment = await db.Payments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == paymentId && x.Method == PaymentMethodType.CardOnDelivery && x.Gateway == PointGateway, cancellationToken);
        if (payment?.GatewayOrderId is null) return PointPaymentOperationResult.Failure("POINT_ORDER_NOT_CREATED", "A cobrança ainda não possui uma order para cancelar.", payment);
        if (!ActiveStatuses.Contains(payment.Status)) return PointPaymentOperationResult.Failure("POINT_ORDER_STATE_INVALID", "Esta cobrança não está aguardando conclusão.", payment);

        try
        {
            await provider.CancelPendingPaymentAsync(payment.GatewayOrderId, cancellationToken);
            return await RefreshAsync(payment.Id, cancellationToken, adminUserId);
        }
        catch (Exception exception) when (exception is PaymentGatewayException or HttpRequestException or TimeoutException)
        {
            await RecordSanitizedFailureAsync(payment, adminUserId, "POINT_CANCEL_REQUEST_FAILED", "Não foi possível confirmar o cancelamento no terminal virtual.", cancellationToken);
            return PointPaymentOperationResult.Failure("POINT_GATEWAY_UNAVAILABLE", "Não foi possível solicitar o cancelamento no Mercado Pago.", payment);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await RecordSanitizedFailureAsync(payment, adminUserId, "POINT_CANCEL_REQUEST_FAILED", "A solicitação de cancelamento expirou no terminal virtual.", cancellationToken);
            return PointPaymentOperationResult.Failure("POINT_GATEWAY_UNAVAILABLE", "A solicitação de cancelamento expirou.", payment);
        }
    }

    public async Task<IReadOnlyList<DriverPaymentTerminalAssignment>> GetEligibleAssignmentsAsync(CancellationToken cancellationToken = default) =>
        await db.DriverPaymentTerminalAssignments.AsNoTracking()
            .Include(x => x.Driver)
            .Include(x => x.PaymentTerminal)
            .Where(x => x.EndedAt == null && x.Driver!.IsActive && x.PaymentTerminal!.IsActive
                && x.PaymentTerminal.Provider == PaymentTerminalProvider.MercadoPago
                && x.PaymentTerminal.DeviceId == VirtualDeviceId)
            .OrderBy(x => x.Driver!.Name)
            .ToListAsync(cancellationToken);

    private PointPaymentOperationResult? ValidateEnabledEnvironment()
    {
        if (!pointOptions.Value.Enabled || !paymentOptions.Value.CardOnDeliveryEnabled)
        {
            return PointPaymentOperationResult.Failure("POINT_INTEGRATION_DISABLED", "A cobrança Point está desabilitada.");
        }
        if (!hostEnvironment.IsEnvironment("Test") || !string.Equals(pointOptions.Value.Environment, "Test", StringComparison.OrdinalIgnoreCase))
        {
            return PointPaymentOperationResult.Failure("POINT_TEST_ENVIRONMENT_REQUIRED", "A cobrança Point só está disponível no ambiente Test.");
        }
        if (string.IsNullOrWhiteSpace(pointOptions.Value.AccessToken))
        {
            return PointPaymentOperationResult.Failure("POINT_TEST_CREDENTIALS_REQUIRED", "As credenciais de teste Point não estão configuradas.");
        }
        if (string.IsNullOrWhiteSpace(pointOptions.Value.PoiType)
            || pointOptions.Value.PoiType.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
        {
            return PointPaymentOperationResult.Failure("POINT_TEST_TERMINAL_CONFIGURATION_INVALID", "A configuração do terminal virtual está inválida.");
        }
        return null;
    }

    private async Task<PointPaymentOperationResult> ApplyProviderResultAsync(int paymentId, int orderId, PointPaymentResult result, CancellationToken cancellationToken, string? adminUserId)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(orderConcurrencyLock.TransactionIsolationLevel, cancellationToken);
        if (!await orderConcurrencyLock.AcquireAsync(orderId, cancellationToken))
        {
            return PointPaymentOperationResult.Failure("POINT_ATTEMPT_NOT_FOUND", "Tentativa de cobrança não encontrada.");
        }
        var payment = await db.Payments.Include(x => x.Order)
            .SingleOrDefaultAsync(x => x.Id == paymentId && x.Method == PaymentMethodType.CardOnDelivery && x.Gateway == PointGateway, cancellationToken);
        if (payment is null) return PointPaymentOperationResult.Failure("POINT_ATTEMPT_NOT_FOUND", "Tentativa de cobrança não encontrada.");

        // StartChargeAsync can retain this Payment in the request-scoped identity map across provider I/O.
        // Another scope may commit a newer status while that request waits, so refresh tracked state only
        // after acquiring the order lock and before evaluating a status transition.
        var paymentEntry = db.Entry(payment);
        await paymentEntry.ReloadAsync(cancellationToken);
        if (paymentEntry.State == EntityState.Detached)
        {
            return PointPaymentOperationResult.Failure("POINT_ATTEMPT_NOT_FOUND", "Tentativa de cobrança não encontrada.");
        }
        if (payment.Order is not null)
        {
            await db.Entry(payment.Order).ReloadAsync(cancellationToken);
        }
        if (string.IsNullOrWhiteSpace(result.GatewayOrderId))
        {
            return PointPaymentOperationResult.Failure("POINT_ORDER_ID_MISSING", "O provedor não retornou o identificador da cobrança.", payment);
        }
        if (payment.GatewayOrderId is not null
            && !string.Equals(result.GatewayOrderId, payment.GatewayOrderId, StringComparison.Ordinal))
        {
            return PointPaymentOperationResult.Failure("POINT_ORDER_ID_MISMATCH", "O identificador retornado não corresponde à cobrança local.", payment);
        }
        if (string.IsNullOrWhiteSpace(result.ExternalReference))
        {
            return PointPaymentOperationResult.Failure("POINT_REFERENCE_MISSING", "O provedor não retornou a referência externa da cobrança.", payment);
        }
        if (!string.Equals(result.ExternalReference, payment.ExternalReference, StringComparison.Ordinal))
        {
            return PointPaymentOperationResult.Failure("POINT_REFERENCE_MISMATCH", "A referência retornada não corresponde ao pedido.", payment);
        }
        // A successful create response can contain the order and transaction identifiers before
        // it contains the aggregate amount. Preserve those identifiers once the reference matches
        // so a later GET can reconcile the same order instead of creating it again.
        var gatewayOrderIdWasMissing = payment.GatewayOrderId is null;
        var gatewayPaymentIdWasMissing = payment.GatewayPaymentId is null;
        payment.GatewayOrderId ??= result.GatewayOrderId;
        payment.GatewayPaymentId ??= result.GatewayPaymentId;
        if (result.TotalAmount is null)
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PointPaymentOperationResult.Failure("POINT_AMOUNT_MISSING", "O provedor não retornou o valor da cobrança.", payment);
        }
        if (result.TotalAmount != payment.Amount)
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PointPaymentOperationResult.Failure("POINT_AMOUNT_MISMATCH", "O valor retornado não corresponde ao pedido.", payment);
        }

        if (result.PaymentStatus is null)
        {
            payment.GatewayOrderId ??= result.GatewayOrderId;
            payment.GatewayPaymentId ??= result.GatewayPaymentId;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PointPaymentOperationResult.Failure(result.ErrorCode, result.Message, payment);
        }
        if (result.PaymentStatus == PaymentStatus.Approved)
        {
            var validationFailure = ValidateApprovedResult(result, payment.Amount);
            if (validationFailure is not null)
            {
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return PointPaymentOperationResult.Failure(validationFailure.Value.Code, validationFailure.Value.Message, payment);
            }
        }

        if (!IsForwardTransition(payment.Status, result.PaymentStatus.Value))
        {
            // External identifiers may have been learned even when the mapped status is
            // unchanged or stale; persist those before returning to keep retries idempotent.
            if ((gatewayOrderIdWasMissing && payment.GatewayOrderId is not null)
                || (gatewayPaymentIdWasMissing && payment.GatewayPaymentId is not null))
            {
                await AddAuditEventAsync(payment, payment.DriverPaymentTerminalAssignmentId, adminUserId, payment.Status, payment.Status,
                    "POINT_ORDER_IDENTIFIERS_RECEIVED", "O identificador externo da cobrança foi recebido.", cancellationToken);
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PointPaymentOperationResult.Success(payment);
        }

        if (result.PaymentStatus == PaymentStatus.Approved && payment.PaidAt is null)
        {
            payment.PaidAt = timeProvider.GetUtcNow().UtcDateTime;
            await approvalHandler.PaymentApprovedAsync(payment, cancellationToken);
        }
        if (result.PaymentStatus == PaymentStatus.Refunded && payment.Status is not (PaymentStatus.Approved or PaymentStatus.Paid))
        {
            return PointPaymentOperationResult.Failure("POINT_REFUND_STATE_INVALID", "O estorno não corresponde a um pagamento aprovado.", payment);
        }
        var previousStatus = payment.Status;
        payment.Status = result.PaymentStatus.Value;
        payment.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        if (previousStatus != payment.Status)
        {
            await AddAuditEventAsync(payment, payment.DriverPaymentTerminalAssignmentId, adminUserId, previousStatus, payment.Status,
                "POINT_STATUS_RECONCILED", "Status de pagamento conciliado com o terminal virtual.", cancellationToken);
        }
        else if ((gatewayOrderIdWasMissing && payment.GatewayOrderId is not null)
            || (gatewayPaymentIdWasMissing && payment.GatewayPaymentId is not null))
        {
            await AddAuditEventAsync(payment, payment.DriverPaymentTerminalAssignmentId, adminUserId, previousStatus, payment.Status,
                "POINT_ORDER_IDENTIFIERS_RECEIVED", "O identificador externo da cobrança foi recebido.", cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return PointPaymentOperationResult.Success(payment);
    }

    private static (string Code, string Message)? ValidateApprovedResult(PointPaymentResult result, decimal expectedAmount)
    {
        if (!string.Equals(result.ProviderStatus, "processed", StringComparison.OrdinalIgnoreCase))
        {
            return ("POINT_ORDER_STATUS_INVALID", "A confirmação Point não retornou uma order processada.");
        }
        if (!string.IsNullOrWhiteSpace(result.ProviderStatusDetail)
            && !string.Equals(result.ProviderStatusDetail, "accredited", StringComparison.OrdinalIgnoreCase))
        {
            return ("POINT_STATUS_DETAIL_INVALID", "O detalhe do pagamento Point não confirma a acreditação.");
        }

        var financials = result.Financials;
        if (financials is null)
        {
            return ("POINT_TRANSACTION_MISSING", "A confirmação Point não contém os dados da transação de pagamento.");
        }
        if (financials.TotalPaidAmountInvalid
            || (financials.TotalPaidAmountPresent && financials.TotalPaidAmount != expectedAmount))
        {
            return ("POINT_TOTAL_PAID_AMOUNT_MISMATCH", "O valor pago da order Point não corresponde ao pedido.");
        }

        // The Point Orders API specifies one payment transaction per order. Reject both an
        // absent transaction and unexpected multiplicity instead of selecting an arbitrary row.
        if (financials.Transactions.Count == 0)
        {
            return ("POINT_TRANSACTION_MISSING", "A confirmação Point não contém uma transação de pagamento.");
        }
        if (financials.Transactions.Count != 1)
        {
            return ("POINT_TRANSACTION_AMBIGUOUS", "A confirmação Point contém transações ambíguas.");
        }

        var payment = financials.Transactions[0];
        if (string.IsNullOrWhiteSpace(payment.Id))
        {
            return ("POINT_TRANSACTION_MISSING", "A transação Point não contém um identificador válido.");
        }
        if (!string.Equals(payment.Status, "processed", StringComparison.OrdinalIgnoreCase))
        {
            return ("POINT_TRANSACTION_STATUS_INVALID", "A transação Point não está processada.");
        }
        if (!string.IsNullOrWhiteSpace(payment.StatusDetail)
            && !string.Equals(payment.StatusDetail, "accredited", StringComparison.OrdinalIgnoreCase))
        {
            return ("POINT_TRANSACTION_STATUS_INVALID", "O detalhe da transação Point não confirma a acreditação.");
        }
        if (!payment.AmountPresent || payment.AmountInvalid || payment.Amount != expectedAmount)
        {
            return ("POINT_TRANSACTION_AMOUNT_MISMATCH", "O valor da transação Point não corresponde ao pedido.");
        }
        if (payment.PaidAmountInvalid
            || (payment.PaidAmountPresent && payment.PaidAmount != expectedAmount))
        {
            return ("POINT_PAID_AMOUNT_MISMATCH", "O valor efetivamente pago na transação Point não corresponde ao pedido.");
        }

        return null;
    }

    private async Task<PointPaymentOperationResult> ApplyProviderResultWithGateAsync(
        int paymentId,
        int orderId,
        PointPaymentResult result,
        CancellationToken cancellationToken,
        string? adminUserId)
    {
        using (await attemptGate.AcquireAsync(orderId, cancellationToken))
        {
            try
            {
                return await ApplyProviderResultAsync(paymentId, orderId, result, cancellationToken, adminUserId);
            }
            catch (Exception exception) when (DatabaseWriteConflict.IsExpected(exception))
            {
                return PointPaymentOperationResult.Failure("POINT_RECONCILIATION_CONFLICT", "Outra operação atualizou esta cobrança. Consulte o status novamente.");
            }
        }
    }

    private async Task AddAuditEventAsync(
        Payment payment,
        int? assignmentId,
        string? adminUserId,
        PaymentStatus? previousStatus,
        PaymentStatus newStatus,
        string resultCode,
        string resultSummary,
        CancellationToken cancellationToken)
    {
        var assignment = assignmentId is null
            ? null
            : await db.DriverPaymentTerminalAssignments.AsNoTracking()
                .Where(x => x.Id == assignmentId.Value)
                .Select(x => new { x.DriverId, x.PaymentTerminalId })
                .SingleOrDefaultAsync(cancellationToken);
        var attemptNumber = resultCode == "POINT_CHARGE_REQUESTED"
            ? await db.PointPaymentAuditEvents.CountAsync(x => x.OrderId == payment.OrderId && x.ResultCode == "POINT_CHARGE_REQUESTED", cancellationToken) + 1
            : await db.PointPaymentAuditEvents.Where(x => x.OrderId == payment.OrderId).Select(x => (int?)x.AttemptNumber).MaxAsync(cancellationToken) ?? 1;
        db.PointPaymentAuditEvents.Add(new PointPaymentAuditEvent
        {
            OrderId = payment.OrderId,
            PaymentId = payment.Id,
            DriverId = assignment?.DriverId,
            PaymentTerminalId = assignment?.PaymentTerminalId,
            AssignmentId = assignmentId,
            AdminUserId = adminUserId,
            OccurredAt = timeProvider.GetUtcNow().UtcDateTime,
            ExternalOrderId = payment.GatewayOrderId,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            AttemptNumber = attemptNumber,
            ResultCode = resultCode.Length > 100 ? resultCode[..100] : resultCode,
            ResultSummary = resultSummary.Length > 500 ? resultSummary[..500] : resultSummary
        });
    }

    private async Task RecordSanitizedFailureAsync(Payment payment, string? adminUserId, string code, string summary, CancellationToken cancellationToken)
    {
        try
        {
            await AddAuditEventAsync(payment, payment.DriverPaymentTerminalAssignmentId, adminUserId, payment.Status, payment.Status,
                code, summary, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (DatabaseWriteConflict.IsExpected(exception))
        {
            // Failure to add a secondary audit row must not turn a recoverable Point timeout into HTTP 500.
        }
    }

    private static bool IsForwardTransition(PaymentStatus current, PaymentStatus incoming)
    {
        if (current == incoming) return true;
        if (current is PaymentStatus.Approved or PaymentStatus.Paid) return incoming == PaymentStatus.Refunded;
        if (current is PaymentStatus.Rejected or PaymentStatus.Cancelled or PaymentStatus.Expired or PaymentStatus.Refunded or PaymentStatus.Failed) return false;

        static int Rank(PaymentStatus status) => status switch
        {
            PaymentStatus.Pending => 0,
            PaymentStatus.Processing => 1,
            PaymentStatus.ActionRequired => 2,
            PaymentStatus.Approved or PaymentStatus.Paid or PaymentStatus.Rejected or PaymentStatus.Cancelled or PaymentStatus.Expired or PaymentStatus.Refunded or PaymentStatus.Failed => 3,
            _ => -1
        };
        return Rank(incoming) >= Rank(current);
    }

    private static string ExternalReference(int orderId, int paymentId) => $"oro-order-{orderId}-attempt-{paymentId}";
}
