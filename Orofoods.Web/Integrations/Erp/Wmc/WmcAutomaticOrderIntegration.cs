using Microsoft.Extensions.Options;
using Orofoods.Web.Integrations.Erp;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcAutomaticOrderIntegration(
    IOptions<WmcJsonOrderOptions> jsonOptions,
    WmcJsonOrderErpIntegration jsonIntegration,
    WmcFileDropErpOrderIntegration fileDropIntegration) : IErpOrderIntegration
{
    public Task<ErpOrderResult> SendOrderAsync(Order order, CancellationToken cancellationToken = default) =>
        jsonOptions.Value.Enabled
            ? jsonIntegration.SendOrderAsync(order, cancellationToken)
            : fileDropIntegration.SendOrderAsync(order, cancellationToken);
}
