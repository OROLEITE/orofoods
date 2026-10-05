namespace Orofoods.Web.Tests.Views;

public class PortalDarkThemeTests
{
    [Fact]
    public void Portal_dark_styles_are_loaded_only_for_portal_and_customer_selection_pages()
    {
        var projectPath = GetWebProjectPath();
        var layout = ReadText(Path.Combine(projectPath, "Views", "Shared", "_Layout.cshtml"));
        var selectCustomer = ReadText(Path.Combine(projectPath, "Views", "Portal", "SelectCustomer.cshtml"));

        Assert.Contains("portal-dark.css", layout);
        Assert.Contains("portal-customer-select", layout);
        Assert.Contains("ViewData[\"BodyClass\"] = \"portal-customer-select\"", selectCustomer);
    }

    [Fact]
    public void Portal_dark_styles_use_the_approved_palette_and_scoped_surfaces()
    {
        var projectPath = GetWebProjectPath();
        var styles = ReadText(Path.Combine(projectPath, "wwwroot", "css", "portal-dark.css"));

        Assert.Contains("--portal-dark-canvas: #0F172A", styles);
        Assert.Contains("--portal-dark-surface: #111827", styles);
        Assert.Contains("--portal-dark-surface-secondary: #1E293B", styles);
        Assert.Contains("--portal-dark-border: #334155", styles);
        Assert.Contains("--portal-dark-text-primary: #F8FAFC", styles);
        Assert.Contains("--portal-dark-text-secondary: #CBD5E1", styles);
        Assert.Contains("--portal-dark-text-muted: #94A3B8", styles);
        Assert.Contains("--portal-dark-primary: #2563EB", styles);
        Assert.Contains("--portal-dark-primary-hover: #1D4ED8", styles);
        Assert.Contains("body.portal-authenticated", styles);
        Assert.Contains("body.portal-customer-select", styles);
        Assert.DoesNotContain(".admin-authenticated", styles);
        Assert.DoesNotContain(".customer-experience", styles);
        Assert.Contains("body.portal-authenticated .site-header-modern", styles);
        Assert.Contains("body.portal-customer-select .site-header-modern", styles);
        Assert.DoesNotContain(".portal-app-sidebar", styles);
        Assert.DoesNotContain(".page-hero", styles);
    }

    [Fact]
    public void Customer_selection_page_uses_the_dark_canvas_behind_its_card()
    {
        var projectPath = GetWebProjectPath();
        var styles = ReadText(Path.Combine(projectPath, "wwwroot", "css", "portal-dark.css"));

        Assert.Matches(
            @"body\.portal-customer-select \.site-main \.order-page\s*\{[^}]*background:\s*var\(--portal-dark-canvas\);",
            styles);
    }

    [Fact]
    public void Authenticated_portal_dashboard_uses_dark_surfaces_and_legible_chart_and_order_text()
    {
        var projectPath = GetWebProjectPath();
        var styles = ReadText(Path.Combine(projectPath, "wwwroot", "css", "portal-dark.css"));

        Assert.Matches(
            @"body\.portal-authenticated \.portal-app-main \.portal-main-inner\s*\{[^}]*background:\s*var\(--portal-dark-canvas\);",
            styles);
        Assert.Matches(
            @"body\.portal-authenticated \.portal-app-main \.purchase-chart-track\s*\{[^}]*background:\s*var\(--portal-dark-surface-secondary\);",
            styles);
        Assert.Matches(
            @"body\.portal-authenticated \.portal-app-main \.customer-last-order-meta strong,\s*body\.portal-authenticated \.portal-app-main \.customer-last-order-total strong\s*\{[^}]*color:\s*var\(--portal-dark-text-primary\);",
            styles);
    }

    [Fact]
    public void Authenticated_portal_catalog_loads_dark_styles_after_page_styles_and_keeps_buying_surfaces_legible()
    {
        var projectPath = GetWebProjectPath();
        var layout = ReadText(Path.Combine(projectPath, "Views", "Shared", "_Layout.cshtml"));
        var styles = ReadText(Path.Combine(projectPath, "wwwroot", "css", "portal-dark.css"));

        Assert.True(
            layout.IndexOf("@await RenderSectionAsync(\"Head\"", StringComparison.Ordinal) <
            layout.IndexOf("portal-dark.css", StringComparison.Ordinal));
        Assert.Matches(
            @"body\.portal-authenticated \.portal-app-main \.portal-catalog-hero\s*\{[^}]*background:\s*var\(--portal-dark-surface\);",
            styles);
        Assert.Matches(
            @"body\.portal-authenticated \.portal-app-main \.portal-catalog-page \.catalog-price strong\s*\{[^}]*color:\s*var\(--portal-dark-text-primary\);",
            styles);
        Assert.Matches(
            @"body\.portal-authenticated \.portal-app-main \.portal-catalog-page \.catalog-quantity-stepper \.form-control\s*\{[^}]*background:\s*var\(--portal-dark-surface-secondary\);",
            styles);
    }

    [Fact]
    public void Authenticated_portal_cart_uses_dark_hero_and_empty_state_surfaces()
    {
        var projectPath = GetWebProjectPath();
        var styles = ReadText(Path.Combine(projectPath, "wwwroot", "css", "portal-dark.css"));

        Assert.Matches(
            @"body\.portal-authenticated \.portal-app-main \.portal-page-hero\s*\{[^}]*background:\s*var\(--portal-dark-surface\);",
            styles);
        Assert.Matches(
            @"body\.portal-authenticated \.portal-app-main \.portal-page-hero h1\s*\{[^}]*color:\s*var\(--portal-dark-text-primary\);",
            styles);
        Assert.Matches(
            @"body\.portal-authenticated \.portal-app-main \.empty-state\s*\{[^}]*background:\s*var\(--portal-dark-surface\);",
            styles);
        Assert.Matches(
            @"body\.portal-authenticated \.portal-app-main \.empty-state h2\s*\{[^}]*color:\s*var\(--portal-dark-text-primary\);",
            styles);
    }

    private static string GetWebProjectPath() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));

    private static string ReadText(string path) => File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
}
