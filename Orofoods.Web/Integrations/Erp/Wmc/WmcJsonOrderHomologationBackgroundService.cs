using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Services.Orders;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcJsonOrderHomologationBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<WmcJsonOrderOptions> options,
    ILogger<WmcJsonOrderHomologationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled || !settings.HomologationWorkerEnabled || settings.HomologationOrderId is not int orderId || orderId <= 0)
        {
            logger.LogDebug("Worker HTTP WMC de homologação desativado ou sem Order.Id selecionado.");
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var pending = await db.Orders.AnyAsync(
            order => order.Id == orderId && order.IntegrationStatus == IntegrationStatus.Pending,
            stoppingToken);
        if (!pending)
        {
            logger.LogInformation("Order.Id {OrderId} não está pendente; worker HTTP WMC não enviou pedidos.", orderId);
            return;
        }

        var integrationService = scope.ServiceProvider.GetRequiredService<OrderIntegrationService>();
        var jsonIntegration = scope.ServiceProvider.GetRequiredService<WmcJsonOrderErpIntegration>();
        await integrationService.SendAsync(orderId, jsonIntegration, stoppingToken);
    }
}
