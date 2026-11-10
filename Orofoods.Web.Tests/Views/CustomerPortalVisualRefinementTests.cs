namespace Orofoods.Web.Tests.Views;

public class CustomerPortalVisualRefinementTests
{
    [Fact]
    public void Catalog_keeps_the_existing_post_and_adds_a_minimum_aware_quantity_stepper()
    {
        var projectPath = GetWebProjectPath();
        var view = ReadText(Path.Combine(projectPath, "Views", "Portal", "Catalog.cshtml"));
        var script = ReadText(Path.Combine(projectPath, "wwwroot", "js", "catalog.js"));

        Assert.Contains("asp-action=\"AddToCart\"", view);
        Assert.DoesNotContain("catalog-details-link", view);
        Assert.Contains("min=\"@product.MinimumSalesQuantity\"", view);
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
        var styles = ReadText(Path.Combine(projectPath, "wwwroot", "css", "catalog.css"));

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
    public void Portal_catalog_keeps_its_dark_palette_and_does_not_crop_product_images()
    {
        var projectPath = GetWebProjectPath();
        var catalogStyles = ReadText(Path.Combine(projectPath, "wwwroot", "css", "catalog.css"));
        var darkStyles = ReadText(Path.Combine(projectPath, "wwwroot", "css", "portal-dark.css"));

        Assert.DoesNotMatch(
            @"(?s)(?:\.public-products-hero,\s*)?\.portal-catalog-hero\s*\{[^}]*background:\s*#F1F5FB\s*!important",
            catalogStyles);
        Assert.Contains("body.portal-authenticated .portal-app-main .portal-catalog-page", darkStyles);
        Assert.Contains("background: #0B132B;", darkStyles);
        Assert.Contains("body.portal-authenticated .portal-app-main .portal-catalog-hero", darkStyles);
        Assert.Matches(
            @"(?s)body\.portal-authenticated \.portal-app-main \.portal-catalog-hero\s*\{[^}]*background:\s*var\(--portal-dark-surface\);",
            darkStyles);
        Assert.Contains("color: #E5E7EB;", darkStyles);
        Assert.Contains("grid-template-columns: repeat(5, minmax(0, 1fr));", catalogStyles);
        Assert.Contains("object-fit: contain;", darkStyles);
        Assert.Contains("object-position: center;", darkStyles);
        Assert.Contains("transform: none;", darkStyles);
        Assert.DoesNotContain("object-fit:cover", catalogStyles);
    }

    [Fact]
    public void Portal_catalog_search_is_live_debounced_and_keeps_server_filters_and_identifier_matching()
    {
        var projectPath = GetWebProjectPath();
        var view = ReadText(Path.Combine(projectPath, "Views", "Portal", "Catalog.cshtml"));
        var script = ReadText(Path.Combine(projectPath, "wwwroot", "js", "portal-catalog-search.js"));
        var catalogInteractions = ReadText(Path.Combine(projectPath, "wwwroot", "js", "catalog.js"));
        var controller = ReadText(Path.Combine(projectPath, "Controllers", "PortalController.cs"));

        Assert.Contains("data-portal-catalog-search", view);
        Assert.Contains("data-portal-catalog-grid", view);
        Assert.Contains("autocomplete=\"off\"", view);
        Assert.Contains("data-portal-catalog-results-status", view);
        Assert.Contains("searchForm.addEventListener('submit'", script);
        Assert.Contains("searchInput.addEventListener('input'", script);
        Assert.Contains("categorySelect?.addEventListener('change'", script);
        Assert.Contains("brandSelect?.addEventListener('change'", script);
        Assert.Contains("setTimeout", script);
        Assert.Contains("200", script);
        Assert.Contains("AbortController", script);
        Assert.Contains("X-Requested-With", script);
        Assert.Contains("DOMParser", script);
        Assert.Contains("replaceChildren", script);
        Assert.Contains("window.initializeCatalogProductInteractions(searchGrid)", script);
        Assert.Contains("window.initializeCatalogProductInteractions = initializeCatalogProductInteractions", catalogInteractions);
        Assert.Contains("~/js/portal-catalog-search.js", view);
        Assert.Contains("product.WmcCode", controller);
        Assert.Contains("product.Ean", controller);
        Assert.Contains("product.Ean13", controller);
        Assert.Contains("product.Sku", controller);
        Assert.Contains("product.Brand", controller);
        Assert.Contains("product.Name", controller);

        var normalizer = ReadText(Path.Combine(projectPath, "Services", "Catalog", "CatalogSearchText.cs"));
        Assert.Contains("NormalizationForm.FormD", normalizer);
        Assert.Contains("ToUpperInvariant()", normalizer);
    }

    [Fact]
    public void Portal_catalog_uses_transparent_product_copies_for_all_matching_wmc_codes()
    {
        var projectPath = GetWebProjectPath();
        var view = ReadText(Path.Combine(projectPath, "Views", "Portal", "Catalog.cshtml"));
        var styles = ReadText(Path.Combine(projectPath, "wwwroot", "css", "portal-dark.css"));
        var imageCatalog = ReadText(Path.Combine(projectPath, "Services", "Catalog", "BimboProductImageCatalog.cs"));
        var transparentImages = new[]
        {
            (Code: "503768", File: "503768_corte_duplo"),
            (Code: "503775", File: "503775_tradicional_gergelim"),
            (Code: "503777", File: "503777_smart_gergelim"),
            (Code: "503805", File: "503805_tradicional"),
            (Code: "503811", File: "503811_hot_dog"),
            (Code: "503813", File: "503813_smart"),
            (Code: "503833", File: "503833_brioche_class"),
            (Code: "503838", File: "503838_mini_brioche"),
            (Code: "504338", File: "504338_brioche_max"),
            (Code: "504342", File: "504342_australiano")
        };

        Assert.Contains("var catalogImageUrl = officialImage?.ImageUrl", view);
        Assert.Contains("image is not null ? Url.RouteUrl(\"ProductMedia\"", view);
        Assert.DoesNotContain("portalTransparentProductImages", view);
        Assert.DoesNotMatch(
            @"(?s)body\.portal-authenticated \.portal-app-main \.portal-catalog-page \.product-visual\s*\{[^}]*background:\s*#FFFFFF",
            styles);

        foreach (var (wmcCode, fileName) in transparentImages)
        {
            Assert.Contains($"[\"{wmcCode}\"] = new(", imageCatalog, StringComparison.Ordinal);
            Assert.Contains($"/images/products/bimbo-catalog-transparent/{fileName}.png", imageCatalog, StringComparison.Ordinal);
            var assetPath = Path.Combine(projectPath, "wwwroot", "images", "products", "bimbo-catalog-transparent", $"{fileName}.png");
            Assert.True(File.Exists(assetPath), $"Portal transparent image for WMC {wmcCode} is missing.");
            var png = File.ReadAllBytes(assetPath);
            Assert.True(png.Length > 26, $"Portal transparent image for WMC {wmcCode} is incomplete.");
            Assert.Equal((byte)6, png[25]);
        }
    }

    [Fact]
    public void Portal_navigation_inherits_admin_typography_and_has_no_broad_span_highlight_rule()
    {
        var projectPath = GetWebProjectPath();
        var portal = ReadText(Path.Combine(projectPath, "wwwroot", "css", "portal-navigation.css"));
        var density = ReadText(Path.Combine(projectPath, "wwwroot", "css", "portal-density.css"));
        var admin = ReadText(Path.Combine(projectPath, "wwwroot", "css", "admin-navigation.css"));

        Assert.Contains("font: 600 15px 'DM Sans', sans-serif", admin);
        Assert.Contains("font: 600 15px 'DM Sans', sans-serif", portal);
        Assert.Contains("flex: 0 0 24px", portal);
        Assert.Contains("width: 24px", portal);
        Assert.Contains("font-size: 19px", portal);
        Assert.Contains(".portal-app-nav a.active {\n    background: var(--nav-item-hover);\n    color: var(--nav-text-active);\n}", portal);
        Assert.Contains(".portal-app-nav a.active::before {", portal);
        Assert.Contains("    background: #38BDF8;", portal);
        Assert.Contains("width: 3px", portal);
        Assert.DoesNotContain(".portal-app-nav a span{", density);
        Assert.DoesNotContain(".portal-app-nav a.active span{", density);
    }

    [Fact]
    public void Customer_header_uses_shared_visual_rules_without_portal_overrides_or_logo_effects()
    {
        var projectPath = GetWebProjectPath();
        var portal = ReadText(Path.Combine(projectPath, "wwwroot", "css", "portal-navigation.css"));
        var customer = ReadText(Path.Combine(projectPath, "wwwroot", "css", "customer-experience.css"));
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
        var portal = ReadText(Path.Combine(projectPath, "wwwroot", "css", "portal-navigation.css"));

        Assert.Contains(".portal-sidebar-collapsed .portal-company {\n    display: none;\n}", portal);
        Assert.DoesNotContain(".portal-company small::first-letter", portal);
    }

    private static string GetWebProjectPath() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));

    private static string ReadText(string path) => File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
}
