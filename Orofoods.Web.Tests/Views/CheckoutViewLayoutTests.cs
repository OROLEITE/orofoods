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

    [Fact]
    public void Checkout_never_posts_raw_card_data_and_uses_the_browser_tokenization_boundary()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Portal", "Checkout.cshtml"));
        var script = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "js", "checkout.js"));

        Assert.DoesNotContain("asp-for=\"CardNumber\"", markup);
        Assert.DoesNotContain("asp-for=\"CardCvv\"", markup);
        Assert.DoesNotContain("asp-for=\"CardExpiryDate\"", markup);
        Assert.Contains("sdk.mercadopago.com/js/v2", markup);
        Assert.Contains("asp-for=\"CardToken\"", markup);
        Assert.Contains("asp-for=\"CardPaymentMethodId\"", markup);
        Assert.Contains("createCardToken", script);
    }

    [Fact]
    public void Checkout_disables_submission_for_every_payment_method_after_a_valid_submit()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Portal", "Checkout.cshtml"));
        var script = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "js", "checkout.js"));

        Assert.Contains("data-processing-label=\"Enviando pedido…\"", markup);
        Assert.Contains("if (!isCreditCardSelected())", script);
        Assert.Contains("setSubmittingState();", script);
        Assert.Contains("aria-busy", script);
        Assert.Contains("event.preventDefault();", script);
    }
}
