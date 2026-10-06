namespace Orofoods.Web.Tests.Views;

public class AdminDashboardThemeTests
{
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
        Assert.Contains("min-height:280px;", dashboardStyles);
        Assert.Contains("border-radius:14px;", dashboardStyles);
        Assert.Contains("linear-gradient", dashboardStyles);
        Assert.Contains("@media (max-width: 767px)", dashboardStyles);
        Assert.Contains("flex-direction:column;", dashboardStyles);
        Assert.Contains("html[data-theme=\"dark\"] .admin-priority-card:hover", dashboardStyles);
        Assert.Contains("html[data-theme=\"dark\"] .admin-dashboard-page .admin-orders-table thead th", dashboardStyles);
    }
}
