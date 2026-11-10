using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public interface IWmcTransmissionIdAllocator
{
    Task<int> GetOrAllocateAsync(Order order, CancellationToken cancellationToken = default);
}
