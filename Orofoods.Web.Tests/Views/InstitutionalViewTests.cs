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
        var markup = File.ReadAllText(path);

        Assert.Contains("asp-controller=\"CustomerRegistration\" asp-action=\"Register\"", markup);
        Assert.Contains("Url.Page(\"/Account/Login\", values: new { area = \"Identity\", returnUrl = customerDashboardUrl })", markup);
        Assert.Contains("Url.Action(\"Dashboard\", \"Portal\", new { area = \"\" })", markup);
        Assert.Contains("returnUrl = customerDashboardUrl", markup);
        Assert.Contains("User.Identity?.IsAuthenticated == true", markup);
    }

    [Fact]
    public void Home_view_has_one_institutional_hero_categories_and_featured_products_without_a_carousel()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web/Views/Home/Index.cshtml"));
        var markup = File.ReadAllText(path);

        Assert.Contains("home-hero", markup);
        Assert.Contains("home-categories", markup);
        Assert.Contains("public-home-featured", markup);
        Assert.DoesNotContain("carousel", markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("home-command-bar", markup);
        Assert.DoesNotContain("home-catalog-cta", markup);
        Assert.DoesNotContain("home-operation", markup);
        Assert.DoesNotContain("btn-gold", markup);
    }

    [Fact]
    public void Home_product_cards_use_registered_product_media_or_a_neutral_placeholder()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web/Views/Home/Index.cshtml"));
        var markup = File.ReadAllText(path);

        Assert.Contains("ProductMedia", markup);
        Assert.Contains("home-product-placeholder", markup);
        Assert.Contains("home-product-card", markup);
        Assert.DoesNotContain("BIM-00", markup);
    }

    [Fact]
    public void Home_styles_keep_the_public_visual_system_local_responsive_and_accessible()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web/wwwroot/css/home-showcase.css"));
        var styles = File.ReadAllText(path);

        Assert.Contains("#F4F7FC", styles, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#0E7490", styles, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("object-fit: contain", styles, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(":focus-visible", styles);
        Assert.Contains("@media (max-width: 767px)", styles);
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
}
