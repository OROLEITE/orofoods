namespace Orofoods.Web.Tests.Views;

public class AdminDashboardPriorityVisualTests
{
    [Fact]
    public void Priority_cards_are_compact_and_integration_failures_use_only_a_discreet_red_accent_when_nonzero()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Dashboard", "Index.cshtml"));
        var baseCss = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-dashboard.css"));
        var css = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-dashboard-priority.css"));
        var indicatorCss = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-dashboard-indicators.css"));

        Assert.Contains("Model.FailedIntegrations + Model.FailedWmcExports == 0 ? \"admin-dashboard-indicator--normal\" : \"admin-dashboard-indicator--critical\"", view);
        Assert.Contains("Model.LowStockProducts == 0 ? \"admin-dashboard-indicator--normal\" : \"admin-dashboard-indicator--warning\"", view);
        Assert.Contains("min-height: 0;", indicatorCss);
        Assert.Contains("padding: 9px 12px;", indicatorCss);
        Assert.Contains("margin: 1px 0 2px;", indicatorCss);
        Assert.Contains(".admin-priority-card span > i", indicatorCss);
        Assert.Contains("color: var(--dashboard-indicator-accent);", indicatorCss);
        Assert.Contains(".admin-priority-card {\n        padding: 9px 12px;\n    }", indicatorCss);
        Assert.Contains("border-left: 4px solid var(--dashboard-indicator-accent);", indicatorCss);
        Assert.Contains("--dashboard-indicator-accent: #F87171;", indicatorCss);
        Assert.Contains("--dashboard-indicator-accent: #F59E0B;", indicatorCss);
        Assert.Contains("background: #111827;", indicatorCss);
        Assert.Contains("grid-template-columns: repeat(4, minmax(0, 1fr));", baseCss);
        Assert.Contains("@media (max-width: 650px)", css);
        Assert.DoesNotMatch(@"(?:^|[;\r\n])\s*height\s*:", css);
    }
}
