namespace Orofoods.Web.Tests.Views;

public class HomeProductViewTests
{
    [Fact]
    public void Product_detail_groups_customer_actions_with_bootstrap_spacing()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Home", "Product.cshtml"));

        Assert.Contains("product-detail-actions d-flex", markup);
        Assert.Contains("gap-2", markup);
    }

    [Fact]
    public void Home_product_card_opens_the_public_product_detail()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Home", "Index.cshtml"));

        Assert.Contains("home-product-card\" asp-controller=\"Home\" asp-action=\"Product\" asp-route-id=\"@product.Id\"", markup);
    }

    [Fact]
    public void Product_detail_offers_administrators_a_return_to_administration()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Home", "Product.cshtml"));

        Assert.Contains("User.IsInRole(\"Administrador\")", markup);
        Assert.Contains("asp-area=\"Admin\" asp-controller=\"Dashboard\"", markup);
    }

    [Fact]
    public void Product_availability_uses_text_not_an_html_entity_inside_razor_code()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Home", "Product.cshtml"));

        Assert.Contains("? \"Disponível\" : \"Indisponível no momento\"", markup);
        Assert.DoesNotContain("? \"Dispon&iacute;vel\"", markup);
    }
}
