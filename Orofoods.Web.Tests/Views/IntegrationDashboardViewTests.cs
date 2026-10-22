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
        Assert.Contains("WmcManualExportService", controller);
        Assert.Contains("return ExportWmc(id, cancellationToken);", controller);
        Assert.Contains("status", view);
        Assert.Contains("wmcStatus", view);
        Assert.DoesNotContain("asp-action=\"Reprocess\"", view);
        Assert.Contains("Exportar WMC", view);
        Assert.Contains("&Uacute;ltima exporta&ccedil;&atilde;o WMC", view);
        Assert.Contains("WmcExportAudits.OrderByDescending", view);
        Assert.Contains("lastWmcExport.Source", view);
        Assert.Contains("lastWmcExport.Outcome", view);
        Assert.Contains("asp-controller=\"Orders\" asp-action=\"Details\"", view);
        Assert.Contains("order.Status == OrderStatus.Approved", view);
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

    [Fact]
    public void Integration_dashboard_keeps_wmc_export_metadata_in_a_compact_popover()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Integrations", "Index.cshtml"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-integrations.css"));

        Assert.Contains("erp-header-actions", view);
        Assert.Contains("erp-wmc-summary", view);
        Assert.Contains("erp-wmc-details-toggle", view);
        Assert.Contains("data-bs-toggle=\"popover\"", view);
        Assert.Contains("erp-wmc-details-template", view);
        Assert.Contains("new bootstrap.Popover", view);
        Assert.Contains("lastWmcExport.FileName", view);
        Assert.Contains("lastWmcExport.Error", view);
        Assert.Contains("asp-controller=\"Orders\" asp-action=\"Details\"", view);
        Assert.DoesNotContain("<small class=\"d-block\">@lastWmcExport.FileName</small>", view);
        Assert.Contains(".erp-header-actions", styles);
        Assert.Contains(".erp-wmc-summary", styles);
        Assert.Contains(".erp-wmc-details-template", styles);
    }

    [Fact]
    public void Integration_dashboard_shows_commercial_and_derived_wmc_statuses_and_gates_actions()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Integrations", "Index.cshtml"));
        var controller = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Controllers", "IntegrationsController.cs"));
        var statusDisplay = File.ReadAllText(Path.Combine(projectPath, "Models", "StatusDisplayExtensions.cs"));
        var orderDetails = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Orders", "Details.cshtml"));

        Assert.Contains("order.Status.ToDisplayName()", view);
        Assert.Contains("Aguardando aprovação", view);
        Assert.Contains("Aguardando envio WMC", view);
        Assert.Contains("Não elegível para envio", view);
        Assert.Contains("IntegrationStatus.Succeeded => \"Arquivo disponibilizado\"", statusDisplay);
        Assert.DoesNotContain("Importado WMC", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Importado WMC", orderDetails, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("order.Status == OrderStatus.Approved", view);
        Assert.Contains("WmcManualExportEligibleOrderIds", controller);
        Assert.Contains("WmcOrderExportState", controller);
        Assert.Contains("eligibleOrderIds.Contains(order.Id)", view);
        Assert.Contains("Importa&ccedil;&atilde;o confirmada", view);
        Assert.Contains("WmcOrderExportStatus.Rejected => \"erp-status-badge--failed\"", view);
        Assert.Contains("WmcOrderExportStatus.Processing => \"erp-status-badge--processing\"", view);
        Assert.Contains("wmcSummaryLabel", view);
        Assert.Contains("@if (manualState is not null)", view);
        Assert.Contains("@if (lastWmcExport is not null)", view);
        Assert.DoesNotContain("asp-action=\"Reprocess\"", view);
        Assert.Contains("WmcExportAudits.OrderByDescending(audit => audit.ExportedAt).ThenByDescending(audit => audit.Id)", controller);
        Assert.Contains("OrderByDescending(item => item.ExportedAt).ThenByDescending(item => item.Id)", view);
        Assert.DoesNotContain("OrderByDescending(audit => audit.ExportedAt).Select", controller);
    }
}
