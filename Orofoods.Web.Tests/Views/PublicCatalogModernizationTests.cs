namespace Orofoods.Web.Tests.Views;

public sealed class PublicCatalogModernizationTests
{
    private static readonly string WebRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));

    [Fact]
    public void Products_page_uses_the_local_banner_and_approved_catalog_copy_without_a_second_cart_action()
    {
        var view = ReadWebFile("Views", "Home", "Products.cshtml");
        var header = ReadWebFile("Views", "Shared", "_SiteHeader.cshtml");
        var styles = ReadWebFile("wwwroot", "css", "catalog.css");

        Assert.Contains("CATÁLOGO OROFOODS", view, StringComparison.Ordinal);
        Assert.Contains("Produtos para sua opera&ccedil;&atilde;o.", view, StringComparison.Ordinal);
        Assert.Contains("P&atilde;es de alta qualidade para hamburguerias e food service.", view, StringComparison.Ordinal);
        Assert.Contains("catalogo-banner-orofoods.jpg", styles, StringComparison.Ordinal);
        Assert.Contains("catalog-header-cart", header, StringComparison.Ordinal);
        Assert.DoesNotContain("public-catalog-cart-row", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Quick_filters_include_tradicional_and_public_catalog_has_a_five_column_desktop_grid()
    {
        var view = ReadWebFile("Views", "Home", "Products.cshtml");
        var card = ReadWebFile("Views", "Shared", "_PublicProductCard.cshtml");
        var styles = ReadWebFile("wwwroot", "css", "catalog.css");

        Assert.Contains("data-catalog-filter=\"tradicional\"", view, StringComparison.Ordinal);
        Assert.Contains(">Tradicional</button>", view, StringComparison.Ordinal);
        Assert.DoesNotContain("data-catalog-filter=\"congelados\">Congelados", view, StringComparison.Ordinal);
        Assert.Contains("quickFilterTags.Add(\"tradicional\")", card, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: repeat(5, minmax(0, 1fr))", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 575.98px)", styles, StringComparison.Ordinal);
        Assert.Contains("#F4F7FC", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_quantity_can_be_changed_after_add_and_status_uses_renderable_portuguese_text()
    {
        var card = ReadWebFile("Views", "Shared", "_PublicProductCard.cshtml");
        var script = ReadWebFile("wwwroot", "js", "catalog.js");
        var styles = ReadWebFile("wwwroot", "css", "catalog.css");

        Assert.Contains("data-catalog-allow-quantity-update", card, StringComparison.Ordinal);
        Assert.Contains("Produto já está no carrinho.", card, StringComparison.Ordinal);
        Assert.DoesNotContain("Produto j&aacute; est&aacute; no carrinho", card, StringComparison.Ordinal);
        Assert.Contains("Quantidade do carrinho atualizada.", script, StringComparison.Ordinal);
        Assert.Contains("catalog-quantity-button", styles, StringComparison.Ordinal);
        Assert.Contains("width: 36px", styles, StringComparison.Ordinal);
        Assert.Contains("height: 36px", styles, StringComparison.Ordinal);
        Assert.Contains("gap: 6px", styles, StringComparison.Ordinal);
    }

    private static string ReadWebFile(params string[] segments) => File.ReadAllText(Path.Combine([WebRoot, .. segments]));
}
