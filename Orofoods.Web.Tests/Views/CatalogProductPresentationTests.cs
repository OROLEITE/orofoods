namespace Orofoods.Web.Tests.Views;

public sealed class CatalogProductPresentationTests
{
    private static string WebProjectPath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));

    [Fact]
    public void Customer_catalog_displays_the_presented_product_name()
    {
        var view = File.ReadAllText(Path.Combine(WebProjectPath, "Views/Portal/Catalog.cshtml"));

        Assert.Contains("CatalogProductNamePresentation.DisplayName(product.Name)", view);
        Assert.Contains("<h3>@displayName</h3>", view);
        Assert.Contains("Quantidade de @displayName", view);
    }

    [Fact]
    public void Seller_catalog_displays_the_name_presented_by_its_catalog_service()
    {
        var service = File.ReadAllText(Path.Combine(WebProjectPath, "Services/Sellers/SellerCatalogService.cs"));
        var view = File.ReadAllText(Path.Combine(WebProjectPath, "Areas/Vendedor/Views/Catalog/Index.cshtml"));

        Assert.Contains("Name = CatalogProductNamePresentation.DisplayName(product.Name)", service);
        Assert.Contains("<h2 class=\"h5 seller-product-name\">@product.Name</h2>", view);
        Assert.Contains("alt=\"@product.Name\"", view);
    }
}
