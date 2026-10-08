namespace Orofoods.Web.Tests.Views;

public class AdminOrdersNewOrderViewTests
{
    [Fact]
    public void Orders_header_starts_the_existing_customer_selection_flow()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var ordersView = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Orders", "Index.cshtml"));

        Assert.Contains("Novo pedido", ordersView);
        Assert.Contains("asp-controller=\"Customers\"", ordersView);
        Assert.Contains("asp-route-context=\"new-order\"", ordersView);
        Assert.Contains("class=\"btn admin-primary-action", ordersView);
    }

    [Fact]
    public void Customer_list_exposes_assisted_order_only_for_approved_active_customers()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var customersView = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Customers", "Index.cshtml"));

        Assert.Contains("customer.IsActive && customer.Status == CustomerStatus.Approved", customersView);
        Assert.Contains("asp-action=\"NewOrder\"", customersView);
        Assert.Contains("asp-route-id=\"@customer.Id\"", customersView);
        Assert.Contains("class=\"btn btn-sm admin-primary-action\"", customersView);
        Assert.Contains("class=\"btn btn-sm @(isNewOrderContext ? \"btn-outline-dark\" : \"btn-gold\")\"", customersView);
        Assert.Contains("customer-row-actions--selection", customersView);
        Assert.Contains("table table-sm customer-list-table", customersView);
        Assert.Contains("btn-gold", customersView);
        Assert.Contains("context", customersView);
        Assert.Contains("Selecionar cliente para novo pedido", customersView);
        Assert.Contains("Escolha o cliente para iniciar o pedido assistido.", customersView);
        Assert.Contains("Voltar para pedidos", customersView);
        Assert.Contains("Clientes B2B", customersView);
    }
}
