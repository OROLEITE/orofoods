using Microsoft.Extensions.Options;
using Orofoods.Web.Integrations.Erp;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcAutomaticOrderIntegration(
    IOptions<WmcJsonOrderOptions> jsonOptions,
    WmcJsonOrderErpIntegration jsonIntegration,
    WmcFileDropErpOrderIntegration fileDropIntegration) : IErpOrderIntegration
{
    public Task<ErpOrderResult> SendOrderAsync(Order order, CancellationToken cancellationToken = default, Guid? attemptId = null) =>
        jsonOptions.Value.Enabled
            ? jsonIntegration.SendOrderAsync(order, cancellationToken, attemptId)
            : fileDropIntegration.SendOrderAsync(order, cancellationToken, attemptId);
}
