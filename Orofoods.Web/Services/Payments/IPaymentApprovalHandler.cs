namespace Orofoods.Web.Services.Payments;

/// <summary>Extension point invoked exactly once when a payment first reaches <see cref="PaymentStatus.Approved"/>.</summary>
public interface IPaymentApprovalHandler
{
    Task PaymentApprovedAsync(Payment payment, CancellationToken cancellationToken = default);
}
