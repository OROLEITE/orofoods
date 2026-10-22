namespace Orofoods.Web.Tests.Views;

public sealed class AdminCompactVisualRegressionTests
{
    [Fact]
    public void Wmc_products_use_compact_columns_and_accessible_block_reasons()
    {
        var view = ReadWebFile("Areas", "Admin", "Views", "Integrations", "Wmc.cshtml");
        var styles = ReadWebFile("wwwroot", "css", "admin-integrations.css");

        Assert.DoesNotContain("wmc-bimbo-product-name", view, StringComparison.Ordinal);
        Assert.Contains("class=\"wmc-import-table\"", view, StringComparison.Ordinal);
        Assert.Contains("class=\"wmc-description-cell\"", view, StringComparison.Ordinal);
        Assert.Contains("class=\"wmc-row-details\"", view, StringComparison.Ordinal);
        Assert.Contains("<summary>Detalhes</summary>", view, StringComparison.Ordinal);
        Assert.Contains("class=\"wmc-row-details-content\"", view, StringComparison.Ordinal);
        Assert.Contains("@item.Status", view, StringComparison.Ordinal);
        Assert.Contains("BlockReason", view, StringComparison.Ordinal);
        Assert.Contains(".wmc-product-table-scroll", styles, StringComparison.Ordinal);
        Assert.Contains("min-width:980px", styles, StringComparison.Ordinal);
        Assert.Contains("table-layout:fixed", styles, StringComparison.Ordinal);
        Assert.Contains(".wmc-description-cell", styles, StringComparison.Ordinal);
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
    public void Admin_customer_terms_are_read_only_and_source_is_wmc()
    {
        var view = ReadWebFile("Areas", "Admin", "Views", "Customers", "Edit.cshtml");
        var styles = ReadWebFile("wwwroot", "css", "admin-customer-config.css");

        Assert.Contains("Condição principal no WMC", view, StringComparison.Ordinal);
        Assert.Contains("Model.WmcPaymentConditionStatus", view, StringComparison.Ordinal);
        Assert.Contains("Model.WmcPaymentConditionEligible", view, StringComparison.Ordinal);
        Assert.Contains("WmcPaymentConditionSource", view, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"PaymentTermIds\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Definir prazo manualmente", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Prazo manual", view, StringComparison.Ordinal);
    }

    private static string ReadWebFile(params string[] segments)
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        return File.ReadAllText(Path.Combine([projectPath, .. segments]));
    }
}
