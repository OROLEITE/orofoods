using System.Linq.Expressions;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Services.Orders;

public static class AdminOrderMonitorQueries
{
    public static Expression<Func<Order, bool>> AwaitingAction =>
        order => order.Status == OrderStatus.Received || order.Status == OrderStatus.UnderReview;

    public static Expression<Func<Order, bool>> WmcAwaitingConfirmation =>
        order => order.WmcOrderExportState != null
            && order.WmcOrderExportState.Status == WmcOrderExportStatus.Exported
            && !order.WmcOrderExportState.RequiresManualReview;

    public static Expression<Func<Order, bool>> WmcNeedsAttention =>
        order => order.WmcOrderExportState != null
                && (order.WmcOrderExportState.RequiresManualReview
                    || order.WmcOrderExportState.Status == WmcOrderExportStatus.Rejected
                    || order.WmcOrderExportState.Status == WmcOrderExportStatus.Error)
            || order.WmcOrderExportState == null && order.WmcExportAudits.Any();

    public static IQueryable<Order> ApplyWmcStatus(IQueryable<Order> query, string? status) => status?.ToLowerInvariant() switch
    {
        "pending" => query.Where(order => order.WmcOrderExportState == null
            || order.WmcOrderExportState.RequiresManualReview
            || order.WmcOrderExportState.Status == WmcOrderExportStatus.Pending
            || order.WmcOrderExportState.Status == WmcOrderExportStatus.Processing),
        "exported" => query.Where(WmcAwaitingConfirmation),
        "imported" => query.Where(order => order.WmcOrderExportState != null
            && order.WmcOrderExportState.Status == WmcOrderExportStatus.Imported
            && !order.WmcOrderExportState.RequiresManualReview),
        "rejected" => query.Where(order => order.WmcOrderExportState != null
            && order.WmcOrderExportState.Status == WmcOrderExportStatus.Rejected
            && !order.WmcOrderExportState.RequiresManualReview),
        "error" => query.Where(order => order.WmcOrderExportState != null
            && order.WmcOrderExportState.Status == WmcOrderExportStatus.Error
            && !order.WmcOrderExportState.RequiresManualReview),
        _ => query
    };

    public static bool HasUnresolvedWmcExport(Order order) =>
        order.WmcOrderExportState is not { Status: WmcOrderExportStatus.Imported, RequiresManualReview: false };
}
