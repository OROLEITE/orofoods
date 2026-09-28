namespace Orofoods.Web.Tests.Views;

public class IntegrationDashboardViewTests
{
    [Fact]
    public void Integration_dashboard_exposes_filters_and_reprocessing_actions()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var controller = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Controllers", "IntegrationsController.cs"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Integrations", "Index.cshtml"));

        Assert.Contains("Authorize(Roles = \"Administrador\")", controller);
        Assert.Contains("IntegrationStatus", controller);
        Assert.Contains("Reprocess", controller);
        Assert.Contains("ExportWmc", controller);
        Assert.Contains("WmcExportAuditService", controller);
        Assert.Contains("status", view);
        Assert.Contains("wmcStatus", view);
        Assert.Contains("Reprocess", view);
        Assert.Contains("Exportar WMC", view);
        Assert.Contains("&Uacute;ltima exporta&ccedil;&atilde;o WMC", view);
    }

    [Fact]
    public void Integration_dashboard_keeps_error_details_in_a_reusable_modal()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Integrations", "Index.cshtml"));

        Assert.Contains("hasIntegrationError", view);
        Assert.Contains("data-bs-toggle=\"modal\"", view);
        Assert.Contains("Ver detalhes", view);
        Assert.Contains("erpErrorModal", view);
        Assert.Contains("textContent = trigger.dataset.error", view);
        Assert.DoesNotContain("erp-error-detail-row", view);
    }
}
