using Microsoft.Extensions.Options;
using Orofoods.Web.Integrations.Erp;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcAutomaticOrderIntegration(
    IOptions<WmcJsonOrderOptions> jsonOptions,
    WmcJsonOrderErpIntegration jsonIntegration) : IErpOrderIntegration
{
    public Task<ErpOrderResult> SendOrderAsync(Order order, CancellationToken cancellationToken = default, Guid? attemptId = null)
    {
        if (!jsonOptions.Value.Enabled || !jsonOptions.Value.AutomaticWorkerEnabled)
        {
            return Task.FromResult(new ErpOrderResult(
                false,
                Error: "WMC automatic HTTP/JSON processing is disabled."));
        }

        return jsonIntegration.SendAutomaticWorkerOrderAsync(order, cancellationToken, attemptId);
    }
}
