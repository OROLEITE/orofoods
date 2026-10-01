namespace Orofoods.Web.Services.Payments;

public sealed class DisabledPointPaymentProvider : IPointPaymentProvider
{
    public Task<PointPaymentResult> CreateTerminalPaymentAsync(PointPaymentRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(PointPaymentResult.Disabled);

    public Task<PointPaymentResult> GetPaymentStatusAsync(string externalPaymentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(PointPaymentResult.Disabled);

    public Task<PointPaymentResult> CancelPendingPaymentAsync(string externalPaymentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(PointPaymentResult.Disabled);
}
