namespace Orofoods.Web.Services.Payments;

public interface IPointPaymentProvider
{
    Task<PointPaymentResult> CreateTerminalPaymentAsync(PointPaymentRequest request, CancellationToken cancellationToken = default);
    Task<PointPaymentResult> GetPaymentStatusAsync(string externalPaymentId, CancellationToken cancellationToken = default);
    Task<PointPaymentResult> CancelPendingPaymentAsync(string externalPaymentId, CancellationToken cancellationToken = default);
}

public sealed record PointPaymentRequest(
    int OrderId,
    int PaymentId,
    int AssignmentId,
    decimal Amount,
    string DeviceId,
    string? StoreId,
    string? PosId,
    string IdempotencyKey,
    string? ExternalReference = null);

public enum PointPaymentState
{
    Disabled,
    Created,
    Processing,
    ActionRequired,
    Approved,
    Rejected,
    Cancelled,
    Expired,
    Refunded,
    Unknown
}

public sealed record PointPaymentResult(
    PointPaymentState State,
    string ErrorCode,
    string Message,
    PaymentStatus? PaymentStatus = null,
    string? GatewayOrderId = null,
    string? GatewayPaymentId = null,
    string? ExternalReference = null,
    decimal? TotalAmount = null,
    string? ProviderStatus = null,
    string? ProviderStatusDetail = null,
    PointPaymentFinancials? Financials = null)
{
    public static PointPaymentResult Disabled { get; } = new(
        PointPaymentState.Disabled,
        "POINT_INTEGRATION_DISABLED",
        "A integração de pagamento Mercado Pago Point não está habilitada.");
}

/// <summary>Financial fields returned by the authoritative Point order and its nested payment transactions.</summary>
public sealed record PointPaymentFinancials(
    decimal? TotalPaidAmount,
    bool TotalPaidAmountPresent,
    bool TotalPaidAmountInvalid,
    IReadOnlyList<PointPaymentTransaction> Transactions);

/// <summary>A payment transaction nested in the queried Point order.</summary>
public sealed record PointPaymentTransaction(
    string? Id,
    string? Status,
    string? StatusDetail,
    decimal? Amount,
    bool AmountPresent,
    bool AmountInvalid,
    decimal? PaidAmount,
    bool PaidAmountPresent,
    bool PaidAmountInvalid);
