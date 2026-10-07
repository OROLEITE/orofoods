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

        Assert.Contains("Model.FailedIntegrations + Model.FailedWmcExports == 0 ? \"\" : \"admin-priority-card--danger\"", view);
        Assert.Contains("class=\"admin-priority-card @(Model.LowStockProducts == 0 ? \"\" : \"admin-priority-card--accent\")\"", view);
        Assert.Contains("min-height: 112px;", css);
        Assert.Contains("padding: 12px;", css);
        Assert.Contains("margin-top: 7px;", css);
        Assert.Contains("margin: 2px 0 4px;", css);
        Assert.Contains("box-shadow: inset 3px 0 0 #A45149;", css);
        Assert.Contains("background: #F8ECEB;", css);
        Assert.Contains("background: #3A2528;", css);
        Assert.Contains("color: #FCA5A5;", css);
        Assert.Contains("grid-template-columns: repeat(4, minmax(0, 1fr));", baseCss);
        Assert.Contains("@media (max-width: 650px)", css);
        Assert.DoesNotMatch(@"(?:^|[;\r\n])\s*height\s*:", css);
    }
}
