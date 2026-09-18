namespace Orofoods.Web.Services.Payments;

public sealed class PendingBoletoProvider : IBoletoProvider
{
    public Task<BoletoProviderResult> CreateBankSlipAsync(BoletoProviderRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new BoletoProviderResult(null, request.DueDate, null, null, null, PaymentStatus.Pending));
}