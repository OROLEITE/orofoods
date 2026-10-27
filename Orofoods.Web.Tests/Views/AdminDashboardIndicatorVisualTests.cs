namespace Orofoods.Web.Tests.Views;

public class AdminDashboardIndicatorVisualTests
{
    [Fact]
    public void Dashboard_indicator_sections_use_reusable_severity_classes_without_changing_navigation_or_values()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Dashboard", "Index.cshtml"));

        Assert.Contains("admin-dashboard-indicator--normal", view);
        Assert.Contains("admin-dashboard-indicator--warning", view);
        Assert.Contains("admin-dashboard-indicator--critical", view);
        Assert.Contains("asp-controller=\"Orders\" asp-action=\"Index\" asp-route-scope=\"awaiting-action\"", view);
        Assert.Contains("@Model.OrdersTodayAwaitingAction", view);
        Assert.Contains("@Model.MonthRevenue.ToString(\"C\")", view);
        Assert.Contains("@(Model.FailedIntegrations + Model.FailedWmcExports)", view);
        Assert.Contains("asp-controller=\"Reports\"", view);
        Assert.True(view.Split("admin-dashboard-indicator", StringSplitOptions.None).Length - 1 >= 18);
    }

    [Fact]
    public void Dashboard_indicator_styles_share_compact_palette_and_dark_surface_across_sections()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var layout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_AdminLayout.cshtml"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-dashboard-indicators.css"));

        Assert.Contains("admin-dashboard-indicators.css", layout);
        Assert.Contains("border-left: 4px solid", styles);
        Assert.Contains("border-radius: 14px", styles);
        Assert.Contains("#38BDF8", styles);
        Assert.Contains("#F59E0B", styles);
        Assert.Contains("#F87171", styles);
        Assert.Contains("#111827", styles);
        Assert.Contains("font-size: clamp(1.35rem, 1.8vw, 1.625rem);", styles);
        Assert.Contains("@media (max-width: 650px)", styles);
        Assert.DoesNotMatch(@"\.admin-dashboard-indicator\s*\{[^}]*\bheight\s*:", styles);
    }

    [Fact]
    public void Admin_indicator_numbers_share_compact_typography_and_priority_arrows_follow_severity()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-dashboard-indicators.css"));

        Assert.Contains("font-size: clamp(1.35rem, 1.8vw, 1.625rem);", styles);
        Assert.Contains("font-weight: 800;", styles);
        Assert.Contains("font-variant-numeric: tabular-nums;", styles);
        Assert.Contains("text-align: left;", styles);
        Assert.Contains(".admin-priority-card span > i", styles);
        Assert.Contains("color: var(--dashboard-indicator-accent);", styles);
    }
}
