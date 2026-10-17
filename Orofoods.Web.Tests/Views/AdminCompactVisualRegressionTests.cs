namespace Orofoods.Web.Tests.Views;

public sealed class AdminCompactVisualRegressionTests
{
    [Fact]
    public void Bimbo_products_use_single_line_columns_and_accessible_block_reasons()
    {
        var view = ReadWebFile("Areas", "Admin", "Views", "Integrations", "Wmc.cshtml");
        var styles = ReadWebFile("wwwroot", "css", "admin-integrations.css");

        Assert.Contains("class=\"wmc-bimbo-product-name\"", view, StringComparison.Ordinal);
        Assert.Contains("class=\"wmc-bimbo-reason-details\"", view, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Ver motivos de bloqueio do produto @product.WmcCode\"", view, StringComparison.Ordinal);
        Assert.Contains("@foreach (var reason in product.Reasons)", view, StringComparison.Ordinal);
        Assert.Contains("class=\"wmc-bimbo-reason-list\"", view, StringComparison.Ordinal);
        Assert.Contains(".wmc-bimbo-status-scroll", styles, StringComparison.Ordinal);
        Assert.Contains("min-width:1120px", styles, StringComparison.Ordinal);
        Assert.Contains("white-space:nowrap", styles, StringComparison.Ordinal);
        Assert.Contains("overflow-x:auto", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Customer_configuration_uses_admin_dark_palette_and_cyan_primary_action()
    {
        var styles = ReadWebFile("wwwroot", "css", "admin-customer-config.css");

        Assert.Contains("--customer-config-page:#0B132B", styles, StringComparison.Ordinal);
        Assert.Contains("--customer-config-surface:#111827", styles, StringComparison.Ordinal);
        Assert.Contains("--customer-config-text:#E5E7EB", styles, StringComparison.Ordinal);
        Assert.Contains("background:var(--customer-config-page)", styles, StringComparison.Ordinal);
        Assert.Contains("background:#0284C7", styles, StringComparison.Ordinal);
        Assert.Contains("color:#FFFFFF", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Product_gallery_uses_compact_uncropped_ninety_pixel_thumbnails()
    {
        var styles = ReadWebFile("wwwroot", "css", "admin-products.css");

        Assert.Contains("grid-template-columns:repeat(auto-fill,minmax(260px,1fr))", styles, StringComparison.Ordinal);
        Assert.Contains("width:90px", styles, StringComparison.Ordinal);
        Assert.Contains("height:90px", styles, StringComparison.Ordinal);
        Assert.Contains("object-fit:contain", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Payment_terms_are_real_multi_value_checkboxes_with_responsive_layout()
    {
        var view = ReadWebFile("Areas", "Admin", "Views", "Customers", "Edit.cshtml");
        var styles = ReadWebFile("wwwroot", "css", "admin-customer-config.css");

        Assert.Contains("<fieldset class=\"customer-payment-terms\"", view, StringComparison.Ordinal);
        Assert.Contains("class=\"customer-payment-indicators\"", view, StringComparison.Ordinal);
        Assert.Contains("class=\"customer-payment-controls\"", view, StringComparison.Ordinal);
        Assert.Contains("@foreach (var term in Model.PaymentTerms)", view, StringComparison.Ordinal);
        Assert.Contains("type=\"checkbox\" name=\"PaymentTermIds\" value=\"@term.Value\"", view, StringComparison.Ordinal);
        Assert.Contains("checked=\"@(term.Selected ? \"checked\" : null)\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("<select asp-for=\"PaymentTermIds\"", view, StringComparison.Ordinal);
        Assert.Contains(".customer-payment-terms-grid", styles, StringComparison.Ordinal);
        Assert.Contains(".customer-payment-indicators", styles, StringComparison.Ordinal);
        Assert.Contains(".customer-payment-controls", styles, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns:repeat(auto-fit,minmax(210px,1fr))", styles, StringComparison.Ordinal);
    }

    private static string ReadWebFile(params string[] segments)
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        return File.ReadAllText(Path.Combine([projectPath, .. segments]));
    }
}
