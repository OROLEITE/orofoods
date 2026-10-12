using Orofoods.Web.Areas.Admin.Controllers;

namespace Orofoods.Web.Tests.Views;

public class AdminDashboardRecentOrdersViewTests
{
    [Fact]
    public void Dashboard_sections_follow_the_requested_final_order()
    {
        var view = ReadDashboardView();
        var sections = new[]
        {
            "Central de controle",
            "O que exige aten&ccedil;&atilde;o agora",
            "Opera&ccedil;&atilde;o de hoje",
            "Resultado do m&ecirc;s",
            "admin-dashboard-integrations",
            "class=\"admin-recent-orders\">"
        };

        var positions = sections.Select(section => view.IndexOf(section, StringComparison.Ordinal)).ToArray();

        Assert.All(positions, position => Assert.True(position >= 0));
        Assert.True(positions.Zip(positions.Skip(1), (current, next) => current < next).All(isOrdered => isOrdered));
    }

    [Fact]
    public void Recent_orders_keep_real_model_data_existing_columns_and_routes_in_responsive_table()
    {
        var view = ReadDashboardView();
        var recentOrdersStart = view.IndexOf("class=\"admin-recent-orders\">", StringComparison.Ordinal);
        var recentOrders = view[recentOrdersStart..];

        Assert.Contains("class=\"btn btn-outline-dark\" asp-controller=\"Orders\">Todos os pedidos", recentOrders);
        Assert.Contains("Model.RecentOrders", recentOrders);
        Assert.Contains("asp-controller=\"Orders\"", recentOrders);
        Assert.Contains("asp-action=\"Details\" asp-route-id=\"@order.Id\"", recentOrders);
        Assert.Contains("<div class=\"table-responsive\">", recentOrders);
        Assert.Contains("<th>Pedido</th>", recentOrders);
        Assert.Contains("<th>Cliente</th>", recentOrders);
        Assert.Contains("<th>Situa&ccedil;&atilde;o</th>", recentOrders);
        Assert.Contains("<th>Total</th>", recentOrders);
        Assert.Contains("<th>Registrado em</th>", recentOrders);
        Assert.NotNull(typeof(OrdersController).GetMethod(nameof(OrdersController.Index)));
        Assert.NotNull(typeof(OrdersController).GetMethod(nameof(OrdersController.Details)));
    }

    [Fact]
    public void Recent_orders_use_compact_spacing_without_losing_responsive_width_constraints()
    {
        var styles = File.ReadAllText(Path.Combine(ProjectPath(), "wwwroot", "css", "admin-dashboard.css"));

        Assert.Matches(
            @"\.admin-authenticated \.admin-dashboard-page \.admin-recent-orders:not\(\.admin-dashboard-integrations\)\s*\{(?=[^}]*padding:\s*12px 14px)(?=[^}]*min-width:\s*0)[^}]*\}",
            styles);
        Assert.Matches(
            @"\.admin-authenticated \.admin-dashboard-page \.admin-recent-orders:not\(\.admin-dashboard-integrations\) \.admin-dashboard-section-head\s*\{(?=[^}]*margin-bottom:\s*8px)(?=[^}]*gap:\s*10px)[^}]*\}",
            styles);
        Assert.Matches(
            @"\.admin-authenticated \.admin-dashboard-page \.admin-recent-orders:not\(\.admin-dashboard-integrations\) \.admin-orders-table thead th,\s*\.admin-authenticated \.admin-dashboard-page \.admin-recent-orders:not\(\.admin-dashboard-integrations\) \.admin-orders-table tbody td\s*\{(?=[^}]*padding:\s*8px 12px)[^}]*\}",
            styles);
    }

    private static string ReadDashboardView() =>
        File.ReadAllText(Path.Combine(ProjectPath(), "Areas", "Admin", "Views", "Dashboard", "Index.cshtml"));

    private static string ProjectPath() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
}
