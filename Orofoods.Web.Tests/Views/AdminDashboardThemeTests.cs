namespace Orofoods.Web.Tests.Views;

public class AdminDashboardThemeTests
{
    [Fact]
    public void Dashboard_hero_uses_singular_order_label_only_for_one_order()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Dashboard", "Index.cshtml"));

        Assert.Contains("@Model.OrdersToday @(Model.OrdersToday == 1 ? \"pedido\" : \"pedidos\")", view);
    }

    [Fact]
    public void Dark_admin_dashboard_sets_a_dark_canvas_and_compact_responsive_hero()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var shellStyles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-shell.css"));
        var dashboardStyles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-dashboard.css"));

        Assert.Contains("html[data-theme=\"dark\"] .admin-shell", shellStyles);
        Assert.Contains("html[data-theme=\"dark\"] .admin-dashboard-page", dashboardStyles);
        Assert.Contains("background-color:#0F172A;", dashboardStyles);
        Assert.Contains("html[data-theme=\"dark\"] .admin-dashboard-hero .container", dashboardStyles);
        Assert.Contains("min-height: 168px;", dashboardStyles);
        Assert.DoesNotContain("min-height:280px;", dashboardStyles);
        Assert.Contains("border-radius: 14px", dashboardStyles);
        Assert.Contains("linear-gradient", dashboardStyles);
        Assert.Contains("@media (max-width: 767px)", dashboardStyles);
        Assert.Contains("flex-direction:column;", dashboardStyles);
        Assert.Contains("html[data-theme=\"dark\"] .admin-priority-card:hover", dashboardStyles);
        Assert.Contains("html[data-theme=\"dark\"] .admin-dashboard-page .admin-orders-table thead th", dashboardStyles);
    }

    [Fact]
    public void Dashboard_priority_cards_reuse_the_existing_accent_for_nonzero_counts()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Dashboard", "Index.cshtml"));
        var dashboardStyles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-dashboard.css"));

        Assert.Contains("Model.PendingCustomers == 0 ? \"\" : \"admin-priority-card--accent\"", view);
        Assert.Contains("Model.PendingOrders == 0 ? \"\" : \"admin-priority-card--accent\"", view);
        Assert.Contains("Model.LowStockProducts == 0 ? \"\" : \"admin-priority-card--accent\"", view);
        Assert.Contains("Model.FailedIntegrations + Model.FailedWmcExports == 0 ? \"\" : \"admin-priority-card--danger\"", view);
        Assert.DoesNotContain("admin-priority-card--critical", view + dashboardStyles);
    }

    [Fact]
    public void Dashboard_cards_share_compact_geometry_and_scoped_theme_tokens()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var dashboardStyles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-dashboard.css"));

        Assert.Matches(@"\.admin-authenticated \.admin-dashboard-page \.admin-priority-card,\s*\.admin-authenticated \.admin-dashboard-page \.dashboard-kpi-card,\s*\.admin-authenticated \.admin-dashboard-page \.admin-recent-orders\s*\{(?=[^}]*border-radius:\s*14px)(?=[^}]*padding:\s*14px)(?=[^}]*border:\s*1px solid)", dashboardStyles);
        Assert.Contains("--dashboard-card-bg: var(--admin-dark-surface)", dashboardStyles);
        Assert.Contains("font-size: 21px", dashboardStyles);
        Assert.Contains("letter-spacing: -.4px", dashboardStyles);
        Assert.Contains("@media (max-width: 1000px)", dashboardStyles);
        Assert.Contains("@media (max-width: 650px)", dashboardStyles);
    }

    [Fact]
    public void Dashboard_operation_section_uses_existing_status_labels_and_compact_responsive_grids()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Dashboard", "Index.cshtml"));
        var dashboardStyles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-dashboard.css"));

        var prioritiesIndex = view.IndexOf("O que exige aten&ccedil;&atilde;o agora", StringComparison.Ordinal);
        var operationIndex = view.IndexOf("Opera&ccedil;&atilde;o de hoje", StringComparison.Ordinal);
        var commercialIndex = view.IndexOf("Resultado do m&ecirc;s", StringComparison.Ordinal);

        Assert.True(prioritiesIndex >= 0 && prioritiesIndex < operationIndex && operationIndex < commercialIndex);
        Assert.Contains("Acompanhe o fluxo dos pedidos do dia.", view);
        Assert.Contains("OrderStatus.UnderReview.ToDisplayName()", view);
        Assert.Contains("OrderStatus.Approved.ToDisplayName()", view);
        Assert.Contains("OrderStatus.Picking.ToDisplayName()", view);
        Assert.Contains("OrderStatus.Invoiced.ToDisplayName()", view);
        Assert.Contains("OrderStatus.OutForDelivery.ToDisplayName()", view);
        Assert.Contains("OrderStatus.Delivered.ToDisplayName()", view);
        Assert.Contains("Entregues hoje", view);
        Assert.Contains("OrdersTodayTotal", view);
        Assert.Contains("OrdersTodayAwaitingAction", view);
        Assert.Contains("OrdersDeliveredToday", view);
        Assert.Contains("grid-template-columns: repeat(4, minmax(0, 1fr))", dashboardStyles);
        Assert.Contains("@media (max-width: 1000px)", dashboardStyles);
        Assert.Contains("@media (max-width: 650px)", dashboardStyles);
    }
}
