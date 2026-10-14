namespace Orofoods.Web.Tests.Views;

public class AdminProductEditVisualContractTests
{
    [Fact]
    public void Product_edit_uses_the_available_admin_width_and_scoped_responsive_layout()
    {
        var projectPath = WebProjectPath();
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Products", "Edit.cshtml"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-products.css"));

        Assert.Contains("class=\"container admin-products-edit-container\"", view);
        Assert.DoesNotContain("max-width:900px", view);
        Assert.Contains(".admin-products-edit-page > .admin-products-edit-container", styles);
        Assert.Contains("max-width: none", styles);
        Assert.Contains("padding-inline: 24px", styles);
        Assert.Contains(".admin-products-edit-page .admin-image-manager > form", styles);
        Assert.Contains(".admin-products-edit-page .admin-image-grid", styles);
        Assert.Contains("@media (max-width: 767px)", styles);
    }

    [Fact]
    public void Product_edit_icon_uses_a_contrasting_cyan_in_dark_mode()
    {
        var styles = File.ReadAllText(Path.Combine(WebProjectPath(), "wwwroot", "css", "admin-products.css"));

        Assert.Contains("html[data-theme=\"dark\"] body.admin-authenticated:not(:has(.whatsapp-inbox-page)) .admin-products-edit-page .admin-form-hero > i", styles);
        Assert.Contains("color: #38BDF8", styles);
        Assert.Contains("background: #0C4A6E", styles);
    }

    private static string WebProjectPath() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
}
