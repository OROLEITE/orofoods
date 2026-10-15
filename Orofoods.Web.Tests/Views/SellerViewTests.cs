namespace Orofoods.Web.Tests.Views;

public class SellerViewTests
{
    [Fact]
    public void Seller_views_expose_only_workspace_actions()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var dashboard = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Areas", "Vendedor", "Views", "Dashboard", "Index.cshtml"));
        var sellerLayout = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Areas", "Vendedor", "Views", "_SellerLayout.cshtml"));
        var workspace = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Areas", "Vendedor", "Views", "Customers", "Details.cshtml"));

        Assert.Contains("Digitar pedido", dashboard);
        Assert.Contains("Meus rascunhos", dashboard);
        Assert.DoesNotContain("Minha carteira", dashboard);
        Assert.DoesNotContain("Ver carteira", dashboard);
        Assert.Contains("Minha carteira", sellerLayout);
        Assert.Contains("asp-controller=\"Customers\" asp-action=\"Index\"", sellerLayout);
        Assert.Contains("Ver catálogo", workspace);
        Assert.Contains("Pedidos", workspace);
        Assert.DoesNotContain("asp-controller=\"Admin\"", dashboard + workspace, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("checkout", workspace, StringComparison.OrdinalIgnoreCase);
    }
}
