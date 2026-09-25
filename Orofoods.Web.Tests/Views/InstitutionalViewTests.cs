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
        Assert.Contains("Url.Page(\"/Account/Login\", values: new { area = \"Identity\", returnUrl = customerCatalogUrl })", markup);
        Assert.Contains("Url.Action(\"Catalog\", \"Portal\", new { area = \"\" })", markup);
        Assert.Contains("returnUrl = customerDashboardUrl", markup);
        Assert.Contains("User.Identity?.IsAuthenticated == true", markup);
    }

    [Fact]
    public void Home_view_has_a_commercial_command_bar_and_featured_product_section()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web/Views/Home/Index.cshtml"));
        var markup = File.ReadAllText(path);

        Assert.Contains("home-command-bar", markup);
        Assert.Contains("home-feature-grid", markup);
        Assert.Contains("home-catalog-cta", markup);
    }

    [Fact]
    public void Home_carousel_uses_non_overlapping_slide_transitions()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web/Views/Home/Index.cshtml"));
        var markup = File.ReadAllText(path);

        Assert.Contains("class=\"carousel slide\"", markup);
        Assert.DoesNotContain("carousel-fade", markup);
    }

    [Fact]
    public void Home_carousel_keeps_a_full_bleed_background()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web/Views/Home/Index.cshtml"));
        var markup = File.ReadAllText(path);

        Assert.Contains("<section class=\"home-showcase\">", markup);
        Assert.DoesNotContain("max-width:1600px", markup);
    }

    [Fact]
    public void Home_carousel_aligns_copy_with_the_site_content_column()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web/Views/Home/Index.cshtml"));
        var markup = File.ReadAllText(path);

        Assert.Contains("padding-left:max(32px,calc((100vw - 1180px)/2))", markup);
    }

    [Fact]
    public void Home_showcase_uses_the_compact_desktop_geometry()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web/wwwroot/css/home-showcase.css"));
        var styles = File.ReadAllText(path);

        Assert.Contains("height: 350px", styles);
        Assert.Contains("font-size: 46px", styles);
        Assert.Contains("min-height: 54px", styles);
        Assert.Contains("grid-template-columns: 57% 43%", styles);
        Assert.Contains("padding-top: 0", styles);
        Assert.Contains("height: 150px", styles);
        Assert.Contains("font-size: 34px", styles);
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
