namespace Orofoods.Web.Tests.Views;

public class InstitutionalViewTests
{
    [Fact]
    public void Contact_view_contains_antiforgery_form_and_privacy_link()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web/Views/Home/Contact.cshtml"));
        var markup = File.ReadAllText(path);

        Assert.Contains("asp-antiforgery=\"true\"", markup);
        Assert.Contains("asp-action=\"Privacy\"", markup);
    }

    [Fact]
    public void Home_view_sends_product_interest_to_customer_access()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web/Views/Home/Index.cshtml"));
        var cardPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web/Views/Shared/_PublicProductCard.cshtml"));
        var markup = File.ReadAllText(path);
        var card = File.ReadAllText(cardPath);

        Assert.Contains("asp-controller=\"CustomerRegistration\" asp-action=\"Register\"", card);
        Assert.Contains("asp-page=\"/Account/Login\" asp-route-returnUrl=\"@loginReturnUrl\"", card);
        Assert.Contains(">Comprar</a>", card);
        Assert.Contains("var loginCatalogUrl = Url.Page(\"/Account/Login\", values: new { area = \"Identity\", returnUrl = publicCatalogUrl })", markup);
        Assert.Contains("var publicCatalogUrl = Url.Action(\"Products\", \"Home\", new { area = \"\" }) ?? \"/produtos\"", markup);
        Assert.Contains("Url.Action(\"Dashboard\", \"Portal\", new { area = \"\" })", markup);
        Assert.Contains("href=\"@orderUrl\"", markup);
        Assert.Contains("User.Identity?.IsAuthenticated == true", markup);
    }

    [Fact]
    public void Home_omits_search_and_quick_filters_while_the_catalog_keeps_them_and_real_products()
    {
        var webRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(webRoot, "Views/Home/Index.cshtml"));
        var catalog = File.ReadAllText(Path.Combine(webRoot, "Views/Home/Products.cshtml"));

        Assert.Contains("Fazer pedido", markup);
        Assert.Contains("fa-cart-shopping", markup);
        Assert.Contains("Meu portal", markup);
        Assert.DoesNotContain("name=\"search\"", markup);
        Assert.DoesNotContain("public-home-search", markup);
        Assert.DoesNotContain("public-home-chips", markup);
        Assert.Contains("name=\"search\"", catalog);
        Assert.Contains("public-catalog-quick-filters", catalog);
        Assert.Contains("data-catalog-filter=\"brioche\"", catalog);

        Assert.Contains("Produtos em destaque", markup);
        Assert.Contains("<partial name=\"_PublicProductCard\" model=\"product\" />", markup);
        Assert.Contains("Model.Products.Count == 0", markup);
        Assert.DoesNotContain("carousel", markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("orofoods-hero.png", markup);
        Assert.Contains("banner-paes-foodservice.webp", markup);
        Assert.DoesNotContain("btn-gold", markup);
    }

    [Fact]
    public void Home_and_catalog_share_one_product_card_without_inventing_prices_or_promotions()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var home = File.ReadAllText(Path.Combine(root, "Views/Home/Index.cshtml"));
        var catalog = File.ReadAllText(Path.Combine(root, "Views/Home/Products.cshtml"));
        var card = File.ReadAllText(Path.Combine(root, "Views/Shared/_PublicProductCard.cshtml"));

        Assert.Contains("_PublicProductCard", catalog);
        Assert.DoesNotContain("data-catalog-add-form", home);
        Assert.DoesNotContain("data-catalog-add-form", catalog);
        Assert.Contains("ProductMedia", card);
        Assert.Contains("prices.GetValueOrDefault(product.Id)", card);
        Assert.DoesNotContain("desconto", home + card, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("promo", home + card, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Home_styles_use_the_ice_blue_palette_without_a_navy_header_and_stay_responsive_and_accessible()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web/wwwroot/css/home-showcase.css"));
        var styles = File.ReadAllText(path);

        Assert.Contains("#F1F5FB", styles, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#0F172A", styles, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#0E7490", styles, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".site-header-modern", styles);
        Assert.DoesNotContain("#0B132B !important", styles);
        Assert.Contains(":focus-visible", styles);
        Assert.Contains("@media (max-width: 575.98px)", styles);
        Assert.Contains("prefers-reduced-motion", styles);
    }

    [Fact]
    public void Institutional_pages_use_the_shared_photo_treatment_and_clear_copy()
    {
        var webRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var about = File.ReadAllText(Path.Combine(webRoot, "Views/Home/About.cshtml"));
        var styles = File.ReadAllText(Path.Combine(webRoot, "wwwroot/css/site.css"));

        Assert.Contains("about-story", about);
        Assert.Contains("orofoods-institutional-kitchen.png", about);
        Assert.Contains("/images/orofoods-institutional-kitchen.png", styles);
        Assert.Contains(".institutional-photo", styles);
    }

    [Fact]
    public void Institutional_pages_use_a_scoped_blue_and_beige_backdrop_without_sticky_blank_space()
    {
        var webRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var about = File.ReadAllText(Path.Combine(webRoot, "Views/Home/About.cshtml"));
        var howItWorks = File.ReadAllText(Path.Combine(webRoot, "Views/Home/HowItWorks.cshtml"));
        var contact = File.ReadAllText(Path.Combine(webRoot, "Views/Home/Contact.cshtml"));
        var views = new[] { about, howItWorks, contact };
        var styles = File.ReadAllText(Path.Combine(webRoot, "wwwroot/css/customer-experience.css"));
        var footerRule = System.Text.RegularExpressions.Regex.Match(
            styles,
            @"body\.customer-experience\.public-institutional-page:has\([^)]*\)\s*>\s*\.site-footer\s*\{[^}]*\}").Value;

        Assert.All(views, view => Assert.Contains("public-institutional-page", view, StringComparison.Ordinal));
        Assert.DoesNotContain("public-sticky-footer", string.Join(Environment.NewLine, views), StringComparison.Ordinal);
        Assert.Contains("body.customer-experience.public-institutional-page", styles, StringComparison.Ordinal);
        Assert.Contains("#F4F7FC", styles, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#F8F5ED", styles, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".page-hero.public-page-hero:not(.public-products-hero)", styles, StringComparison.Ordinal);
        Assert.Contains("body.customer-experience.public-institutional-page .process-step", styles, StringComparison.Ordinal);
        Assert.Contains("padding-left: 100px;", styles, StringComparison.Ordinal);
        Assert.Contains("padding-left: 87px;", styles, StringComparison.Ordinal);

        Assert.Matches(@"ViewData\[""BodyClass""\]\s*=\s*""public-institutional-page""", about);
        Assert.Matches(@"ViewData\[""BodyClass""\]\s*=\s*""public-institutional-page""", howItWorks);
        Assert.DoesNotContain("about-story", contact, StringComparison.Ordinal);
        Assert.DoesNotContain("process-flow", contact, StringComparison.Ordinal);
        Assert.Matches(@"body\.customer-experience\.public-institutional-page:has\(\.about-story,\s*\.process-flow\)\s*\{\s*display:\s*flex;\s*min-height:\s*100vh;\s*min-height:\s*100dvh;\s*flex-direction:\s*column;", styles);
        Assert.Matches(@"body\.customer-experience\.public-institutional-page:has\(\.about-story,\s*\.process-flow\)\s*>\s*\.site-main\s*\{\s*flex:\s*1 0 auto;", styles);
        Assert.Matches(@"body\.customer-experience\.public-institutional-page:has\(\.about-story,\s*\.process-flow\)\s*>\s*\.site-footer\s*\{\s*flex:\s*0 0 auto;\s*margin-top:\s*auto;", styles);
        Assert.DoesNotMatch(@"position:\s*(fixed|absolute)", footerRule);
    }
}
