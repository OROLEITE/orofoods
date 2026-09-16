namespace Orofoods.Web.Tests.Views;

public class CheckoutViewLayoutTests
{
    [Fact]
    public void Checkout_uses_structured_delivery_payment_and_summary_panels()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Portal", "Checkout.cshtml"));

        Assert.Contains("checkout-form-grid", markup);
        Assert.Contains("checkout-panel", markup);
        Assert.Contains("checkout-summary", markup);
        Assert.Contains("asp-action=\"Catalog\"", markup);
        Assert.Contains("Adicionar produtos", markup);
    }
}
