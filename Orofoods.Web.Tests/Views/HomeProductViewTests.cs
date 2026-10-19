namespace Orofoods.Web.Tests.Views;

public class HomeProductViewTests
{
    [Fact]
    public void Product_detail_groups_customer_actions_with_bootstrap_spacing()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Home", "Product.cshtml"));

        Assert.Contains("product-detail-actions d-flex", markup);
        Assert.Contains("gap-2", markup);
    }

    [Fact]
    public void Home_product_card_opens_the_public_product_detail()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Home", "Index.cshtml"));

        Assert.Contains("class=\"public-home-product-media\" asp-controller=\"Home\" asp-action=\"Product\" asp-route-id=\"@product.Id\"", markup);
        Assert.Contains("class=\"public-home-product-action\" asp-controller=\"Home\" asp-action=\"Product\" asp-route-id=\"@product.Id\"", markup);
    }

    [Fact]
    public void Home_uses_the_institutional_food_photo_without_associating_it_with_a_product_sku()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Home", "Index.cshtml"));

        Assert.Contains("Soluções para o seu Food Service", markup);
        Assert.Contains("~/images/orofoods-hero.png", markup);
        Assert.Contains("Hambúrguer artesanal e pães em uma cozinha profissional", markup);
        Assert.Contains("public-home-primary-action\" asp-controller=\"Home\" asp-action=\"Products\">Conheça nossos produtos", markup);
        Assert.Contains("public-home-secondary-action\" asp-controller=\"CustomerRegistration\" asp-action=\"Register\">Cadastre sua empresa", markup);
        Assert.DoesNotContain("public-home-bimbo-banner-placeholder", markup);
        Assert.DoesNotContain("BIMBO", markup);
    }

    [Fact]
    public void Home_header_has_a_larger_proportional_logo_and_visible_navigation_states()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var homeStyles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "home-showcase.css"));

        Assert.Contains("height: clamp(54px, 4vw, 64px);", homeStyles);
        Assert.Contains("object-fit: contain;", homeStyles);
        Assert.Contains("color: #F8FAFC;", homeStyles);
        Assert.Contains("color: #22D3EE;", homeStyles);
        Assert.Contains("box-shadow: inset 0 -2px 0 var(--home-gold);", homeStyles);
        Assert.Contains("@media (max-width: 767px)", homeStyles);
    }

    [Fact]
    public void Home_branding_is_scoped_to_the_public_home_and_reuses_the_transparent_official_logo()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var layout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_Layout.cshtml"));
        var header = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_SiteHeader.cshtml"));
        var homeStyles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "home-showcase.css"));

        Assert.Contains("isPublicHomePage", layout);
        Assert.Contains("public-home-page", layout);
        Assert.Contains("logo-orofoods-transparent.png", header);
        Assert.Contains("logo-orofoods-transparent.png", layout);
        Assert.True(File.Exists(Path.Combine(projectPath, "wwwroot", "images", "logo-orofoods-transparent.png")));
        Assert.Contains(".public-home-page .site-header-modern", homeStyles);
        Assert.Contains(".public-home-page .site-footer", homeStyles);
        Assert.Contains("#0B132B", homeStyles);
        Assert.Contains("#0E7490", homeStyles);
        Assert.Contains("#D4A017", homeStyles);
        Assert.Contains("focus-visible", homeStyles);
        Assert.Contains("outline: 3px solid #0B132B;", homeStyles);
        Assert.Contains("object-fit: contain", homeStyles);
    }

    [Fact]
    public void Home_product_card_action_uses_the_cyan_button_treatment()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var homeStyles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "home-showcase.css"));
        var actionStart = homeStyles.IndexOf(".public-home-product-action {", StringComparison.Ordinal);
        Assert.True(actionStart >= 0);
        var actionEnd = homeStyles.IndexOf('}', actionStart);
        var actionRule = homeStyles[actionStart..actionEnd];

        Assert.Contains("background: var(--home-cyan);", actionRule);
        Assert.Contains("color: #FFFFFF !important;", actionRule);
        Assert.Contains("min-height: 40px;", actionRule);
    }

    [Fact]
    public void Product_detail_routes_authorized_administrators_to_the_existing_customer_portal()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Home", "Product.cshtml"));
        var controller = File.ReadAllText(Path.Combine(projectPath, "Controllers", "HomeController.cs"));

        Assert.Contains("User.IsInRole(\"Administrador\")", markup);
        Assert.Contains("Comprar para o cliente selecionado", markup);
        Assert.Contains("OrofoodsPolicies.ApprovedCustomer", controller);
        Assert.Contains("RedirectToAction(\"Product\", \"Portal\", new { id })", controller);
    }

    [Fact]
    public void Product_purchase_links_use_existing_customer_login_and_seller_order_entry_flows()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Home", "Product.cshtml"));
        var portalDetail = File.ReadAllText(Path.Combine(projectPath, "Views", "Portal", "Product.cshtml"));

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
