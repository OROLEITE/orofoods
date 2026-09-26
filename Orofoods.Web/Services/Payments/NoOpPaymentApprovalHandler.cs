namespace Orofoods.Web.Services.Payments;

/// <summary>No-op placeholder; WMC release/write behavior is intentionally out of scope for this phase.</summary>
public sealed class NoOpPaymentApprovalHandler : IPaymentApprovalHandler
{
    public Task PaymentApprovedAsync(Payment payment, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
