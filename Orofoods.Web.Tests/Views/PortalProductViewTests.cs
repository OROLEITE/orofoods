namespace Orofoods.Web.Tests.Views;

public class PortalProductViewTests
{
    [Fact]
    public void Product_detail_uses_the_effective_resolved_price()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Portal", "Product.cshtml"));

        Assert.Contains("ResolvedPrice", markup);
        Assert.Contains("EffectivePrice", markup);
        Assert.Contains("IsCommerciallyAvailable", markup);
    }

    [Fact]
    public void Catalog_adds_products_to_the_cart_instead_of_favoriting_them()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Portal", "Catalog.cshtml"));

        Assert.Contains("asp-action=\"AddToCart\"", markup);
        Assert.Contains("Adicionar ao carrinho", markup);
        Assert.Contains("IsCommerciallyAvailable", markup);
        Assert.DoesNotContain("Favoritar", markup);
    }
}
