namespace Orofoods.Web.Models.Integrations;

public sealed record WmcJsonOrderCandidateSelectionSummary(
    int EligibleCount,
    int CandidateCount,
    int ExcludedManualReviewCount,
    int ExcludedExportStateCount,
    int ExcludedAuditCount,
    int ExcludedBatchLimitCount);

public sealed record WmcJsonOrderWorkerCycleSummary(
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    int EligibleCount,
    int CandidateCount,
    int ProcessedCount,
    int SucceededCount,
    int FailedCount,
    int ReservationRejectedCount,
    int SkippedBeforeReservationCount,
    int ExcludedManualReviewCount,
    int ExcludedExportStateCount,
    int ExcludedAuditCount,
    int ExcludedBatchLimitCount,
    int CycleErrorCount,
    string? LastSafeErrorType);

public sealed record WmcJsonOrderWorkerRuntimeSnapshot(
    bool IsServiceRunning,
    DateTime? ServiceStartedAtUtc,
    DateTime? ServiceStoppedAtUtc,
    DateTime? CurrentCycleStartedAtUtc,
    WmcJsonOrderWorkerCycleSummary? LastCycle);
