using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Services.Orders;

namespace Orofoods.Web.Integrations.Erp;

public sealed class ErpRetryBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<ErpRetryBackgroundService> logger,
    IOptions<WmcFileDropOptions> wmcOptions) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!wmcOptions.Value.Enabled)
            {
                logger.LogDebug("Integracao WMC permanece desativada.");
            }
            else
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var service = scope.ServiceProvider.GetRequiredService<OrderIntegrationService>();
                var ids = await db.Orders
                    .Where(order => order.IntegrationStatus == IntegrationStatus.Pending ||
                        (wmcOptions.Value.AutoRetryEnabled && order.IntegrationStatus == IntegrationStatus.Failed))
                    .OrderBy(order => order.LastIntegrationAttempt ?? order.CreatedAt)
                    .Select(order => order.Id)
                    .Take(20)
                    .ToListAsync(stoppingToken);
                foreach (var id in ids)
                {
                    try { await service.SendAsync(id, stoppingToken); }
                    catch (Exception exception) { logger.LogError(exception, "Falha ao processar pedido {OrderId}", id); }
                }
            }
        }
    }
}
