namespace Orofoods.Web.Services.Payments;

internal sealed record MercadoPagoCreateOrderRequest(
    string Type,
    string ProcessingMode,
    string TotalAmount,
    string ExternalReference,
    MercadoPagoPayerRequest Payer,
    MercadoPagoTransactionsRequest Transactions);

internal sealed record MercadoPagoPayerRequest(string Email);

internal sealed record MercadoPagoTransactionsRequest(IReadOnlyList<MercadoPagoPaymentRequest> Payments);

internal sealed record MercadoPagoPaymentRequest(
    string Amount,
    MercadoPagoPaymentMethodRequest PaymentMethod,
    string? ExpirationTime = null);

internal sealed record MercadoPagoPaymentMethodRequest(
    string Id,
    string Type,
    string? Token = null,
    int? Installments = null);

internal sealed record MercadoPagoRefundRequest(IReadOnlyList<MercadoPagoRefundTransactionRequest> Transactions);

internal sealed record MercadoPagoRefundTransactionRequest(string Id, string Amount);

internal sealed class MercadoPagoOrderResponse
{
    public string Id { get; init; } = "";
    public string? ExternalReference { get; init; }
    public string? TotalAmount { get; init; }
    public string? Status { get; init; }
    public string? StatusDetail { get; init; }
    public MercadoPagoTransactionsResponse? Transactions { get; init; }
}

internal sealed class MercadoPagoTransactionsResponse
{
    public List<MercadoPagoPaymentResponse> Payments { get; init; } = [];
    public List<MercadoPagoRefundResponse> Refunds { get; init; } = [];
}

internal sealed class MercadoPagoPaymentResponse
{
    public string? Id { get; init; }
    public string? Amount { get; init; }
    public string? Status { get; init; }
    public string? StatusDetail { get; init; }
    // DateTimeOffset preserves the offset Mercado Pago sends (e.g. -03:00); DateTime would lose it and default to Kind=Local.
    public DateTimeOffset? DateOfExpiration { get; init; }
    public MercadoPagoPaymentMethodResponse? PaymentMethod { get; init; }
    // Verified against a real sandbox response: the Orders API (unlike the classic Payments API) does not
    // return authorization_code or a card object at all, so these are expected to stay null via this endpoint.
    public string? AuthorizationCode { get; init; }
    public MercadoPagoCardResponse? Card { get; init; }
}

internal sealed class MercadoPagoCardResponse
{
    public string? LastFourDigits { get; init; }
}

internal sealed class MercadoPagoPaymentMethodResponse
{
    public string? Id { get; init; }
    public string? Type { get; init; }
    public int? Installments { get; init; }
    public string? QrCode { get; init; }
    public string? QrCodeBase64 { get; init; }
}

internal sealed class MercadoPagoRefundResponse
{
    public string? TransactionId { get; init; }
}
