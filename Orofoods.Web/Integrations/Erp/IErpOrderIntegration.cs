using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Integrations.Erp;

public interface IErpOrderIntegration
{
    Task<ErpOrderResult> SendOrderAsync(Order order, CancellationToken cancellationToken = default);
}

public sealed record ErpOrderResult(bool Succeeded, string? ExternalOrderId = null, string? ErpOrderNumber = null, string? Error = null);
