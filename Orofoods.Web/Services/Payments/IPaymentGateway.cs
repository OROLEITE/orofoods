using System.Net;

namespace Orofoods.Web.Services.Payments;

public interface IPaymentGateway
{
    Task<PaymentGatewayOrder> CreatePixAsync(CreatePixPaymentRequest request, CancellationToken cancellationToken = default);
    Task<PaymentGatewayOrder> CreateCreditCardPaymentAsync(CreateCreditCardPaymentRequest request, CancellationToken cancellationToken = default);
    Task<PaymentGatewayOrder> GetOrderAsync(string gatewayOrderId, CancellationToken cancellationToken = default);
    Task<PaymentGatewayOrder> RefundAsync(RefundPaymentRequest request, CancellationToken cancellationToken = default);
}

public sealed record CreatePixPaymentRequest(
    decimal Amount,
    string ExternalReference,
    string PayerEmail,
    TimeSpan Expiration,
    string IdempotencyKey);

public sealed record CreateCreditCardPaymentRequest(
    decimal Amount,
    string ExternalReference,
    string PayerEmail,
    string CardToken,
    string PaymentMethodId,
    int Installments,
    string IdempotencyKey);

public sealed record RefundPaymentRequest(
    string GatewayOrderId,
    string GatewayPaymentId,
    decimal Amount,
    string IdempotencyKey);

public sealed record PaymentGatewayOrder(
    string GatewayOrderId,
    string? GatewayPaymentId,
    string? ExternalReference,
    decimal TotalAmount,
    PaymentStatus Status,
    string? PixCopyPaste,
    string? PixQrCodeBase64,
    DateTime? ExpiresAt,
    string? CardBrand,
    int? Installments,
    string? LastFourDigits = null,
    string? AuthorizationCode = null);

public sealed class PaymentGatewayException(HttpStatusCode statusCode)
    : Exception($"Mercado Pago request failed with HTTP status {(int)statusCode}.")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
