namespace Orofoods.Web.Tests.Views;

public class AdminProductEditVisualContractTests
{
    [Fact]
    public void Product_create_and_edit_share_the_same_admin_form_view()
    {
        var projectPath = WebProjectPath();
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Products", "Edit.cshtml"));
        var controller = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Controllers", "ProductsController.cs"));

        Assert.False(File.Exists(Path.Combine(projectPath, "Areas", "Admin", "Views", "Products", "Create.cshtml")));
        Assert.Contains("public async Task<IActionResult> Edit(int? id, string? q = null, bool includeInactive = false, int? page = null)", controller);
        Assert.Contains("id is null ? new Product()", controller);
        Assert.Contains("return View(product);", controller);
        Assert.Contains("class=\"container admin-products-edit-container\"", view);
        Assert.DoesNotContain("max-width:900px", view);
    }

    [Fact]
    public void Product_form_title_tracks_product_identity()
    {
        var view = ReadProductView("Edit.cshtml");

        Assert.Contains("Model.Id == 0 ? \"Novo produto\" : \"Editar produto\"", view);
        Assert.Contains("<h1>@ViewData[\"Title\"]</h1>", view);
    }

    [Fact]
    public void Product_form_displays_property_validation_errors()
    {
        var view = ReadProductView("Edit.cshtml");

        Assert.Contains("asp-validation-summary=\"All\"", view);
    }

    [Fact]
    public void New_product_action_opens_the_shared_form_without_an_id()
    {
        var index = ReadProductView("Index.cshtml");

        Assert.Matches("(?s)<a[^>]*asp-action=\"Edit\"(?![^>]*asp-route-id)[^>]*>.*Novo produto\\s*</a>", index);
        Assert.DoesNotContain("asp-action=\"Create\"", index);
    }

    [Fact]
    public void Shared_product_form_is_responsive_in_light_and_dark_themes()
    {
        var styles = File.ReadAllText(Path.Combine(WebProjectPath(), "wwwroot", "css", "admin-products.css"));

        Assert.Contains(".admin-products-edit-page > .admin-products-edit-container", styles);
        Assert.Contains("max-width: none", styles);
        Assert.Contains("padding-inline: 24px", styles);
        Assert.Contains(".admin-products-edit-page .admin-image-manager > form", styles);
        Assert.Contains(".admin-products-edit-page .admin-image-grid", styles);
        Assert.Contains("@media (min-width: 768px) and (max-width: 991.98px)", styles);
        Assert.Contains("@media (max-width: 767px)", styles);
        Assert.Contains("background: #E0F2FE", styles);
        Assert.Contains("color: #0369A1", styles);
        Assert.Contains("html[data-theme=\"dark\"] body.admin-authenticated:not(:has(.whatsapp-inbox-page)) .admin-products-edit-page .admin-form-hero > i", styles);
        Assert.Contains("color: #38BDF8", styles);
        Assert.Contains("background: #0C4A6E", styles);
    }

    private static string ReadProductView(string filename) =>
        File.ReadAllText(Path.Combine(WebProjectPath(), "Areas", "Admin", "Views", "Products", filename));

    private static string WebProjectPath() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
}
