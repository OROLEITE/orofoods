using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Orders;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcJsonOrderAutomaticBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<WmcJsonOrderOptions> options,
    ILogger<WmcJsonOrderAutomaticBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessPendingOrdersAsync(stoppingToken);
        }
    }

    public async Task ProcessPendingOrdersAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled || !settings.AutomaticWorkerEnabled)
        {
            logger.LogDebug("Worker automÃ¡tico HTTP/JSON do WMC desativado.");
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var integrationService = scope.ServiceProvider.GetRequiredService<OrderIntegrationService>();
        var jsonIntegration = scope.ServiceProvider.GetRequiredService<WmcJsonOrderErpIntegration>();
        var candidates = await db.Orders
            .Where(order => order.Status == OrderStatus.Approved && order.IntegrationStatus == IntegrationStatus.Pending)
            .OrderBy(order => order.LastIntegrationAttempt ?? order.CreatedAt)
            .Select(order => order.Id)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var orderId in candidates)
        {
            try
            {
                await integrationService.SendAutomaticJsonAsync(orderId, jsonIntegration, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Falha ao processar pedido {OrderId} pelo WMC HTTP/JSON.", orderId);
            }
        }
    }
}
