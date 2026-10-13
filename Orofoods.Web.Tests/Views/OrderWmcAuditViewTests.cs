namespace Orofoods.Web.Tests.Views;

public class OrderWmcAuditViewTests
{
    [Fact]
    public void Admin_order_details_show_the_complete_wmc_export_history()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var controller = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Controllers", "OrdersController.cs"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Orders", "Details.cshtml"));

        Assert.Contains("WmcExportAudits", controller);
        Assert.Contains("Hist&oacute;rico de exporta&#231;&otilde;es WMC", view);
    }

    [Fact]
    public void Admin_order_details_use_compact_layout_without_raw_html_entities_in_code_strings()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Orders", "Details.cshtml"));

        Assert.Contains("order-details-hero", view);
        Assert.Contains("order-wmc-technical", view);
        Assert.Contains("order-history-more", view);
        Assert.DoesNotContain("?? \"N&atilde;o", view);
        Assert.DoesNotContain("\"Eleg&iacute;vel", view);
        Assert.DoesNotContain("\"Necess&aacute;ria\"", view);
    }

    [Fact]
    public void Exported_pending_import_diagnostic_uses_specific_compact_copy_and_translated_status()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Orders", "Details.cshtml"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-order-details.css"));

        Assert.Contains("wmcAwaitingImportConfirmation", view);
        Assert.Contains("Status == Orofoods.Web.Models.Integrations.WmcOrderExportStatus.Exported", view);
        Assert.Contains("RequiresManualReview != true", view);
        Assert.Contains("TXT gerado &mdash; aguardando confirma&ccedil;&atilde;o de importa&ccedil;&atilde;o", view);
        Assert.Contains("Nova exporta\\u00e7\\u00e3o bloqueada", view);
        Assert.Contains("@wmcManualExportDiagnostic.BlockReason", view);
        Assert.Contains(": \"Bloqueado\")", view);
        Assert.Contains("Exported ? \"TXT gerado\"", view);
        Assert.Contains("EXPORTA&#199;&#195;O WMC</small><strong>@(wmcImportConfirmed ? \"Importa\\u00e7\\u00e3o WMC confirmada\" : wmcState?.Status == Orofoods.Web.Models.Integrations.WmcOrderExportStatus.Exported ? \"TXT gerado\"", view);
        Assert.DoesNotContain("alert alert-warning order-wmc-block-reason", view);
        Assert.Contains("background: var(--order-detail-surface-secondary);", styles);
        Assert.Contains("color: var(--order-detail-muted);", styles);
        Assert.Contains("overflow-wrap: anywhere;", styles);
        Assert.Contains("html[data-theme=\"dark\"] body.admin-authenticated:not(:has(.whatsapp-inbox-page)) .admin-order-details-page .order-wmc-eligibility--pending-import", styles);
        Assert.Contains("@media (max-width: 640px)", styles);
    }

    [Fact]
    public void Admin_order_monitor_shows_only_wmc_badges_and_keeps_actions_on_the_details_page()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Orders", "Index.cshtml"));
        var details = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Orders", "Details.cshtml"));

        Assert.Contains("\"Pendente\"", view);
        Assert.Contains("\"TXT gerado\"", view);
        Assert.Contains("\"Importado\"", view);
        Assert.Contains("\"Erro\"", view);
        Assert.DoesNotContain("asp-action=\"SendToWmc\"", view);
        Assert.DoesNotContain("asp-action=\"ConfirmWmcImported\"", view);
        Assert.Contains("Enviar ao WMC", details);
        Assert.Contains("Confirmar importação", details);
        Assert.Contains("Confirmar exportado", details);
        Assert.Contains("Confirmar que não chegou ao WMC", details);
        Assert.Contains("asp-action=\"SendToWmc\"", details);
        Assert.Contains("asp-action=\"ConfirmWmcImported\"", details);
    }

    [Fact]
    public void Confirming_wmc_import_uses_an_accessible_bootstrap_modal_and_keeps_the_antiforgery_post()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Orders", "Details.cshtml"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-order-details.css"));
        var integrationView = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Integrations", "Index.cshtml"));

        Assert.DoesNotContain("onclick=\"return confirm(", view);
        Assert.Contains("data-bs-toggle=\"modal\"", view);
        Assert.Contains("data-bs-target=\"#confirm-wmc-import-modal\"", view);
        Assert.Contains("id=\"confirm-wmc-import-modal\"", view);
        Assert.Contains("data-wmc-confirmation-form", view);
        Assert.Contains("asp-action=\"ConfirmWmcImported\" method=\"post\"", view);
        Assert.Contains("name=\"id\" value=\"@Model.Id\"", view);
        Assert.Contains("@Model.Number", view);
        Assert.Contains("TXT gerado — aguardando importação", view);
        Assert.Contains("form.dataset.wmcSubmitting", view);
        Assert.Contains("WmcExportOutcome.Imported => \"Importação confirmada\"", view);
        Assert.Contains("WmcExportOutcome.Imported => \"Importado\"", integrationView);
        Assert.Contains("html[data-theme=\"dark\"]", styles);
        Assert.Contains("@media (max-width: 575.98px)", styles);
    }

    [Fact]
    public void Manually_imported_wmc_order_shows_positive_confirmation_and_hides_erp_reprocess()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Orders", "Details.cshtml"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-order-details.css"));

        Assert.Contains("var wmcImportConfirmed = wmcState?.Status == Orofoods.Web.Models.Integrations.WmcOrderExportStatus.Imported;", view);
        Assert.Contains("Importa\\u00e7\\u00e3o WMC confirmada", view);
        Assert.Contains("wmcImportConfirmed ? \"imported\"", view);
        Assert.Contains("!wmcManualExportDiagnostic.CanExport && !wmcImportConfirmed", view);
        Assert.Contains("&& !wmcImportConfirmed", view);
        Assert.Contains("order-wmc-eligibility--imported", styles);
        Assert.Contains("html[data-theme=\"dark\"] body.admin-authenticated:not(:has(.whatsapp-inbox-page)) .admin-order-details-page .order-wmc-eligibility--imported", styles);
    }

    [Fact]
    public void Order_details_only_offer_integration_reprocessing_for_approved_orders_and_break_audit_ties()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Orders", "Details.cshtml"));

        Assert.Contains("@if (Model.Status == OrderStatus.Approved && Model.IntegrationStatus != IntegrationStatus.Processing && !wmcImportConfirmed)", view);
        Assert.Contains("Model.WmcExportAudits.OrderByDescending(x => x.ExportedAt).ThenByDescending(x => x.Id)", view);
    }
}
