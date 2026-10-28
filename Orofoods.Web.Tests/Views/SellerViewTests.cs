namespace Orofoods.Web.Tests.Views;

public class SellerViewTests
{
    [Fact]
    public void Seller_customer_search_uses_supported_terms_and_the_dark_seller_layout_placeholder_style()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var view = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Areas", "Vendedor", "Views", "Customers", "Index.cshtml"));
        var sellerLayout = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Areas", "Vendedor", "Views", "_SellerLayout.cshtml"));
        var layout = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Views", "Shared", "_Layout.cshtml"));
        var styles = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "wwwroot", "css", "seller-area.css"));

        Assert.Contains("Buscar por nome, razão social, CNPJ ou código WMC", view);
        Assert.Contains("seller-app-page", sellerLayout);
        Assert.Contains("<body class=\"@bodyClass\">", layout);
        Assert.Contains("seller-customer-search", view);
        Assert.Contains("body.seller-app-page .seller-customer-search::placeholder", styles);
        Assert.Contains("color: var(--seller-muted)", styles);
        Assert.Contains("opacity: 1", styles);
        Assert.DoesNotContain("html[data-theme=\"dark\"] body.seller-app-page .seller-customer-search::placeholder", styles);
    }

    [Fact]
    public void Seller_order_draft_editor_does_not_require_manual_delivery_address()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var editor = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Areas", "Vendedor", "Views", "OrderDrafts", "Editor.cshtml"));

        Assert.DoesNotContain("name=\"DeliveryAddressId\"", editor);
        Assert.DoesNotContain("id=\"delivery-address\"", editor);
        Assert.Contains("name=\"PaymentTermId\"", editor);
    }

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
        Assert.Contains("Digitar pedido", workspace);
        Assert.Contains("Meus rascunhos", workspace);
        Assert.Contains("Pedidos", workspace);
        Assert.DoesNotContain("Ver catÃ¡logo", workspace);
        Assert.DoesNotContain("Carrinho", workspace);
        Assert.DoesNotContain("asp-controller=\"Checkout\"", workspace);
        Assert.DoesNotContain("asp-controller=\"Admin\"", dashboard + workspace, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("checkout", workspace, StringComparison.OrdinalIgnoreCase);
    }
}
