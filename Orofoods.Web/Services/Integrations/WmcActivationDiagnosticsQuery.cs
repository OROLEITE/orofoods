using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Services.Integrations;

public sealed class WmcActivationDiagnosticsQuery(
    ApplicationDbContext db,
    IOptions<WmcJsonOrderOptions> wmcJsonOrderOptions,
    IOptions<WmcFileDropOptions> wmcFileDropOptions,
    WmcJsonOrderWorkerRuntime? workerRuntime = null)
{
    private readonly WmcJsonOrderWorkerRuntime runtime = workerRuntime ?? new WmcJsonOrderWorkerRuntime();

    public async Task<WmcActivationDiagnostics> ReadAsync(
        int? diagnosticOrderId = null,
        CancellationToken cancellationToken = default)
    {
        var aggregates = await db.Orders
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                MaxOrderId = (int?)group.Max(order => order.Id),
                TotalOrderCount = group.LongCount()
            })
            .SingleOrDefaultAsync(cancellationToken);
        var jsonOptions = wmcJsonOrderOptions.Value;

        var inputError = diagnosticOrderId is <= 0
            ? "Informe um Order.Id positivo."
            : null;
        var orderEligibility = diagnosticOrderId is > 0
            ? await ReadOrderEligibilityAsync(diagnosticOrderId.Value, jsonOptions, cancellationToken)
            : null;

        return new WmcActivationDiagnostics(
            aggregates?.MaxOrderId ?? 0,
            aggregates?.TotalOrderCount ?? 0,
            jsonOptions.AutomaticWorkerStartAfterOrderId,
            jsonOptions.Enabled,
            jsonOptions.AutomaticWorkerEnabled,
            wmcFileDropOptions.Value.Enabled,
            runtime.Snapshot,
            orderEligibility,
            inputError);
    }

    private async Task<WmcOrderEligibilityDiagnostic> ReadOrderEligibilityAsync(
        int orderId,
        WmcJsonOrderOptions options,
        CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Where(candidate => candidate.Id == orderId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.Status,
                candidate.IntegrationStatus,
                HasManualReview = db.WmcOrderExportStates.Any(state => state.OrderId == candidate.Id && state.RequiresManualReview),
                HasBlockingState = db.WmcOrderExportStates.Any(state => state.OrderId == candidate.Id
                    && (state.Status == WmcOrderExportStatus.Processing
                        || state.Status == WmcOrderExportStatus.Imported
                        || state.Status == WmcOrderExportStatus.Exported)),
                HasBlockingAudit = db.WmcExportAudits.Any(audit => audit.OrderId == candidate.Id
                    && (audit.Outcome == WmcExportOutcome.Started
                        || audit.Outcome == WmcExportOutcome.Unknown
                        || audit.Outcome == WmcExportOutcome.Succeeded
                        || audit.Outcome == WmcExportOutcome.Imported
                        || audit.Outcome == WmcExportOutcome.Generated
                        || audit.Outcome == WmcExportOutcome.Available
                        || audit.Outcome == WmcExportOutcome.Regenerated))
            })
            .SingleOrDefaultAsync(cancellationToken);

        var reasons = new List<string>();
        if (!options.Enabled)
            reasons.Add("json-disabled");
        if (!options.AutomaticWorkerEnabled)
            reasons.Add("automatic-worker-disabled");

        var hasValidCutoff = options.AutomaticWorkerStartAfterOrderId is >= 0;
        if (!hasValidCutoff)
            reasons.Add("cutoff-missing-or-invalid");

        if (order is null)
        {
            reasons.Add("order-not-found");
            return new WmcOrderEligibilityDiagnostic(
                orderId,
                Exists: false,
                OrderStatus: null,
                IntegrationStatus: null,
                AboveActivationCutoff: false,
                RequiresManualReview: false,
                HasBlockingExportState: false,
                HasBlockingAudit: false,
                MatchesReservationPredicates: false,
                IncludedInNextBatch: false,
                ExclusionReasons: reasons);
        }

        var aboveCutoff = hasValidCutoff && order.Id > options.AutomaticWorkerStartAfterOrderId;
        if (hasValidCutoff && !aboveCutoff)
            reasons.Add("at-or-below-cutoff");
        if (order.Status != OrderStatus.Approved)
            reasons.Add("order-not-approved");
        if (order.IntegrationStatus != IntegrationStatus.Pending)
            reasons.Add("integration-not-pending");
        if (order.HasManualReview)
            reasons.Add("manual-review");
        if (order.HasBlockingState)
            reasons.Add("blocking-export-state");
        if (order.HasBlockingAudit)
            reasons.Add("blocking-export-audit");

        var configAllowsWorker = options.Enabled && options.AutomaticWorkerEnabled && hasValidCutoff;
        var matchesReservationPredicates = configAllowsWorker && await WmcJsonOrderCandidateQuery.Build(
                db,
                options.AutomaticWorkerStartAfterOrderId!.Value)
            .AnyAsync(candidate => candidate.Id == orderId, cancellationToken);
        var nextBatchIds = matchesReservationPredicates
            ? await WmcJsonOrderCandidateQuery.Build(db, options.AutomaticWorkerStartAfterOrderId!.Value)
                .OrderBy(candidate => candidate.LastIntegrationAttempt ?? candidate.CreatedAt)
                .Select(candidate => candidate.Id)
                .Take(WmcJsonOrderCandidateQuery.BatchSize)
                .ToListAsync(cancellationToken)
            : [];
        var includedInNextBatch = nextBatchIds.Contains(orderId);

        if (matchesReservationPredicates && !includedInNextBatch)
            reasons.Add("batch-limit-20");

        return new WmcOrderEligibilityDiagnostic(
            order.Id,
            Exists: true,
            OrderStatus: order.Status.ToString(),
            IntegrationStatus: order.IntegrationStatus.ToString(),
            AboveActivationCutoff: aboveCutoff,
            RequiresManualReview: order.HasManualReview,
            HasBlockingExportState: order.HasBlockingState,
            HasBlockingAudit: order.HasBlockingAudit,
            MatchesReservationPredicates: matchesReservationPredicates,
            IncludedInNextBatch: includedInNextBatch,
            ExclusionReasons: reasons);
    }
}
