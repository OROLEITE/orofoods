namespace Orofoods.Web.Tests.Views;

public class ProductCatalogImagePresentationTests
{
    [Fact]
    public void Manually_registered_product_media_takes_precedence_over_wmc_fallback_images()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var views = new (string Path, string ManualImagePrecedence)[]
        {
            (Path.Combine(projectPath, "Views", "Home", "Index.cshtml"), "image is not null ? Url.RouteUrl(\"ProductMedia\", new { imageId = image.Id })"),
            (Path.Combine(projectPath, "Views", "Home", "Product.cshtml"), "primaryImage is not null ? Url.RouteUrl(\"ProductMedia\", new { imageId = primaryImage.Id })"),
            (Path.Combine(projectPath, "Views", "Home", "Products.cshtml"), "image is not null ? Url.RouteUrl(\"ProductMedia\", new { imageId = image.Id })"),
            (Path.Combine(projectPath, "Views", "Portal", "Catalog.cshtml"), "image is not null ? Url.RouteUrl(\"ProductMedia\", new { imageId = image.Id })"),
            (Path.Combine(projectPath, "Views", "Portal", "Product.cshtml"), "primaryImage is not null ? Url.RouteUrl(\"ProductMedia\", new { imageId = primaryImage.Id })")
        };

        foreach (var (path, manualImagePrecedence) in views)
        {
            var view = File.ReadAllText(path);
            Assert.Contains("data-fallback-stage=", view, StringComparison.Ordinal);
            Assert.Contains("data-catalog-fallback=", view, StringComparison.Ordinal);
            Assert.Contains(manualImagePrecedence, view, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Public_product_images_use_a_shared_frame_and_preserve_the_full_photo()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "site.css"));
        var portalLayout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_PortalLayout.cshtml"));
        var homeStyles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "home-showcase.css"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains(".public-product-card .product-visual{aspect-ratio:4/3", styles, StringComparison.Ordinal);
        Assert.Contains(".public-product-card .product-visual>img{object-fit:contain", styles, StringComparison.Ordinal);
        Assert.Contains("class=\"portal-app-shell\"", portalLayout, StringComparison.Ordinal);
        Assert.Contains(".portal-app-shell .product-visual{aspect-ratio:4/3", styles, StringComparison.Ordinal);
        Assert.Contains(".portal-app-shell .product-visual>img{object-fit:contain", styles, StringComparison.Ordinal);
        Assert.Contains(".public-home-product-media {\n    display: grid;\n    position: relative;\n    width: 100%;\n    aspect-ratio: 4/3;", homeStyles, StringComparison.Ordinal);
        Assert.Contains(".public-home-product-media {\n        aspect-ratio: 4/3;", homeStyles, StringComparison.Ordinal);
        Assert.Contains(".product-detail-media img{object-fit:contain;mix-blend-mode:normal;", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Illustrative_bimbo_metadata_is_preserved_without_visible_badges_and_desktop_keeps_five_columns()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var publicCatalog = File.ReadAllText(Path.Combine(projectPath, "Views", "Home", "Products.cshtml"));
        var home = File.ReadAllText(Path.Combine(projectPath, "Views", "Home", "Index.cshtml"));
        var portalCatalog = File.ReadAllText(Path.Combine(projectPath, "Views", "Portal", "Catalog.cshtml"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "site.css"));
        var catalogStyles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "catalog.css"));
        var normalizedCatalogStyles = catalogStyles.Replace("\r\n", "\n", StringComparison.Ordinal);
        var imageCatalog = File.ReadAllText(Path.Combine(projectPath, "Services", "Catalog", "BimboProductImageCatalog.cs"));

        Assert.Contains("IsIllustrative", imageCatalog, StringComparison.Ordinal);
        Assert.DoesNotContain("Imagem ilustrativa", publicCatalog, StringComparison.Ordinal);
        Assert.DoesNotContain("Imagem ilustrativa", home, StringComparison.Ordinal);
        Assert.DoesNotContain("Imagem ilustrativa", portalCatalog, StringComparison.Ordinal);
        Assert.Contains(".public-quick-buy-grid{display:grid;grid-template-columns:repeat(5,minmax(0,1fr))", styles, StringComparison.Ordinal);
        Assert.Contains("@media(min-width:1200px){.portal-catalog-page .product-grid{grid-template-columns:repeat(5,minmax(0,1fr))}}", styles, StringComparison.Ordinal);
        Assert.Contains("background: #F4F7FC", catalogStyles, StringComparison.Ordinal);
        Assert.Contains("border: 1px solid #E2E8F0", catalogStyles, StringComparison.Ordinal);
        Assert.Contains("background: #0E7490", catalogStyles, StringComparison.Ordinal);
        Assert.Contains("background: #0B5D75", catalogStyles, StringComparison.Ordinal);
        Assert.Contains("aspect-ratio: 4 / 3", catalogStyles, StringComparison.Ordinal);
        Assert.Contains("object-fit: contain", catalogStyles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 599px)", catalogStyles, StringComparison.Ordinal);
        Assert.Contains(".portal-catalog-page .product-grid {\n        grid-template-columns: repeat(5, minmax(0, 1fr));", normalizedCatalogStyles, StringComparison.Ordinal);
    }

    [Fact]
    public void Public_catalog_keeps_real_identifier_search_data_based_quick_filters_and_visitor_login_return()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var publicCatalog = File.ReadAllText(Path.Combine(projectPath, "Views", "Home", "Products.cshtml"));
        var controller = File.ReadAllText(Path.Combine(projectPath, "Controllers", "HomeController.cs"));
        var filters = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "js", "public-catalog-filters.js"));

        Assert.Contains("quick-buy-button", publicCatalog, StringComparison.Ordinal);
        Assert.Contains("/Account/Login", publicCatalog, StringComparison.Ordinal);
        Assert.Contains("asp-page=\"/Account/Login\"", publicCatalog, StringComparison.Ordinal);
        Assert.Contains("asp-route-returnUrl=\"@loginReturnUrl\"", publicCatalog, StringComparison.Ordinal);
        Assert.Contains("name=\"search\"", publicCatalog, StringComparison.Ordinal);
        Assert.Contains("Brioche", publicCatalog, StringComparison.Ordinal);
        Assert.Contains("Gergelim", publicCatalog, StringComparison.Ordinal);
        Assert.Contains("Australiano", publicCatalog, StringComparison.Ordinal);
        Assert.Contains("Congelados", publicCatalog, StringComparison.Ordinal);
        Assert.Contains("x.WmcCode", controller, StringComparison.Ordinal);
        Assert.Contains("x.Ean", controller, StringComparison.Ordinal);
        Assert.Contains("x.Ean13", controller, StringComparison.Ordinal);
        Assert.Contains("data-catalog-filter", filters, StringComparison.Ordinal);
    }

    [Fact]
    public void Wmc_504342_can_override_an_incorrect_manual_photo_with_its_confirmed_catalog_image()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var views = new[]
        {
            Path.Combine(projectPath, "Views", "Home", "Index.cshtml"),
            Path.Combine(projectPath, "Views", "Home", "Product.cshtml"),
            Path.Combine(projectPath, "Views", "Home", "Products.cshtml"),
            Path.Combine(projectPath, "Views", "Portal", "Catalog.cshtml"),
            Path.Combine(projectPath, "Views", "Portal", "Product.cshtml")
        };

        foreach (var path in views)
        {
            Assert.Contains("UseCatalogImage", File.ReadAllText(path), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Public_catalog_cards_support_direct_purchase_by_authorized_customers_and_sellers()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Home", "Products.cshtml"));
        var controller = File.ReadAllText(Path.Combine(projectPath, "Controllers", "HomeController.cs"));

        Assert.Contains("data-catalog-add-form", markup, StringComparison.Ordinal);
        Assert.Contains("asp-controller=\"Portal\" asp-action=\"AddToCart\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-catalog-quantity-decrease", markup, StringComparison.Ordinal);
        Assert.Contains("data-catalog-quantity-increase", markup, StringComparison.Ordinal);
        Assert.Contains("OrderDrafts\" asp-action=\"New\"", markup, StringComparison.Ordinal);
        Assert.Contains("CustomerAccessService", controller, StringComparison.Ordinal);
        Assert.Contains("GetPricesAsync", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("Conhecer produto", markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">Comprar<", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Visitor_header_returns_to_catalog_and_seller_selection_keeps_product_id()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var header = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_SiteHeader.cshtml"));
        var sellerEditor = File.ReadAllText(Path.Combine(projectPath, "Areas", "Vendedor", "Views", "OrderDrafts", "Editor.cshtml"));
        var sellerController = File.ReadAllText(Path.Combine(projectPath, "Areas", "Vendedor", "Controllers", "OrderDraftsController.cs"));
        var sellerCatalog = File.ReadAllText(Path.Combine(projectPath, "Areas", "Vendedor", "Views", "Catalog", "Index.cshtml"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Home", "Products.cshtml"));

        Assert.Contains("Cadastrar", header, StringComparison.Ordinal);
        Assert.Contains("asp-route-returnUrl=\"@catalogLoginReturnUrl\"", header, StringComparison.Ordinal);
        Assert.Contains("CustomerRegistration\" asp-action=\"Register", header, StringComparison.Ordinal);
        Assert.Contains("PendingProductId", sellerEditor, StringComparison.Ordinal);
        Assert.Contains("PendingProductId = productId", sellerController, StringComparison.Ordinal);
        Assert.Contains("asp-route-productId=\"@product.Id\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("asp-controller=\"Cart\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-controller=\"OrderDrafts\" asp-action=\"New\"", sellerCatalog, StringComparison.Ordinal);
        Assert.Contains(">Comprar</a>", sellerCatalog, StringComparison.Ordinal);
        Assert.DoesNotContain("asp-controller=\"Cart\"", sellerCatalog, StringComparison.Ordinal);
        Assert.DoesNotContain("seller-cart.js", sellerCatalog, StringComparison.Ordinal);
        Assert.DoesNotContain("seller-product-details-link", sellerCatalog, StringComparison.Ordinal);
    }

    [Fact]
    public void Quick_buy_catalog_uses_registered_names_and_codes_and_prevents_image_distortion()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Home", "Products.cshtml"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "customer-experience.css"));

        Assert.Contains("<h2>@product.Name</h2>", markup, StringComparison.Ordinal);
        Assert.Contains("<small class=\"quick-buy-code\">@(string.IsNullOrWhiteSpace(product.WmcCode) ? product.Sku : $\"WMC {product.WmcCode}\")</small>", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("quick-buy-wmc", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<h2>@(catalogImage?.ModelName ?? product.Name)</h2>", markup, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: repeat(5, minmax(0, 1fr))", styles, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: repeat(3, minmax(0, 1fr))", styles, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr))", styles, StringComparison.Ordinal);
        Assert.Contains("aspect-ratio: 4 / 3", styles, StringComparison.Ordinal);
        Assert.Contains("object-fit: contain", styles, StringComparison.Ordinal);
        Assert.Contains("object-position: center", styles, StringComparison.Ordinal);
        Assert.Contains("grid-template-rows: minmax(0, 1fr)", styles, StringComparison.Ordinal);
        Assert.Contains("min-height: 0", styles, StringComparison.Ordinal);
        Assert.Contains("transform: scale(var(--bimbo-image-scale, 1))", styles, StringComparison.Ordinal);
        Assert.Contains("background: #F1F5FB", styles, StringComparison.Ordinal);
        Assert.Contains("background: #F8FAFC", styles, StringComparison.Ordinal);
    }
}
