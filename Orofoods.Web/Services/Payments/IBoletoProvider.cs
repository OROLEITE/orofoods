namespace Orofoods.Web.Services.Payments;

public interface IBoletoProvider
{
    Task<BoletoProviderResult> CreateBankSlipAsync(BoletoProviderRequest request, CancellationToken cancellationToken = default);
}

public sealed record BoletoProviderRequest(int OrderId, int CustomerId, decimal Amount, DateTime DueDate);

public sealed record BoletoProviderResult(
    string? ExternalPaymentId,
    DateTime DueDate,
    string? DigitableLine,
    string? Barcode,
    string? BankSlipUrl,
    PaymentStatus Status);