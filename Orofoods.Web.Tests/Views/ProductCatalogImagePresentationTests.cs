namespace Orofoods.Web.Tests.Views;

public class ProductCatalogImagePresentationTests
{
    [Fact]
    public void Manually_registered_product_media_takes_precedence_over_wmc_fallback_images()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var views = new (string Path, string ManualImagePrecedence)[]
        {
            (Path.Combine(projectPath, "Views", "Shared", "_PublicProductCard.cshtml"), "image is not null ? Url.RouteUrl(\"ProductMedia\", new { imageId = image.Id })"),
            (Path.Combine(projectPath, "Views", "Home", "Product.cshtml"), "primaryImage is not null ? Url.RouteUrl(\"ProductMedia\", new { imageId = primaryImage.Id })"),
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

        Assert.Contains(".public-product-card .product-visual{aspect-ratio:4/3", styles, StringComparison.Ordinal);
        Assert.Contains(".public-product-card .product-visual>img{object-fit:contain", styles, StringComparison.Ordinal);
        Assert.Contains("class=\"portal-app-shell\"", portalLayout, StringComparison.Ordinal);
        Assert.Contains(".portal-app-shell .product-visual{aspect-ratio:4/3", styles, StringComparison.Ordinal);
        Assert.Contains(".portal-app-shell .product-visual>img{object-fit:contain", styles, StringComparison.Ordinal);
        Assert.Contains(".product-detail-media img{object-fit:contain;mix-blend-mode:normal;", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Public_catalog_and_detail_use_the_approved_light_palette_without_dark_photo_overlays()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var publicCatalogStyles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "catalog.css"));
        var customerStyles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "customer-experience.css"));
        var publicDetail = File.ReadAllText(Path.Combine(projectPath, "Views", "Home", "Product.cshtml"));
        var portalStyles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "portal-dark.css"));

        Assert.Contains(".public-products-section .product-visual", publicCatalogStyles, StringComparison.Ordinal);
        Assert.Contains("background: #FFFFFF;", publicCatalogStyles, StringComparison.Ordinal);
        Assert.Contains(".public-quick-buy-card .product-visual > img", customerStyles, StringComparison.Ordinal);
        Assert.Contains("mix-blend-mode: normal;", customerStyles, StringComparison.Ordinal);
        Assert.Contains("public-product-detail-hero", publicDetail, StringComparison.Ordinal);
        Assert.Contains("public-product-detail-section", publicDetail, StringComparison.Ordinal);
        Assert.Contains(".customer-experience .public-product-detail-hero", customerStyles, StringComparison.Ordinal);
        Assert.Contains("background: #F1F5FB;", customerStyles, StringComparison.Ordinal);
        Assert.Contains("color: #0F172A;", customerStyles, StringComparison.Ordinal);
        Assert.Contains("background: #FFFFFF;", customerStyles, StringComparison.Ordinal);
        Assert.Contains("color: #0E7490;", customerStyles, StringComparison.Ordinal);
        Assert.Contains("body.portal-authenticated .portal-app-main .product-card", portalStyles, StringComparison.Ordinal);
    }

    [Fact]
    public void Illustrative_bimbo_images_are_disclosed_and_desktop_keeps_five_columns()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var portalCatalog = File.ReadAllText(Path.Combine(projectPath, "Views", "Portal", "Catalog.cshtml"));
        var sharedCard = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_PublicProductCard.cshtml"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "site.css"));
        var catalogStyles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "catalog.css"));
        var normalizedCatalogStyles = catalogStyles.Replace("\r\n", "\n", StringComparison.Ordinal);
        var imageCatalog = File.ReadAllText(Path.Combine(projectPath, "Services", "Catalog", "BimboProductImageCatalog.cs"));

        Assert.Contains("IsIllustrative", imageCatalog, StringComparison.Ordinal);
        Assert.Contains("Imagem ilustrativa", sharedCard, StringComparison.Ordinal);
        Assert.Contains("Imagem ilustrativa", portalCatalog, StringComparison.Ordinal);
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
        var publicCatalog = File.ReadAllText(Path.Combine(projectPath, "Views", "Home", "Products.cshtml"))
            + File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_PublicProductCard.cshtml"));
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
        Assert.Contains("Tradicional", publicCatalog, StringComparison.Ordinal);
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
            Path.Combine(projectPath, "Views", "Shared", "_PublicProductCard.cshtml"),
            Path.Combine(projectPath, "Views", "Home", "Product.cshtml"),
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
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_PublicProductCard.cshtml"));
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
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_PublicProductCard.cshtml"));

        Assert.Contains("Cadastre-se", header, StringComparison.Ordinal);
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
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_PublicProductCard.cshtml"));
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

    [Fact]
    public void Public_and_customer_catalogs_serialize_image_scale_as_invariant_decimal_values()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var scaleViews = new[]
        {
            Path.Combine(projectPath, "Views", "Shared", "_PublicProductCard.cshtml"),
            Path.Combine(projectPath, "Views", "Portal", "Catalog.cshtml"),
            Path.Combine(projectPath, "Views", "Home", "Product.cshtml"),
            Path.Combine(projectPath, "Areas", "Vendedor", "Views", "Catalog", "Index.cshtml")
        };

        foreach (var path in scaleViews)
        {
            Assert.Contains("BimboProductImageCatalog.FormatVisualScale", File.ReadAllText(path), StringComparison.Ordinal);
        }

        var originalCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
            Assert.Equal("1.45", Orofoods.Web.Services.Catalog.BimboProductImageCatalog.FormatVisualScale(1.45m));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void Customer_catalog_limits_images_to_the_centered_four_by_three_frame()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "catalog.css"));
        var frameRules = System.Text.RegularExpressions.Regex.Match(
            styles,
            @"\.customer-experience \.portal-catalog-page \.product-visual\s*\{(?<rules>[^}]*)\}").Groups["rules"].Value;
        var imageRules = System.Text.RegularExpressions.Regex.Match(
            styles,
            @"\.customer-experience \.portal-catalog-page \.product-visual img\s*\{(?<rules>[^}]*)\}").Groups["rules"].Value;

        Assert.Contains("grid-template-rows: minmax(0, 1fr);", frameRules, StringComparison.Ordinal);
        Assert.Contains("object-fit: contain;", imageRules, StringComparison.Ordinal);
        Assert.Contains("object-position: center;", imageRules, StringComparison.Ordinal);
    }

    [Fact]
    public void Portal_catalog_photos_preserve_transparency_without_changing_the_dark_portal_surfaces()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "portal-dark.css"));
        var sharedStyles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "site.css"));
        var frameRules = System.Text.RegularExpressions.Regex.Match(
            styles,
            @"body\.portal-authenticated \.portal-app-main \.portal-catalog-page \.product-visual\s*\{(?<rules>[^}]*)\}").Groups["rules"].Value;
        var imageRules = System.Text.RegularExpressions.Regex.Match(
            styles,
            @"body\.portal-authenticated \.portal-app-main \.portal-catalog-page \.product-visual img\s*\{(?<rules>[^}]*)\}").Groups["rules"].Value;
        var unavailableCardRules = System.Text.RegularExpressions.Regex.Match(
            styles,
            @"body\.portal-authenticated \.portal-app-main \.portal-catalog-page \.product-card\.unavailable\s*\{(?<rules>[^}]*)\}").Groups["rules"].Value;

        Assert.Contains(".unavailable{opacity:.72}", sharedStyles, StringComparison.Ordinal);
        Assert.Contains("opacity: 1;", unavailableCardRules, StringComparison.Ordinal);
        Assert.DoesNotContain("background: #FFFFFF;", frameRules, StringComparison.Ordinal);
        Assert.Contains("object-fit: contain;", imageRules, StringComparison.Ordinal);
        Assert.Contains("object-position: center;", imageRules, StringComparison.Ordinal);
        Assert.Contains("mix-blend-mode: normal;", imageRules, StringComparison.Ordinal);
        Assert.Contains("var(--portal-dark-surface)", styles, StringComparison.Ordinal);
        Assert.Contains("var(--portal-dark-surface-secondary)", styles, StringComparison.Ordinal);
        Assert.Contains("INDISPON&Iacute;VEL", File.ReadAllText(Path.Combine(projectPath, "Views", "Portal", "Catalog.cshtml")), StringComparison.Ordinal);
        Assert.Contains("product.Category", File.ReadAllText(Path.Combine(projectPath, "Views", "Portal", "Catalog.cshtml")), StringComparison.Ordinal);
    }
}
