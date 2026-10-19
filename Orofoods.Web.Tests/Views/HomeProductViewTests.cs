namespace Orofoods.Web.Tests.Views;

public class HomeProductViewTests
{
    private static readonly string ProjectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));

    [Fact]
    public void Product_detail_groups_customer_actions_with_bootstrap_spacing()
    {
        var markup = File.ReadAllText(Path.Combine(ProjectPath, "Views", "Home", "Product.cshtml"));

        Assert.Contains("product-detail-actions d-flex", markup);
        Assert.Contains("gap-2", markup);
    }

    [Fact]
    public void Home_hero_prioritizes_product_access_with_a_cyan_primary_button_and_a_secondary_portal_link()
    {
        var markup = File.ReadAllText(Path.Combine(ProjectPath, "Views", "Home", "Index.cshtml"));

        Assert.Contains("public-home-primary-action\" asp-controller=\"Home\" asp-action=\"Products\">Comprar produtos", markup);
        Assert.Contains("public-home-secondary-action\" href=\"@portalUrl\">Meu portal", markup);
        Assert.DoesNotContain("public-home-bimbo-banner-placeholder", markup);
        Assert.DoesNotContain("BIMBO", markup);
    }

    [Fact]
    public void Home_uses_the_shared_white_header_and_the_nav_has_no_home_only_overrides()
    {
        var layout = File.ReadAllText(Path.Combine(ProjectPath, "Views", "Shared", "_Layout.cshtml"));
        var header = File.ReadAllText(Path.Combine(ProjectPath, "Views", "Shared", "_SiteHeader.cshtml"));
        var homeStyles = File.ReadAllText(Path.Combine(ProjectPath, "wwwroot", "css", "home-showcase.css"));

        Assert.Equal(1, layout.Split("PartialAsync(\"_SiteHeader\")", StringSplitOptions.None).Length - 1);
        Assert.Contains("navbar navbar-expand-lg navbar-light bg-white", header);
        Assert.Contains("logo-orofoods-transparent.png", header);
        Assert.True(File.Exists(Path.Combine(ProjectPath, "wwwroot", "images", "logo-orofoods-transparent.png")));
        Assert.DoesNotContain(".public-home-page .site-header-modern", homeStyles);
        Assert.DoesNotContain(".navbar", homeStyles);
        Assert.DoesNotContain(".nav-link", homeStyles);
        Assert.DoesNotContain("#0B132B !important", homeStyles);
    }

    [Fact]
    public void Home_keeps_the_public_footer_scope_and_exposes_the_page_class()
    {
        var layout = File.ReadAllText(Path.Combine(ProjectPath, "Views", "Shared", "_Layout.cshtml"));
        var homeStyles = File.ReadAllText(Path.Combine(ProjectPath, "wwwroot", "css", "home-showcase.css"));

        Assert.Contains("isPublicHomePage", layout);
        Assert.Contains("public-home-page", layout);
        Assert.Contains(".public-home-page .site-footer", homeStyles);
        Assert.Contains("outline: 3px solid", homeStyles);
    }

    [Fact]
    public void Home_featured_cards_reuse_the_shared_catalog_button_treatment()
    {
        var catalogStyles = File.ReadAllText(Path.Combine(ProjectPath, "wwwroot", "css", "catalog.css")).Replace("\r\n", "\n", StringComparison.Ordinal);
        var home = File.ReadAllText(Path.Combine(ProjectPath, "Views", "Home", "Index.cshtml"));

        Assert.Contains("catalog.css", home);
        Assert.Contains("public-home-featured public-products-section", home);
        Assert.Contains(".public-products-section .quick-buy-button,", catalogStyles);
        Assert.Contains("background: #0E7490;", catalogStyles);
        Assert.Contains("min-height: 44px;", catalogStyles);
    }

    [Fact]
    public void Product_detail_stays_public_and_Admin_uses_the_existing_customer_portal()
    {
        var markup = File.ReadAllText(Path.Combine(ProjectPath, "Views", "Home", "Product.cshtml"));
        var controller = File.ReadAllText(Path.Combine(ProjectPath, "Controllers", "HomeController.cs"));

        Assert.Contains("User.IsInRole(\"Administrador\")", markup);
        Assert.Contains("Comprar para o cliente selecionado", markup);
        Assert.DoesNotContain("RedirectToAction(\"Product\", \"Portal\", new { id })", controller);
        Assert.Contains("GetApprovedCustomerAsync", controller);
        Assert.Contains("customerPurchaseAuthorized", markup);
        Assert.Contains("asp-controller=\"Portal\" asp-action=\"Product\"", markup);
    }

    [Fact]
    public void Product_purchase_links_use_existing_customer_login_and_seller_order_entry_flows()
    {
        var markup = File.ReadAllText(Path.Combine(ProjectPath, "Views", "Home", "Product.cshtml"));
        var portalDetail = File.ReadAllText(Path.Combine(ProjectPath, "Views", "Portal", "Product.cshtml"));

        Assert.Contains("asp-page=\"/Account/Login\"", markup);
        Assert.Contains("asp-route-returnUrl=\"@loginReturnUrl\"", markup);
        Assert.Contains("asp-controller=\"OrderDrafts\" asp-action=\"New\"", markup);
        Assert.Contains("asp-route-customerId=\"@selectedCustomerId\"", markup);
        Assert.Contains("asp-route-productId=\"@Model.Id\"", markup);
        Assert.DoesNotContain("asp-area=\"Vendedor\" asp-controller=\"Cart\"", markup);
        Assert.Contains("sellerProduct.UnitPrice > 0m", markup);
        Assert.Contains("Model.IsCommerciallyAvailable && hasValidPrice", portalDetail);
    }
}
