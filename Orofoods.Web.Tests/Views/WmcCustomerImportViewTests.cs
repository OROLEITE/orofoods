namespace Orofoods.Web.Tests.Views;

public sealed class WmcCustomerImportViewTests
{
    [Fact]
    public void Wmc_view_exposes_manual_customer_import_without_global_customer_sync()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Views", "Integrations", "Wmc.cshtml"));
        var controller = File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Controllers", "IntegrationsController.cs"));

        Assert.Contains("WmcCustomerPreview", view);
        Assert.Contains("WmcCustomerImport", view);
        Assert.Contains("CODVENDEDOR", view);
        Assert.Contains("Confirmar importação", view);
        Assert.DoesNotContain("asp-action=\"WmcSyncNow\"", view);
        Assert.Contains("WmcCustomerImportService", controller);
        Assert.Contains("RunExclusivelyAsync", controller);
        Assert.Contains("ValidateAntiForgeryToken", controller);
    }

    [Fact]
    public void Wmc_customer_filter_uses_compact_responsive_dark_mode_styles()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Views", "Integrations", "Wmc.cshtml"));
        var css = File.ReadAllText(Path.Combine(root, "wwwroot", "css", "admin-integrations.css"));

        Assert.Contains("wmc-customer-import-form", view);
        Assert.Contains("wmc-customer-filter-help", view);
        Assert.Contains("#1E293B", css);
        Assert.Contains("#334155", css);
        Assert.Contains("#94A3B8", css);
        Assert.Contains("#0E7490", css);
        Assert.Contains("wmc-customer-preview-button", css);
        Assert.Contains("max-width: 560px", css);
        Assert.Contains("flex-direction: column", css);
    }
}
