using Orofoods.Web.Models.Integrations;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcJsonOrderWorkerRuntime
{
    private readonly object sync = new();
    private bool isServiceRunning;
    private DateTime? serviceStartedAtUtc;
    private DateTime? serviceStoppedAtUtc;
    private MutableCycle? currentCycle;
    private WmcJsonOrderWorkerCycleSummary? lastCycle;

    public WmcJsonOrderWorkerRuntimeSnapshot Snapshot
    {
        get
        {
            lock (sync)
            {
                return new WmcJsonOrderWorkerRuntimeSnapshot(
                    isServiceRunning,
                    serviceStartedAtUtc,
                    serviceStoppedAtUtc,
                    currentCycle?.StartedAtUtc,
                    lastCycle);
            }
        }
    }

    public void MarkServiceStarted(DateTime startedAtUtc)
    {
        lock (sync)
        {
            isServiceRunning = true;
            serviceStartedAtUtc = startedAtUtc;
            serviceStoppedAtUtc = null;
        }
    }

    public void MarkServiceStopped(DateTime stoppedAtUtc)
    {
        lock (sync)
        {
            isServiceRunning = false;
            serviceStoppedAtUtc = stoppedAtUtc;
            if (currentCycle is not null)
            {
                currentCycle.CycleErrorCount++;
                currentCycle.LastSafeErrorType = "ServiceStoppedDuringCycle";
                lastCycle = currentCycle.ToSummary(stoppedAtUtc);
                currentCycle = null;
            }
        }
    }

    public void BeginCycle(DateTime startedAtUtc)
    {
        lock (sync)
        {
            currentCycle = new MutableCycle(startedAtUtc);
        }
    }

    public void SetCandidateSelection(WmcJsonOrderCandidateSelectionSummary selection)
    {
        lock (sync)
        {
            if (currentCycle is null)
                return;

            currentCycle.EligibleCount = selection.EligibleCount;
            currentCycle.CandidateCount = selection.CandidateCount;
            currentCycle.ExcludedManualReviewCount = selection.ExcludedManualReviewCount;
            currentCycle.ExcludedExportStateCount = selection.ExcludedExportStateCount;
            currentCycle.ExcludedAuditCount = selection.ExcludedAuditCount;
            currentCycle.ExcludedBatchLimitCount = selection.ExcludedBatchLimitCount;
        }
    }

    public void RecordAttempt(bool succeeded)
    {
        lock (sync)
        {
            if (currentCycle is null)
                return;

            currentCycle.ProcessedCount++;
            if (succeeded)
                currentCycle.SucceededCount++;
            else
                currentCycle.FailedCount++;
        }
    }

    public void RecordReservationRejected()
    {
        lock (sync)
        {
            if (currentCycle is null)
                return;

            currentCycle.ProcessedCount++;
            currentCycle.ReservationRejectedCount++;
        }
    }

    public void RecordCycleError(string safeErrorType)
    {
        lock (sync)
        {
            if (currentCycle is null)
                return;

            currentCycle.CycleErrorCount++;
            currentCycle.LastSafeErrorType = safeErrorType;
        }
    }

    public WmcJsonOrderWorkerCycleSummary? FinishCycle(DateTime finishedAtUtc)
    {
        lock (sync)
        {
            if (currentCycle is null)
                return lastCycle;

            lastCycle = currentCycle.ToSummary(finishedAtUtc);
            currentCycle = null;
            return lastCycle;
        }
    }

    private sealed class MutableCycle(DateTime startedAtUtc)
    {
        public DateTime StartedAtUtc { get; } = startedAtUtc;
        public int EligibleCount { get; set; }
        public int CandidateCount { get; set; }
        public int ProcessedCount { get; set; }
        public int SucceededCount { get; set; }
        public int FailedCount { get; set; }
        public int ReservationRejectedCount { get; set; }
        public int ExcludedManualReviewCount { get; set; }
        public int ExcludedExportStateCount { get; set; }
        public int ExcludedAuditCount { get; set; }
        public int ExcludedBatchLimitCount { get; set; }
        public int CycleErrorCount { get; set; }
        public string? LastSafeErrorType { get; set; }

        public WmcJsonOrderWorkerCycleSummary ToSummary(DateTime finishedAtUtc) => new(
            StartedAtUtc,
            finishedAtUtc,
            EligibleCount,
            CandidateCount,
            ProcessedCount,
            SucceededCount,
            FailedCount,
            ReservationRejectedCount,
            ExcludedManualReviewCount,
            ExcludedExportStateCount,
            ExcludedAuditCount,
            ExcludedBatchLimitCount,
            CycleErrorCount,
            LastSafeErrorType);
    }
}
