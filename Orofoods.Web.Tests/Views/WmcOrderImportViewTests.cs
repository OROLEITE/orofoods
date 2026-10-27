namespace Orofoods.Web.Tests.Views;

public sealed class WmcOrderImportViewTests
{
    [Fact]
    public void Pending_wmc_grid_confirms_the_selected_order_and_posts_with_antiforgery()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "WmcOrderImports", "Index.cshtml"));
        var script = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "js", "wmc-order-imports.js"));

        Assert.Contains("Importar ao WMC", view, StringComparison.Ordinal);
        Assert.Contains("@Html.AntiForgeryToken()", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-import-confirm", view, StringComparison.Ordinal);
        Assert.Contains("somente o pedido", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ReconcileNroPedido", view, StringComparison.Ordinal);
        Assert.Contains("Consultar número WMC", view, StringComparison.Ordinal);
        Assert.Contains("window.confirm", script, StringComparison.Ordinal);
        Assert.Contains("event.preventDefault()", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Seller_and_operator_navigation_exposes_the_shared_pending_wmc_grid()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var adminNavigation = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_AdminNavigation.cshtml"));
        var sellerNavigation = File.ReadAllText(Path.Combine(projectPath, "Areas", "Vendedor", "Views", "_SellerLayout.cshtml"));

        Assert.Contains("Pedidos pendentes WMC", adminNavigation, StringComparison.Ordinal);
        Assert.Contains("Pedidos pendentes WMC", sellerNavigation, StringComparison.Ordinal);
    }
}
