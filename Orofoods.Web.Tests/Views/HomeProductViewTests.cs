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

        Assert.Contains("public-home-primary-action\" href=\"@orderUrl\">", markup);
        Assert.Contains("Fazer pedido", markup);
        Assert.Contains("fa-cart-shopping", markup);
        Assert.Contains("public-home-secondary-action\" href=\"@portalUrl\">Meu portal", markup);
        Assert.Contains("Criar minha conta", markup);
        Assert.DoesNotContain("Comprar produtos", markup);
        Assert.DoesNotContain("public-home-bimbo-banner-placeholder", markup);
        Assert.DoesNotContain("BIMBO", markup);
    }

    [Fact]
    public void Home_uses_the_local_bread_showcase_banner_and_stacks_it_after_actions_on_mobile()
    {
        var markup = File.ReadAllText(Path.Combine(ProjectPath, "Views", "Home", "Index.cshtml"));
        var styles = File.ReadAllText(Path.Combine(ProjectPath, "wwwroot", "css", "home-showcase.css"));

        Assert.Contains("Seu pr&oacute;ximo pedido come&ccedil;a aqui.", markup);
        Assert.Contains("P&atilde;es e produtos para hamburguerias, restaurantes e lanchonetes.", markup);
        Assert.Contains("~/images/home/banner-paes-foodservice.webp", markup);
        Assert.True(File.Exists(Path.Combine(ProjectPath, "wwwroot", "images", "home", "banner-paes-foodservice.webp")));
        Assert.Contains("public-home-hero-copy", markup);
        Assert.Contains("public-home-hero-media", markup);
        Assert.Contains("container public-home-hero-inner", markup);
        var heroIndex = markup.IndexOf("<section class=\"public-home-hero\"", StringComparison.Ordinal);
        var shellIndex = markup.IndexOf("<div class=\"container public-home-shell\"", StringComparison.Ordinal);
        var featuredIndex = markup.IndexOf("<section class=\"public-home-featured", StringComparison.Ordinal);
        Assert.True(heroIndex >= 0 && heroIndex < shellIndex && shellIndex < featuredIndex);
        Assert.Contains("width: 100%;", styles);
        Assert.Contains("min-height: 260px;", styles);
        Assert.Contains("mask-image: linear-gradient(90deg", styles);
        Assert.Contains("object-fit: cover;", styles);
        Assert.Contains("object-position: center;", styles);
        var mobileStylesStart = styles.IndexOf("@media (max-width: 767.98px)", StringComparison.Ordinal);
        var mobileStylesEnd = styles.IndexOf("@media (max-width: 420px)", mobileStylesStart, StringComparison.Ordinal);
        Assert.True(mobileStylesStart >= 0 && mobileStylesEnd > mobileStylesStart);
        var mobileStyles = styles[mobileStylesStart..mobileStylesEnd];
        Assert.Contains(".public-home-hero {", mobileStyles);
        Assert.Contains("height: auto;", mobileStyles);
        Assert.Contains(".public-home-hero-media {", mobileStyles);
        Assert.Contains("position: relative;", mobileStyles);
        Assert.Contains("width: 100%;", mobileStyles);
        Assert.Contains("max-width: 100%;", mobileStyles);
    }

    [Fact]
    public void Home_product_cards_allow_purchase_of_active_products_and_keep_visitors_on_the_login_flow()
    {
        var card = File.ReadAllText(Path.Combine(ProjectPath, "Views", "Shared", "_PublicProductCard.cshtml"));

        Assert.DoesNotContain("Consulte disponibilidade", card);
        Assert.DoesNotContain("IsCommerciallyAvailable", card);
        Assert.DoesNotContain("max=\"@availableQuantity\"", card);        Assert.Contains("if (!isAuthenticated)", card);
        Assert.Contains("asp-page=\"/Account/Login\"", card);
        Assert.Contains("asp-route-returnUrl=\"@loginReturnUrl\"", card);
        Assert.Contains("asp-controller=\"Portal\" asp-action=\"AddToCart\"", card);
        Assert.Contains("asp-controller=\"OrderDrafts\" asp-action=\"New\"", card);
    }

    [Fact]
    public void Home_order_cta_routes_guests_clients_sellers_and_other_profiles_through_existing_flows()
    {
        var markup = File.ReadAllText(Path.Combine(ProjectPath, "Views", "Home", "Index.cshtml"));

        Assert.Contains("isSalesRepresentative ? sellerOrderEntryUrl", markup);
        Assert.Contains("Url.Action(\"New\", \"OrderDrafts\", new { area = \"Vendedor\", customerId = selectedCustomerId })", markup);
        Assert.Contains("isCustomer ? customerCatalogUrl", markup);
        Assert.Contains(": portalUrl", markup);
        Assert.Contains("loginCatalogUrl", markup);
        Assert.Contains("asp-controller=\"CustomerRegistration\" asp-action=\"Register\"", markup);
    }

    [Fact]
    public void Home_uses_the_shared_white_header_and_the_nav_has_no_home_only_overrides()
    {
        var layout = File.ReadAllText(Path.Combine(ProjectPath, "Views", "Shared", "_Layout.cshtml"));
        var header = File.ReadAllText(Path.Combine(ProjectPath, "Views", "Shared", "_SiteHeader.cshtml"));
        var publicStyles = File.ReadAllText(Path.Combine(ProjectPath, "wwwroot", "css", "customer-experience.css"));
        var homeStyles = File.ReadAllText(Path.Combine(ProjectPath, "wwwroot", "css", "home-showcase.css"));

        Assert.Equal(1, layout.Split("PartialAsync(\"_SiteHeader\")", StringSplitOptions.None).Length - 1);
        Assert.Contains("navbar navbar-expand-lg navbar-light bg-white", header);
        Assert.Contains("logo-orofoods-transparent.png", header);
        Assert.Contains("Cadastre-se", header);
        Assert.Contains("color: #0F172A;", publicStyles);
        Assert.Contains("color: #0E7490 !important;", publicStyles);
        Assert.True(File.Exists(Path.Combine(ProjectPath, "wwwroot", "images", "logo-orofoods-transparent.png")));
        Assert.DoesNotContain(".public-home-page .site-header-modern", homeStyles);
        Assert.DoesNotContain(".navbar", homeStyles);
        Assert.DoesNotContain(".nav-link", homeStyles);
        Assert.DoesNotContain("#0B132B !important", homeStyles);
    }

    [Fact]
    public void Home_uses_a_compact_white_footer_scoped_to_public_pages()
    {
        var layout = File.ReadAllText(Path.Combine(ProjectPath, "Views", "Shared", "_Layout.cshtml"));
        var publicStyles = File.ReadAllText(Path.Combine(ProjectPath, "wwwroot", "css", "customer-experience.css"));

        Assert.Contains("public-home-page", layout);
        Assert.Contains("site-footer-public", layout);
        Assert.Contains("isPublicSitePage", layout);
        Assert.Contains(".public-site-page .site-footer-public", publicStyles);
        Assert.Contains("background: #FFFFFF;", publicStyles);
        Assert.Contains("color: #0F172A;", publicStyles);
        Assert.Contains("#64748B", publicStyles);
        Assert.Contains("#E2E8F0", publicStyles);
        Assert.DoesNotContain(".public-home-page .site-footer", publicStyles);
    }

    [Fact]
    public void Home_omits_search_shortcuts_while_catalog_keeps_search_for_real_product_fields()
    {
        var markup = File.ReadAllText(Path.Combine(ProjectPath, "Views", "Home", "Index.cshtml"));
        var catalog = File.ReadAllText(Path.Combine(ProjectPath, "Views", "Home", "Products.cshtml"));
        var controller = File.ReadAllText(Path.Combine(ProjectPath, "Controllers", "HomeController.cs"));

        Assert.DoesNotContain("public-home-search-panel", markup);
        Assert.DoesNotContain("public-home-chips", markup);
        Assert.Contains("name=\"search\"", catalog);
        Assert.Contains("x.StorageInformation.ToLower().Contains(normalizedSearch)", controller);
        Assert.Contains("x.StorageTemperature.ToLower().Contains(normalizedSearch)", controller);
        Assert.Contains("x.AdditionalInformation.ToLower().Contains(normalizedSearch)", controller);
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
    public void Home_and_public_catalog_cards_share_two_row_quantity_and_add_controls()
    {
        var home = File.ReadAllText(Path.Combine(ProjectPath, "Views", "Home", "Index.cshtml"));
        var products = File.ReadAllText(Path.Combine(ProjectPath, "Views", "Home", "Products.cshtml"));
        var card = File.ReadAllText(Path.Combine(ProjectPath, "Views", "Shared", "_PublicProductCard.cshtml"));
        var styles = File.ReadAllText(Path.Combine(ProjectPath, "wwwroot", "css", "catalog.css")).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("partial name=\"_PublicProductCard\"", home);
        Assert.Contains("partial name=\"_PublicProductCard\"", products);
        Assert.Contains("class=\"catalog-add-controls quick-buy-controls\"", card);
        Assert.Contains("data-catalog-quantity-decrease", card);
        Assert.Contains("data-catalog-quantity-increase", card);
        Assert.Contains("type=\"submit\">Adicionar</button>", card);

        Assert.Contains(".public-quick-buy-card .catalog-add-controls {", styles);
        Assert.Contains("grid-template-columns: minmax(0, 1fr);", styles);
        Assert.Contains("gap: 8px;", styles);
        Assert.Contains("grid-template-columns: 36px minmax(0, 1fr) 36px;", styles);
        Assert.Contains(".public-quick-buy-card .catalog-quantity-stepper > input", styles);
        Assert.Contains("min-width: 0;", styles);
        Assert.Contains(".public-quick-buy-card .catalog-add-controls > .quick-buy-button", styles);
        Assert.Contains("width: 100%;", styles);
        Assert.Contains(".public-products-section .quick-buy-button,", styles);
        Assert.Contains("background: #0E7490;", styles);
        Assert.Contains("background: #0B5D75;", styles);
        Assert.Contains("body.catalog-modern-page.customer-experience .public-products-section .public-quick-buy-grid {", styles);
        Assert.DoesNotContain(".public-home .quick-buy-controls", styles);
        Assert.DoesNotContain("minmax(86px, .72fr)", styles);
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
