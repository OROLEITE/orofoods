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
}
