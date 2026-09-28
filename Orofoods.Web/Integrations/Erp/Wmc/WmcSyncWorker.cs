using Microsoft.Extensions.Options;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcSyncWorker(
    IServiceScopeFactory scopeFactory,
    WmcSyncCoordinator coordinator,
    ILogger<WmcSyncWorker> logger,
    IOptions<WmcSyncOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, options.Value.IntervalMinutes)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!options.Value.Enabled)
            {
                logger.LogDebug("Sincronizacao WMC permanece desativada.");
                continue;
            }

            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<WmcSyncService>();
            var result = await coordinator.RunExclusivelyAsync(() => service.SyncAllAsync(stoppingToken), stoppingToken);
            if (result is null)
            {
                logger.LogDebug("Sincronizacao WMC ja em andamento; execucao agendada ignorada.");
            }
        }
    }
}
