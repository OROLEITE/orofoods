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
            Assert.DoesNotContain("officialImage?.ImageUrl ??", view, StringComparison.Ordinal);
            Assert.Contains(manualImagePrecedence, view, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Public_product_images_use_a_shared_frame_and_preserve_the_full_photo()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "site.css"));
        var portalLayout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_PortalLayout.cshtml"));
        var homeStyles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "home-showcase.css"));

        Assert.Contains(".public-product-card .product-visual{aspect-ratio:4/3", styles, StringComparison.Ordinal);
        Assert.Contains(".public-product-card .product-visual>img{object-fit:contain", styles, StringComparison.Ordinal);
        Assert.Contains("class=\"portal-app-shell\"", portalLayout, StringComparison.Ordinal);
        Assert.Contains(".portal-app-shell .product-visual{aspect-ratio:4/3", styles, StringComparison.Ordinal);
        Assert.Contains(".portal-app-shell .product-visual>img{object-fit:contain", styles, StringComparison.Ordinal);
        Assert.Contains(".public-home-product-media {\r\n    display: grid;\r\n    width: 100%;\r\n    aspect-ratio: 4/3;", homeStyles, StringComparison.Ordinal);
        Assert.Contains(".public-home-product-media {\r\n        aspect-ratio: 4/3;", homeStyles, StringComparison.Ordinal);
        Assert.Contains(".product-detail-media img{object-fit:contain;mix-blend-mode:normal}", styles, StringComparison.Ordinal);
    }
}
