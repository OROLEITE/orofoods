namespace Orofoods.Web.Tests.Views;

public sealed class AdminOrdersMonitorViewTests
{
    private static string WebProjectPath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));

    [Fact]
    public void Dashboard_pending_orders_opens_the_shared_monitor_scope()
    {
        var dashboardView = File.ReadAllText(Path.Combine(WebProjectPath, "Areas", "Admin", "Views", "Dashboard", "Index.cshtml"));
        var dashboardController = File.ReadAllText(Path.Combine(WebProjectPath, "Areas", "Admin", "Controllers", "DashboardController.cs"));
        var queryPredicates = File.ReadAllText(Path.Combine(WebProjectPath, "Services", "Orders", "AdminOrderMonitorQueries.cs"));

        Assert.Contains("asp-controller=\"Orders\" asp-action=\"Index\" asp-route-scope=\"awaiting-action\"", dashboardView);
        Assert.Contains("PendingOrders = await db.Orders.CountAsync(AdminOrderMonitorQueries.AwaitingAction)", dashboardController);
        Assert.Contains("order.Status == OrderStatus.Received || order.Status == OrderStatus.UnderReview", queryPredicates);
    }

    [Fact]
    public void Order_monitor_exposes_get_filters_summary_and_filter_preserving_navigation()
    {
        var view = File.ReadAllText(Path.Combine(WebProjectPath, "Areas", "Admin", "Views", "Orders", "Index.cshtml"));
        var controller = File.ReadAllText(Path.Combine(WebProjectPath, "Areas", "Admin", "Controllers", "OrdersController.cs"));

        Assert.Contains("method=\"get\"", view);
        foreach (var filter in new[] { "name=\"q\"", "name=\"fromDate\"", "name=\"toDate\"", "name=\"status\"", "name=\"wmcStatus\"" })
            Assert.Contains(filter, view);
        Assert.Contains("Limpar filtros", view);
        Assert.Contains("Total do recorte", view);
        Assert.Contains("Aguardando a&ccedil;&atilde;o", view);
        Assert.Contains("TXT aguardando confirma&ccedil;&atilde;o", view);
        Assert.Contains("Aten&ccedil;&atilde;o WMC", view);
        Assert.Contains("asp-route-q=\"@queryText\"", view);
        Assert.Contains("asp-route-status=\"@statusFilter\"", view);
        Assert.Contains("asp-route-fromDate=\"@RouteDate(fromDate)\"", view);
        Assert.Contains("asp-route-toDate=\"@RouteDate(toDate)\"", view);
        Assert.Contains("asp-route-wmcStatus=\"@wmcStatusFilter\"", view);
        Assert.Contains("asp-route-scope=\"@scope\"", view);
        Assert.Contains("asp-route-sort=\"@sort\"", view);
        Assert.Contains("asp-route-direction=\"@direction\"", view);
        Assert.Contains("asp-route-page=\"@(paged.Page - 1)\"", view);
        Assert.Contains("asp-route-page=\"@currentPage\"", view);
        Assert.Contains("asp-route-page=\"@(paged.Page + 1)\"", view);
        Assert.Contains("ViewBag.PagedResult = new PagedResult<Order>(orders, request.Page, request.PageSize, totalItems)", controller);
    }

    [Fact]
    public void Order_monitor_keeps_the_existing_wmc_classifications_and_responsive_theme_rules()
    {
        var view = File.ReadAllText(Path.Combine(WebProjectPath, "Areas", "Admin", "Views", "Orders", "Index.cshtml"));
        var styles = File.ReadAllText(Path.Combine(WebProjectPath, "wwwroot", "css", "admin-orders-list.css"));

        Assert.Contains("WmcOrderExportStatus.Exported", view);
        Assert.Contains("WmcOrderExportStatus.Imported", view);
        Assert.Contains("WmcOrderExportStatus.Rejected", view);
        Assert.Contains("WmcOrderExportStatus.Error", view);
        Assert.Contains("orders-delivered-wmc-note", view);
        Assert.Contains("orders-row--delivered-wmc-pending", styles);
        Assert.Contains("@media (max-width: 767px)", styles);
        Assert.Contains("html[data-theme=\"dark\"]", styles);
    }
}
