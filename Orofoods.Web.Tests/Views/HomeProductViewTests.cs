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
    public void Home_uses_a_neutral_banner_placeholder_until_an_official_bimbo_photo_is_verified()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Home", "Index.cshtml"));

        Assert.Contains("public-home-bimbo-banner-placeholder", markup);
        Assert.DoesNotContain("orofoods-institutional-kitchen.png", markup);
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
    public void Product_detail_offers_administrators_a_return_to_administration()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Home", "Product.cshtml"));

        Assert.Contains("User.IsInRole(\"Administrador\")", markup);
        Assert.Contains("asp-area=\"Admin\" asp-controller=\"Dashboard\"", markup);
    }

    [Fact]
    public void Product_availability_uses_text_not_an_html_entity_inside_razor_code()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Home", "Product.cshtml"));

        Assert.Contains("? \"Disponível\" : \"Indisponível no momento\"", markup);
        Assert.DoesNotContain("? \"Dispon&iacute;vel\"", markup);
    }
}
