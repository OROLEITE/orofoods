using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Services.Customers;

namespace Orofoods.Web.Services.Payments;

/// <summary>Coordinates persistence and Mercado Pago Orders API calls for one payment attempt at a time.</summary>
public sealed class PaymentOrchestrationService(
    ApplicationDbContext db,
    IPaymentEligibilityService eligibilityService,
    IPaymentGateway gateway,
    IPaymentApprovalHandler approvalHandler,
    IOptions<MercadoPagoOptions> options,
    TimeProvider timeProvider,
    ILogger<PaymentOrchestrationService> logger)
{
    public async Task<Payment> CreatePixAsync(int orderId, int customerId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var existing = await FindByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existing is not null && existing.GatewayOrderId is not null)
        {
            return existing;
        }

        var (order, customer) = existing is not null
            ? await LoadOwnedOrderAsync(existing.OrderId, existing.CustomerId, cancellationToken)
            : await LoadOwnedOrderAsync(orderId, customerId, cancellationToken);
        await ValidatePaymentTermAsync(customer.Id, order, cancellationToken);

        var payment = existing ?? await CreatePendingAttemptAsync(order, customer.Id, PaymentMethodType.Pix, "PIX", idempotencyKey, cancellationToken);

        var externalReference = payment.ExternalReference!;
        var result = await gateway.CreatePixAsync(
            new CreatePixPaymentRequest(order.Total, externalReference, customer.Email, options.Value.PixExpiration, idempotencyKey),
            cancellationToken);

        await ApplyGatewayResultAsync(payment, result, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return payment;
    }

    public async Task<Payment> CreateCreditCardAsync(
        int orderId,
        int customerId,
        string idempotencyKey,
        string cardToken,
        string paymentMethodId,
        int installments,
        CancellationToken cancellationToken = default)
    {
        var existing = await FindByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existing is not null && existing.GatewayOrderId is not null)
        {
            return existing;
        }

        var (order, customer) = existing is not null
            ? await LoadOwnedOrderAsync(existing.OrderId, existing.CustomerId, cancellationToken)
            : await LoadOwnedOrderAsync(orderId, customerId, cancellationToken);
        await ValidatePaymentTermAsync(customer.Id, order, cancellationToken);

        var payment = existing ?? await CreatePendingAttemptAsync(order, customer.Id, PaymentMethodType.CreditCard, "CREDIT_CARD", idempotencyKey, cancellationToken);

        var externalReference = payment.ExternalReference!;
        var result = await gateway.CreateCreditCardPaymentAsync(
            new CreateCreditCardPaymentRequest(order.Total, externalReference, customer.Email, cardToken, paymentMethodId, installments, idempotencyKey),
            cancellationToken);

        await ApplyGatewayResultAsync(payment, result, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return payment;
    }

    public async Task<Payment?> GetOwnedPaymentAsync(int paymentId, int customerId, CancellationToken cancellationToken = default) =>
        await db.Payments.AsNoTracking()
            .FirstOrDefaultAsync(payment => payment.Id == paymentId && payment.CustomerId == customerId, cancellationToken);

    /// <summary>Re-queries the authoritative Mercado Pago order and applies its status idempotently. Returns false when the notification was ignored as a stale/out-of-order regression.</summary>
    public async Task<bool> ReconcileMercadoPagoOrderAsync(string gatewayOrderId, CancellationToken cancellationToken = default)
    {
        // Serializable isolation prevents two concurrent webhook deliveries for the same order from applying conflicting updates.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var payment = await db.Payments.FirstOrDefaultAsync(payment => payment.GatewayOrderId == gatewayOrderId, cancellationToken)
            ?? throw new InvalidOperationException($"No local payment attempt found for gateway order '{gatewayOrderId}'.");

        var result = await gateway.GetOrderAsync(gatewayOrderId, cancellationToken);

        if (result.ExternalReference is not null && !string.Equals(result.ExternalReference, payment.ExternalReference, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Gateway external reference does not match the local payment attempt.");
        }

        if (result.TotalAmount != payment.Amount)
        {
            throw new InvalidOperationException("Gateway amount does not match the local payment attempt.");
        }

        if (!IsForwardTransition(payment.Status, result.Status))
        {
            logger.LogWarning(
                "Ignored stale Mercado Pago status transition. GatewayOrderId={GatewayOrderId} PaymentId={PaymentId} CurrentStatus={CurrentStatus} IncomingStatus={IncomingStatus}",
                gatewayOrderId, payment.Id, payment.Status, result.Status);
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        ApplyDisplayData(payment, result);

        if (result.Status == PaymentStatus.Approved && payment.PaidAt is null)
        {
            payment.PaidAt = timeProvider.GetUtcNow().UtcDateTime;
            await approvalHandler.PaymentApprovedAsync(payment, cancellationToken);
        }

        payment.Status = result.Status;
        payment.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    /// <summary>Centralizes which status transitions a webhook is allowed to apply, so an old/replayed notification can never regress a payment past a terminal state.</summary>
    private static bool IsForwardTransition(PaymentStatus current, PaymentStatus incoming)
    {
        if (current == incoming)
        {
            return true;
        }

        PaymentStatus[] terminalStates =
        [
            PaymentStatus.Approved, PaymentStatus.Paid, PaymentStatus.Rejected,
            PaymentStatus.Expired, PaymentStatus.Cancelled, PaymentStatus.Refunded, PaymentStatus.Failed
        ];
        if (!terminalStates.Contains(current))
        {
            return true;
        }

        // The only legitimate move out of a settled state is a later refund.
        return current is PaymentStatus.Approved or PaymentStatus.Paid && incoming == PaymentStatus.Refunded;
    }

    private Task<Payment?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        db.Payments.FirstOrDefaultAsync(payment => payment.IdempotencyKey == idempotencyKey, cancellationToken);

    private async Task<(Order Order, Customer Customer)> LoadOwnedOrderAsync(int orderId, int customerId, CancellationToken cancellationToken)
    {
        var order = await db.Orders.Include(order => order.Customer)
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order '{orderId}' was not found.");
        if (order.CustomerId != customerId || order.Customer is null)
        {
            throw new InvalidOperationException("Order does not belong to the requesting customer.");
        }

        return (order, order.Customer);
    }

    private async Task ValidatePaymentTermAsync(int customerId, Order order, CancellationToken cancellationToken)
    {
        if (order.PaymentTermId is null)
        {
            return;
        }

        var validation = await eligibilityService.ValidateAsync(customerId, order.PaymentTermId.Value, cancellationToken);
        if (!validation.IsAllowed)
        {
            throw new InvalidOperationException(validation.ErrorMessage);
        }
    }

    private async Task<Payment> CreatePendingAttemptAsync(
        Order order,
        int customerId,
        PaymentMethodType method,
        string paymentMethodCode,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var payment = new Payment
        {
            OrderId = order.Id,
            CustomerId = customerId,
            PaymentMethod = paymentMethodCode,
            Method = method,
            Amount = order.Total,
            Status = PaymentStatus.Pending,
            Gateway = "MercadoPago",
            ExternalReference = $"orofoods-order-{order.Id}",
            IdempotencyKey = idempotencyKey
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);
        return payment;
    }

    private async Task ApplyGatewayResultAsync(Payment payment, PaymentGatewayOrder result, CancellationToken cancellationToken)
    {
        payment.GatewayOrderId = result.GatewayOrderId;
        ApplyDisplayData(payment, result);

        if (result.Status == PaymentStatus.Approved && payment.PaidAt is null)
        {
            payment.PaidAt = timeProvider.GetUtcNow().UtcDateTime;
            await approvalHandler.PaymentApprovedAsync(payment, cancellationToken);
        }

        payment.Status = result.Status;
        payment.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
    }

    private static void ApplyDisplayData(Payment payment, PaymentGatewayOrder result)
    {
        if (result.GatewayPaymentId is not null) payment.GatewayPaymentId = result.GatewayPaymentId;
        if (result.PixCopyPaste is not null) payment.PixCopyPaste = result.PixCopyPaste;
        if (result.PixQrCodeBase64 is not null) payment.PixQrCodeBase64 = result.PixQrCodeBase64;
        if (result.ExpiresAt is not null) payment.ExpiresAt = result.ExpiresAt;
        if (result.CardBrand is not null) payment.CardBrand = result.CardBrand;
        if (result.Installments is not null) payment.Installments = result.Installments;
        if (result.LastFourDigits is not null) payment.LastFourDigits = result.LastFourDigits;
        if (result.AuthorizationCode is not null) payment.AuthorizationCode = result.AuthorizationCode;
    }
}
