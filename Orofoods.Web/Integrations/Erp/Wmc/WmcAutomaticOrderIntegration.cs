using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Orofoods.Web.Integrations.Erp;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcAutomaticOrderIntegration(
    IOptions<WmcJsonOrderOptions> jsonOptions,
    WmcJsonOrderErpIntegration jsonIntegration,
    IServiceProvider serviceProvider) : IErpOrderIntegration
{
    public Task<ErpOrderResult> SendOrderAsync(Order order, CancellationToken cancellationToken = default, Guid? attemptId = null)
    {
        if (jsonOptions.Value.Enabled)
        {
            return jsonIntegration.SendOrderAsync(order, cancellationToken, attemptId);
        }

        return serviceProvider.GetRequiredService<WmcFileDropErpOrderIntegration>()
            .SendOrderAsync(order, cancellationToken, attemptId);
    }
}
