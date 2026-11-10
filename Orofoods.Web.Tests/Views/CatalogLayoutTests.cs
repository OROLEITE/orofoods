namespace Orofoods.Web.Tests.Views;

public class CatalogLayoutTests
{
    [Fact]
    public void Catalog_views_share_ice_background_and_navy_cyan_hierarchy()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web/wwwroot/css/catalog.css"));
        var styles = File.ReadAllText(path).Replace("\r\n", "\n");
        var customerStylesPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web/wwwroot/css/customer-experience.css"));
        var customerStyles = File.ReadAllText(customerStylesPath).Replace("\r\n", "\n");

        Assert.Contains(".public-products-section,\n.portal-catalog-page", styles);
        Assert.Contains("background: #F1F5FB", styles);
        Assert.Contains("color: #0F172A", styles);
        Assert.Contains("color: #0E7490", styles);
        Assert.Contains(".customer-experience .public-products-section {\n    background: #F1F5FB;", customerStyles);
        Assert.Contains(".customer-experience .public-products-section .public-quick-buy-card .product-visual {", customerStyles);
        Assert.Contains("background: #F8FAFC;", customerStyles);
    }

    [Fact]
    public void Catalog_filters_align_on_desktop_and_collapse_without_overflow_on_mobile()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web/wwwroot/css/catalog.css"));
        var styles = File.ReadAllText(path).Replace("\r\n", "\n");
        var customerStylesPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web/wwwroot/css/customer-experience.css"));
        var customerStyles = File.ReadAllText(customerStylesPath).Replace("\r\n", "\n");

        Assert.Contains("grid-template-columns: minmax(260px, 1fr) minmax(170px, .35fr) auto", styles);
        Assert.Contains("grid-template-columns: minmax(260px, 1fr) minmax(170px, .35fr) minmax(150px, .3fr) auto", styles);
        Assert.Contains("@media (max-width: 599px)", styles);
        Assert.Contains(".public-products-tools {\n        grid-template-columns: minmax(0, 1fr) auto", styles);
        Assert.Contains(".portal-catalog-page .catalog-tools {\n        grid-template-columns: minmax(0, 1fr) minmax(0, 1fr)", styles);
        Assert.Contains(".customer-experience .public-products-section .public-products-tools {", customerStyles);
        Assert.Contains(".customer-experience .public-products-section .public-products-search input {\n    width: 100%;\n    min-width: 0;\n    min-height: 0;\n    height: auto;\n    margin: 0;\n    padding: 0;\n    border: 0;", customerStyles);
        Assert.Contains("@media (max-width: 575.98px)", customerStyles);
    }
}
