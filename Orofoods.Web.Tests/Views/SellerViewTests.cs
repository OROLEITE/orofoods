namespace Orofoods.Web.Tests.Views;

public class SellerViewTests
{
    [Fact]
    public void Seller_views_expose_only_workspace_actions()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var dashboard = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Areas", "Vendedor", "Views", "Dashboard", "Index.cshtml"));
        var workspace = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Areas", "Vendedor", "Views", "Customers", "Details.cshtml"));

        Assert.Contains("Minha carteira", dashboard);
        Assert.Contains("Ver catálogo", workspace);
        Assert.Contains("Ver pedidos", workspace);
        Assert.DoesNotContain("asp-controller=\"Admin\"", dashboard + workspace, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("checkout", workspace, StringComparison.OrdinalIgnoreCase);
    }
}