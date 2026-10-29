namespace Orofoods.Web.Models.Integrations;

public sealed record WmcOrderEligibilityDiagnostic(
    int OrderId,
    bool Exists,
    string? OrderStatus,
    string? IntegrationStatus,
    bool AboveActivationCutoff,
    bool RequiresManualReview,
    bool HasBlockingExportState,
    bool HasBlockingAudit,
    bool MatchesReservationPredicates,
    bool IncludedInNextBatch,
    IReadOnlyList<string> ExclusionReasons);
