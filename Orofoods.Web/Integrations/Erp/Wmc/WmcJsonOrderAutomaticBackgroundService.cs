using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Services.Orders;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcJsonOrderAutomaticBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<WmcJsonOrderOptions> options,
    ILogger<WmcJsonOrderAutomaticBackgroundService> logger,
    WmcJsonOrderWorkerRuntime? workerRuntime = null) : BackgroundService
{
    private readonly WmcJsonOrderWorkerRuntime runtime = workerRuntime ?? new WmcJsonOrderWorkerRuntime();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var startedAtUtc = DateTime.UtcNow;
        runtime.MarkServiceStarted(startedAtUtc);
        logger.LogInformation("WMC JSON automatic worker service started at {ServiceStartedAtUtc}.", startedAtUtc);

        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ProcessPendingOrdersAsync(stoppingToken);
            }
        }
        finally
        {
            var stoppedAtUtc = DateTime.UtcNow;
            runtime.MarkServiceStopped(stoppedAtUtc);
            logger.LogInformation("WMC JSON automatic worker service stopped at {ServiceStoppedAtUtc}.", stoppedAtUtc);
        }
    }

    public async Task ProcessPendingOrdersAsync(CancellationToken cancellationToken = default)
    {
        var cycleStartedAtUtc = DateTime.UtcNow;
        runtime.BeginCycle(cycleStartedAtUtc);
        logger.LogInformation("WMC JSON automatic worker cycle started at {CycleStartedAtUtc}.", cycleStartedAtUtc);

        try
        {
            var settings = options.Value;
            if (!settings.Enabled || !settings.AutomaticWorkerEnabled)
            {
                logger.LogDebug("Worker automático HTTP/JSON do WMC desativado pela configuração.");
                return;
            }

            if (settings.AutomaticWorkerStartAfterOrderId is not int startAfterOrderId || startAfterOrderId < 0)
            {
                runtime.RecordCycleError("InvalidActivationCutoff");
                logger.LogError("Automatic WMC JSON worker cycle blocked because AutomaticWorkerStartAfterOrderId is missing or invalid.");
                return;
            }

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var integrationService = scope.ServiceProvider.GetRequiredService<OrderIntegrationService>();
            var jsonIntegration = scope.ServiceProvider.GetRequiredService<WmcJsonOrderErpIntegration>();
            var (candidateIds, selection) = await WmcJsonOrderCandidateQuery.ReadBatchAsync(db, startAfterOrderId, cancellationToken);
            runtime.SetCandidateSelection(selection);

            foreach (var orderId in candidateIds)
            {
                try
                {
                    var result = await integrationService.SendAutomaticJsonAsync(orderId, jsonIntegration, cancellationToken);
                    if (result is null)
                    {
                        runtime.RecordSkippedBeforeReservation();
                        logger.LogWarning(
                            "WMC JSON automatic worker skipped OrderId={OrderId} before atomic reservation; reason=SendReturnedNoResult.",
                            orderId);
                        continue;
                    }

                    if (result.AtomicReservationRejected)
                    {
                        runtime.RecordReservationRejected();
                        continue;
                    }

                    runtime.RecordAttempt(result.Succeeded);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    runtime.RecordAttempt(succeeded: false, safeErrorType: exception.GetType().Name);
                    logger.LogError(
                        "WMC JSON worker failed while processing OrderId={OrderId}; ErrorType={ErrorType}.",
                        orderId,
                        exception.GetType().Name);
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            runtime.RecordCycleError(exception.GetType().Name);
            logger.LogError("WMC JSON automatic worker cycle failed; ErrorType={ErrorType}.", exception.GetType().Name);
        }
        finally
        {
            if (cancellationToken.IsCancellationRequested)
                runtime.RecordCycleError("OperationCanceledException");

            var summary = runtime.FinishCycle(DateTime.UtcNow);
            if (summary is not null)
            {
                logger.LogInformation(
                    "WMC JSON automatic worker cycle finished at {CycleFinishedAtUtc}; CycleStartedAtUtc={CycleStartedAtUtc}; Eligible={EligibleCount}; Candidates={CandidateCount}; Processed={ProcessedCount}; Succeeded={SucceededCount}; Failed={FailedCount}; ReservationRejected={ReservationRejectedCount}; SkippedBeforeReservation={SkippedBeforeReservationCount}; ExcludedManualReview={ExcludedManualReviewCount}; ExcludedExportState={ExcludedExportStateCount}; ExcludedAudit={ExcludedAuditCount}; ExcludedBatchLimit={ExcludedBatchLimitCount}; Errors={CycleErrorCount}; LastErrorType={LastSafeErrorType}.",
                    summary.FinishedAtUtc,
                    summary.StartedAtUtc,
                    summary.EligibleCount,
                    summary.CandidateCount,
                    summary.ProcessedCount,
                    summary.SucceededCount,
                    summary.FailedCount,
                    summary.ReservationRejectedCount,
                    summary.SkippedBeforeReservationCount,
                    summary.ExcludedManualReviewCount,
                    summary.ExcludedExportStateCount,
                    summary.ExcludedAuditCount,
                    summary.ExcludedBatchLimitCount,
                    summary.CycleErrorCount,
                    summary.LastSafeErrorType ?? "none");
            }
        }
    }
}
