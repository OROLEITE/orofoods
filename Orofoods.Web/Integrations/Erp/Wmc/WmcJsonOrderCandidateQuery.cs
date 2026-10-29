using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public static class WmcJsonOrderCandidateQuery
{
    public const int BatchSize = 20;

    public static IQueryable<Order> Build(ApplicationDbContext db, int startAfterOrderId) => db.Orders
        .AsNoTracking()
        .Where(order => order.Id > startAfterOrderId
            && order.Status == OrderStatus.Approved
            && order.IntegrationStatus == IntegrationStatus.Pending)
        .Where(order => !db.WmcOrderExportStates.Any(state => state.OrderId == order.Id
            && (state.RequiresManualReview
                || state.Status == WmcOrderExportStatus.Processing
                || state.Status == WmcOrderExportStatus.Imported
                || state.Status == WmcOrderExportStatus.Exported)))
        .Where(order => !db.WmcExportAudits.Any(audit => audit.OrderId == order.Id
            && (audit.Outcome == WmcExportOutcome.Started
                || audit.Outcome == WmcExportOutcome.Unknown
                || audit.Outcome == WmcExportOutcome.Succeeded
                || audit.Outcome == WmcExportOutcome.Imported
                || audit.Outcome == WmcExportOutcome.Generated
                || audit.Outcome == WmcExportOutcome.Available
                || audit.Outcome == WmcExportOutcome.Regenerated)));

    public static async Task<(IReadOnlyList<int> CandidateIds, WmcJsonOrderCandidateSelectionSummary Summary)>
        ReadBatchAsync(ApplicationDbContext db, int startAfterOrderId, CancellationToken cancellationToken = default)
    {
        var manualReviewBlocked = db.Orders
            .Where(order => order.Id > startAfterOrderId
                && order.Status == OrderStatus.Approved
                && order.IntegrationStatus == IntegrationStatus.Pending)
            .Where(order => db.WmcOrderExportStates.Any(state => state.OrderId == order.Id && state.RequiresManualReview));
        var manualReviewCount = await manualReviewBlocked.CountAsync(cancellationToken);

        var stateBlocked = db.Orders
            .Where(order => order.Id > startAfterOrderId
                && order.Status == OrderStatus.Approved
                && order.IntegrationStatus == IntegrationStatus.Pending)
            .Where(order => !db.WmcOrderExportStates.Any(state => state.OrderId == order.Id && state.RequiresManualReview))
            .Where(order => db.WmcOrderExportStates.Any(state => state.OrderId == order.Id
                && (state.Status == WmcOrderExportStatus.Processing
                    || state.Status == WmcOrderExportStatus.Imported
                    || state.Status == WmcOrderExportStatus.Exported)));
        var stateBlockedCount = await stateBlocked.CountAsync(cancellationToken);

        var auditBlocked = db.Orders
            .Where(order => order.Id > startAfterOrderId
                && order.Status == OrderStatus.Approved
                && order.IntegrationStatus == IntegrationStatus.Pending)
            .Where(order => !db.WmcOrderExportStates.Any(state => state.OrderId == order.Id
                && (state.RequiresManualReview
                    || state.Status == WmcOrderExportStatus.Processing
                    || state.Status == WmcOrderExportStatus.Imported
                    || state.Status == WmcOrderExportStatus.Exported)))
            .Where(order => db.WmcExportAudits.Any(audit => audit.OrderId == order.Id
                && (audit.Outcome == WmcExportOutcome.Started
                    || audit.Outcome == WmcExportOutcome.Unknown
                    || audit.Outcome == WmcExportOutcome.Succeeded
                    || audit.Outcome == WmcExportOutcome.Imported
                    || audit.Outcome == WmcExportOutcome.Generated
                    || audit.Outcome == WmcExportOutcome.Available
                    || audit.Outcome == WmcExportOutcome.Regenerated)));
        var auditBlockedCount = await auditBlocked.CountAsync(cancellationToken);

        var eligibleCount = await Build(db, startAfterOrderId).CountAsync(cancellationToken);
        var candidateIds = await Build(db, startAfterOrderId)
            .OrderBy(order => order.LastIntegrationAttempt ?? order.CreatedAt)
            .Select(order => order.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        return (candidateIds, new WmcJsonOrderCandidateSelectionSummary(
            eligibleCount,
            candidateIds.Count,
            manualReviewCount,
            stateBlockedCount,
            auditBlockedCount,
            Math.Max(0, eligibleCount - candidateIds.Count)));
    }
}
