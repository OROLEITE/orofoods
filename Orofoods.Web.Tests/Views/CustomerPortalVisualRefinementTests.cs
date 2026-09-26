namespace Orofoods.Web.Tests.Views;

public class CustomerPortalVisualRefinementTests
{
    [Fact]
    public void Catalog_keeps_the_existing_post_and_adds_a_minimum_aware_quantity_stepper()
    {
        var projectPath = GetWebProjectPath();
        var view = File.ReadAllText(Path.Combine(projectPath, "Views", "Portal", "Catalog.cshtml"));
        var script = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "js", "catalog.js"));

        Assert.Contains("asp-action=\"AddToCart\"", view);
        Assert.Contains("asp-action=\"Product\"", view);
        Assert.Contains("min=\"@product.MinimumCases\"", view);
        Assert.Contains("data-catalog-quantity-stepper", view);
        Assert.Contains("data-catalog-quantity-decrease", view);
        Assert.Contains("data-catalog-quantity-increase", view);
        Assert.Contains("data-catalog-quantity-decrease", script);
        Assert.Contains("data-catalog-quantity-increase", script);
        Assert.Contains("Math.max(minimum, Number(input.value) - 1)", script);
        Assert.Contains("Number(input.value) + 1", script);
        Assert.Contains("input.addEventListener('change', normalizeQuantity)", script);
    }

    [Fact]
    public void Catalog_layout_uses_compact_hero_and_the_requested_product_column_breakpoints()
    {
        var projectPath = GetWebProjectPath();
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "catalog.css"));

        Assert.Contains(".customer-experience .portal-catalog-hero", styles);
        Assert.Contains("repeat(5, minmax(0, 1fr))", styles);
        Assert.Contains("@media (min-width: 1100px) and (max-width: 1399.98px)", styles);
        Assert.Contains("@media (min-width: 768px) and (max-width: 1099.98px)", styles);
        Assert.Contains("@media (min-width: 576px) and (max-width: 767.98px)", styles);
        Assert.Contains("@media (max-width: 575.98px)", styles);
        Assert.Contains("object-fit: contain;", styles);
        Assert.Contains("grid-template-columns: 40px minmax(48px, 74px) 40px;", styles);
        Assert.Contains("min-height: 42px;", styles);
    }

    [Fact]
    public void Portal_navigation_inherits_admin_typography_and_has_no_broad_span_highlight_rule()
    {
        var projectPath = GetWebProjectPath();
        var portal = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "portal-navigation.css"));
        var density = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "portal-density.css"));
        var admin = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-navigation.css"));

        Assert.Contains("font: 600 15px 'DM Sans', sans-serif", admin);
        Assert.Contains("font: 600 15px 'DM Sans', sans-serif", portal);
        Assert.Contains("flex: 0 0 24px", portal);
        Assert.Contains("width: 24px", portal);
        Assert.Contains("font-size: 19px", portal);
        Assert.Contains("background: rgba(201, 149, 40, .12)", portal);
        Assert.Contains("width: 3px", portal);
        Assert.DoesNotContain(".portal-app-nav a span{", density);
        Assert.DoesNotContain(".portal-app-nav a.active span{", density);
    }

    [Fact]
    public void Customer_header_uses_shared_visual_rules_without_portal_overrides_or_logo_effects()
    {
        var projectPath = GetWebProjectPath();
        var portal = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "portal-navigation.css"));
        var customer = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "customer-experience.css"));
        var logoRule = System.Text.RegularExpressions.Regex.Match(customer, @"(?s)\.customer-experience \.site-logo\s*\{([^}]*)\}").Groups[1].Value;

        Assert.Contains(".customer-experience .site-header-modern", customer);
        Assert.Contains(".customer-experience .main-nav .nav-link.active", customer);
        Assert.DoesNotContain(".portal-authenticated .site-header-modern .main-nav .nav-link", portal);
        Assert.DoesNotContain(".portal-authenticated .site-header-modern .account-trigger", portal);
        Assert.DoesNotContain("background", logoRule.ToLowerInvariant());
        Assert.DoesNotContain("filter", logoRule.ToLowerInvariant());
    }

    [Fact]
    public void Collapsed_portal_sidebar_hides_company_identity_and_has_no_initial_letter_rule()
    {
        var projectPath = GetWebProjectPath();
        var portal = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "portal-navigation.css"));

        Assert.Contains(".portal-sidebar-collapsed .portal-company {\n    display: none;\n}", portal);
        Assert.DoesNotContain(".portal-company small::first-letter", portal);
    }

    private static string GetWebProjectPath() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
}
