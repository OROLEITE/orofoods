namespace Orofoods.Web.Tests.Views;

public sealed class CustomerPortalQaVisualContractTests
{
    private static string WebProjectPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));

    [Fact]
    public void Customer_sidebar_collapses_to_zero_reserved_width_on_mobile_and_keeps_desktop_sidebar()
    {
        var styles = Compact(Read("wwwroot", "css", "portal-navigation.css"));

        Assert.Contains("@media(max-width:900px){.portal-authenticated{padding-top:0;}", styles);
        Assert.Contains("--portal-sidebar-width:0;", styles);
        Assert.Contains("grid-column:auto;width:100%;", styles);
        Assert.Contains("max-width:100%;", styles);
        Assert.Contains("margin-left:0;", styles);
        Assert.Contains(".portal-menu-open.portal-app-sidebar{transform:translateX(0);}", styles);
        Assert.Contains("@media(min-width:901px){.portal-app-shell{min-width:0;", styles);
        Assert.Contains(".portal-app-sidebar{position:fixed;", styles);
    }

    [Fact]
    public void Customer_sidebar_ellipsizes_long_names_and_exposes_the_full_name_as_a_tooltip()
    {
        var layout = Read("Views", "Shared", "_PortalLayout.cshtml");
        var styles = Compact(Read("wwwroot", "css", "portal-navigation.css"));

        Assert.Contains("<h3 title=\"@customerName\">@customerName</h3>", layout);
        Assert.Contains(".portal-companyh3{min-width:0;overflow:hidden;", styles);
        Assert.Contains("text-overflow:ellipsis;", styles);
        Assert.Contains("white-space:nowrap;", styles);
    }

    [Fact]
    public void Saved_model_order_pages_highlight_their_own_sidebar_destination()
    {
        var layout = Read("Views", "Shared", "_PortalLayout.cshtml");

        Assert.Contains("\"SavedOrders\" or \"EditSavedOrder\" => \"saved-orders\"", layout);
        Assert.Contains("activeSection == \"saved-orders\"", layout);
        Assert.Contains("asp-action=\"SavedOrders\"", layout);
        Assert.Contains("Meus modelos de pedido", layout);
        Assert.DoesNotContain("\"SavedOrders\" or \"EditSavedOrder\" => \"cart\"", layout);
    }

    [Fact]
    public void Forgot_password_validation_loads_local_identity_assets_allowed_by_the_existing_csp()
    {
        var view = Read("Areas", "Identity", "Pages", "Account", "ForgotPassword.cshtml");
        var validation = Read("Views", "Shared", "_ValidationScriptsPartial.cshtml");
        var csp = Read("Infrastructure", "SecurityHeadersMiddleware.cs");

        Assert.Contains("~/lib/jquery/dist/jquery.min.js", view);
        Assert.Contains("<partial name=\"_ValidationScriptsPartial\" />", view);
        Assert.Contains("~/lib/jquery-validation/dist/jquery.validate.min.js", validation);
        Assert.Contains("~/lib/jquery-validation-unobtrusive/dist/jquery.validate.unobtrusive.min.js", validation);
        Assert.Contains("script-src 'self'", csp);
        Assert.DoesNotContain("integrity=", view + validation, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(WebProjectPath, "wwwroot", "lib", "jquery", "dist", "jquery.min.js")));
        Assert.True(File.Exists(Path.Combine(WebProjectPath, "wwwroot", "lib", "jquery-validation", "dist", "jquery.validate.min.js")));
        Assert.True(File.Exists(Path.Combine(WebProjectPath, "wwwroot", "lib", "jquery-validation-unobtrusive", "dist", "jquery.validate.unobtrusive.min.js")));
    }

    [Fact]
    public void Customer_catalog_uses_registered_product_media_and_only_shows_a_placeholder_without_an_image_record()
    {
        var view = Read("Views", "Portal", "Catalog.cshtml");
        var mediaController = Read("Controllers", "ProductMediaController.cs");

        Assert.Contains("var image = product.Images", view);
        Assert.Contains("Orofoods.Web.Services.Catalog.BimboProductImageCatalog.Find(product.WmcCode)", view);
        Assert.Contains("if (officialImage is not null || image is not null)", view);
        Assert.Contains("image is not null ? Url.RouteUrl(\"ProductMedia\", new { imageId = image.Id })", view);
        Assert.Contains("else", view);
        Assert.Contains("product-photo-unavailable.svg", view);
        Assert.Contains("imageStorage.OpenReadAsync(image.Url", mediaController);
        Assert.Contains("return result is null ? NotFound() : File(result.Content, result.ContentType)", mediaController);
        Assert.Contains("StatusCode(StatusCodes.Status503ServiceUnavailable)", mediaController);
    }

    [Fact]
    public void Customer_catalog_search_label_discloses_supported_product_codes()
    {
        var view = Read("Views", "Portal", "Catalog.cshtml");

        Assert.Contains("Pesquisar por nome, SKU, marca, código WMC ou EAN", view);
    }

    private static string Compact(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value, @"\s+", string.Empty);

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { WebProjectPath }.Concat(parts).ToArray()));
}
