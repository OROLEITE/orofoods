using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.ViewModels;

public sealed class AdminDashboardViewModel
{
    public int OrdersToday { get; init; }
    public int PendingOrders { get; init; }
    public int ActiveCustomers { get; init; }
    public int PendingCustomers { get; init; }
    public decimal MonthRevenue { get; init; }
    public decimal? MonthRevenueChangePercent { get; init; }
    public decimal AverageTicket { get; init; }
    public int FailedIntegrations { get; init; }
    public int FailedWmcExports { get; init; }
    public int LowStockProducts { get; init; }
    public List<Order> RecentOrders { get; init; } = [];
}
