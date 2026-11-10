using Microsoft.Extensions.Options;

namespace Orofoods.Web.Services.Commercial;

public sealed class WhatsAppHistoryImportWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<WhatsAppBusinessOptions> options,
    ILogger<WhatsAppHistoryImportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!options.Value.Enabled || !options.Value.EmbeddedSignupEnabled)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                continue;
            }

            var processed = false;
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<WhatsAppWebhookDeliveryProcessor>();
                processed = await processor.ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError("WhatsApp history delivery worker failed. ExceptionType={ExceptionType}", exception.GetType().Name);
            }

            if (!processed)
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
