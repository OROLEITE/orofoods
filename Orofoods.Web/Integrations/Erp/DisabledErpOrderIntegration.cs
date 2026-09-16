using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Integrations.Erp;

// Blocks external delivery until the WMC contract and provider configuration are available.
public sealed class DisabledErpOrderIntegration : IErpOrderIntegration
{
    public Task<ErpOrderResult> SendOrderAsync(Order order, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ErpOrderResult(false, Error: "Integração ERP não configurada."));
}
