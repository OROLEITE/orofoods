using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.ViewModels;

public sealed class AdminDashboardViewModel
{
    public int OrdersToday { get; init; }
    public int OrdersTodayTotal { get; init; }
    public int OrdersTodayAwaitingAction { get; init; }
    public int OrdersTodayReceived { get; init; }
    public int OrdersTodayInAnalysisOrSeparation { get; init; }
    public int OrdersTodayReadyOrInDelivery { get; init; }
    public int OrdersTodayDelivered { get; init; }
    public int OrdersDeliveredToday { get; init; }
    public int PendingOrders { get; init; }
    public int ActiveCustomers { get; init; }
    public int PendingCustomers { get; init; }
    public decimal MonthRevenue { get; init; }
    public decimal? MonthRevenueChangePercent { get; init; }
    public decimal AverageTicket { get; init; }
    public int FailedIntegrations { get; init; }
    public int PendingIntegrations { get; init; }
    public int ProcessingIntegrations { get; init; }
    public bool HasIntegrationRecords { get; init; }
    public int FailedWmcExports { get; init; }
    public bool WmcSyncIsRunning { get; init; }
    public bool WmcSyncHasRun { get; init; }
    public bool WmcSyncHasFailed { get; init; }
    public int ActiveMercadoPagoPointTerminals { get; init; }
    public int FailedWhatsAppMessages { get; init; }
    public int LowStockProducts { get; init; }
    public List<Order> RecentOrders { get; init; } = [];
}
