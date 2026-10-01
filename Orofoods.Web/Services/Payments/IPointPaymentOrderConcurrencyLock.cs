using System.Data;

namespace Orofoods.Web.Services.Payments;

public interface IPointPaymentOrderConcurrencyLock
{
    IsolationLevel TransactionIsolationLevel { get; }
    Task<bool> AcquireAsync(int orderId, CancellationToken cancellationToken = default);
}
