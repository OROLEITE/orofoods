namespace Orofoods.Web.Tests.Views;

public class CardOnDeliveryDisplayTests
{
    [Fact]
    public void Customer_checkout_and_order_views_explain_pending_card_on_delivery()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var checkout = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Views", "Portal", "Checkout.cshtml"));
        var success = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Views", "Portal", "Success.cshtml"));
        var order = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Views", "Portal", "Order.cshtml"));
        var script = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "wwwroot", "js", "checkout.js"));

        Assert.Contains("Pague no momento da entrega diretamente na maquininha.", checkout);
        Assert.Contains("Cartão na entrega", success);
        Assert.Contains("Aguardando pagamento", success);
        Assert.Contains("Cartão na entrega", order);
        Assert.Contains("Aguardando pagamento", order);
        Assert.Contains("selected?.dataset.code === \"CREDIT_CARD\"", script);
        Assert.Contains("selected?.dataset.code === \"CARD_ON_DELIVERY\"", script);
    }

    [Fact]
    public void Admin_displays_gated_point_controls_while_seller_success_remains_read_only()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var adminController = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Areas", "Admin", "Controllers", "OrdersController.cs"));
        var wmcController = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Areas", "Admin", "Controllers", "IntegrationsController.cs"));
        var adminView = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Areas", "Admin", "Views", "Orders", "Details.cshtml"));
        var sellerView = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Areas", "Vendedor", "Views", "Checkout", "Success.cshtml"));

        Assert.Contains("Include(x => x.Payments)", adminController);
        Assert.Contains("Include(item => item.PaymentTerm)", wmcController);
        Assert.Contains("Cartão na entrega", adminView);
        Assert.Contains("Aguardando pagamento", adminView);
        Assert.Contains("SITUAÇÃO FINANCEIRA", sellerView);
        Assert.Contains("@Model.FinancialStatus", sellerView);
        Assert.Contains("pointPaymentEnabled", adminView);
        Assert.Contains("StartPointCharge", adminView);
        Assert.Contains("cardOnDeliveryPayment?.Gateway == \"MercadoPagoPoint\"", adminView);
        Assert.Contains("RefreshPointCharge", adminView);
        Assert.Contains("CancelPointCharge", adminView);
        Assert.Contains("COBRAR NA MAQUININHA", adminView);
        Assert.Contains("PollPointCharge", adminController);
        Assert.Contains("admin-point-payment.js", adminView);
        Assert.Contains("External Order ID", adminView);
        Assert.Contains("DriverPaymentTerminalAssignment", adminController);
        Assert.DoesNotContain("Authorization", adminView, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Point", sellerView, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Point_payment_ui_uses_bounded_polling_and_portal_uses_persisted_payment_status()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var poller = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "wwwroot", "js", "admin-point-payment.js"));
        var order = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Views", "Portal", "Order.cshtml"));
        var success = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Views", "Portal", "Success.cshtml"));

        Assert.Contains("60_000", poller);
        Assert.Contains("5_000", poller);
        Assert.Contains("result.terminal", poller);
        Assert.Contains("Status: @cardPaymentLabel", order);
        Assert.Contains("Status: @cardPaymentLabel", success);
        Assert.Contains("PaymentStatus.Paid", order);
        Assert.Contains("PaymentStatus.Approved", success);
    }

    [Fact]
    public void Admin_order_details_shows_approved_state_and_disables_delivery_when_unpaid()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var adminView = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Areas", "Admin", "Views", "Orders", "Details.cshtml"));

        Assert.Contains("PaymentStatus.Approved", adminView);
        Assert.Contains("Status.ToDisplayName()", adminView);
        Assert.Contains("disabled", adminView);
        Assert.Contains("OrderStatusError", adminView);
    }
}
