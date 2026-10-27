namespace Orofoods.Web.Tests.Views;

public class AdminOrderDetailsVisualTests
{
    [Fact]
    public void Order_details_expose_a_read_only_wmc_export_diagnostic()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var controller = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Controllers", "OrdersController.cs"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Orders", "Details.cshtml"));

        Assert.Contains("Diagn&oacute;stico WMC", view);
        Assert.Contains("ManualExportDiagnostic", controller);
        Assert.Contains("PaymentTerm?.Code", view);
        Assert.Contains("DaysUntilDue", view);
        Assert.Contains("Customer?.Cnpj", view);
        Assert.Contains("Customer?.WmcCode", view);
        Assert.Contains("informativo", view);
        Assert.Contains("Product?.WmcCode", view);
        Assert.Contains("SalesUnitCodeSnapshot", view);
        Assert.Contains("WmcOrderExportState", controller);
        Assert.Contains("BlockReason", view);
        Assert.Contains("CanExport", view);
        Assert.DoesNotContain("asp-action=\"SendToWmc\"", view.Substring(0, view.IndexOf("order-wmc-actions", StringComparison.Ordinal)));
    }

    [Fact]
    public void Admin_wmc_controls_separate_first_export_download_rejection_and_regeneration()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var controller = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Controllers", "OrdersController.cs"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Orders", "Index.cshtml"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-orders-list.css"));
        var details = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Orders", "Details.cshtml"));

        Assert.Contains("[Area(\"Admin\"), Authorize(Roles = \"Administrador\")]", controller);
        foreach (var action in new[] { "SendToWmc", "DownloadWmcTxt", "RejectWmcTxt", "RegenerateWmcTxt", "ConfirmWmcImported", "ConfirmPriorWmcExport" })
        {
            Assert.DoesNotContain($"asp-action=\"{action}\"", view);
            Assert.Contains($"asp-action=\"{action}\"", details);
        }
        Assert.DoesNotContain("<form", view.Substring(view.IndexOf("<tbody>", StringComparison.Ordinal)));
        Assert.DoesNotContain("name=\"reason\"", view);
        Assert.Contains("wmc-status-badge", view);
        Assert.Contains("\"TXT gerado\"", view);
        Assert.Contains("\"Rejeitado\"", view);
        Assert.Contains("\"Importado\"", view);
        Assert.Contains("CurrentArtifactId is not null", details);
        Assert.Contains("maxlength=\"1000\" required", details);
        Assert.Contains("btn-primary wmc-send-button", details);
        Assert.Contains("btn-success order-wmc-confirm-button", details);
        Assert.Contains("height: 52px", styles);
        Assert.Contains("[HttpGet]", controller);
        Assert.Contains("[HttpPost, ValidateAntiForgeryToken]", controller);
    }

    [Fact]
    public void Wmc_post_actions_use_details_prg_and_disable_duplicate_submissions()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var controller = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Controllers", "OrdersController.cs"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Orders", "Details.cshtml"));

        Assert.Contains("return RedirectToAction(nameof(Details), new { id });", controller);
        Assert.Contains("data-wmc-submit-once", view);
        Assert.Contains("wmcSubmitting", view);
        Assert.Contains("WmcManualExportMessage", view);
        Assert.Contains("WmcManualExportError", view);
        var sendStart = controller.IndexOf("public async Task<IActionResult> SendToWmc", StringComparison.Ordinal);
        var sendEnd = controller.IndexOf("[HttpGet]", sendStart, StringComparison.Ordinal);
        Assert.DoesNotContain("return WmcFile(result);", controller[sendStart..sendEnd]);
        var regenerateStart = controller.IndexOf("public async Task<IActionResult> RegenerateWmcTxt", StringComparison.Ordinal);
        var regenerateEnd = controller.IndexOf("private static FileContentResult WmcFile", regenerateStart, StringComparison.Ordinal);
        Assert.DoesNotContain("return WmcFile(result);", controller[regenerateStart..regenerateEnd]);
    }

    [Fact]
    public void Order_details_groups_operational_information_without_changing_actions()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var controller = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Controllers", "OrdersController.cs"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Orders", "Details.cshtml"));

        foreach (var component in new[]
        {
            "order-details-header",
            "order-details-summary",
            "order-payment-card",
            "order-point-audit-list",
            "order-actions-card",
            "order-history-grid",
            "order-history-list",
            "order-empty-state",
            "order-items-heading"
        })
        {
            Assert.Contains(component, view);
        }

        Assert.Contains("asp-action=\"UpdateStatus\"", view);
        Assert.DoesNotContain("asp-action=\"ReprocessIntegration\"", view);
        Assert.Contains("return SendToWmc(id, cancellationToken);", controller);
        Assert.Contains("asp-action=\"StartPointCharge\"", view);
        Assert.Contains("asp-action=\"RefreshPointCharge\"", view);
        Assert.Contains("asp-action=\"CancelPointCharge\"", view);
        Assert.Contains("data-delivered-payment-guard", view);
        Assert.Contains("data-card-on-delivery-details", view);
        Assert.Contains("<dt>Motorista da cobrança</dt>", view);
        Assert.Contains("<dt>Terminal</dt>", view);
        Assert.Contains("<dt>Última tentativa Point</dt>", view);
        Assert.Contains("<dt>External Order ID</dt>", view);
        Assert.Contains("<dt>Última atualização</dt>", view);
        Assert.Contains("@audit.PreviousStatus?.ToDisplayName() → @audit.NewStatus.ToDisplayName()", view);
        Assert.Contains("@audit.AttemptNumber", view);
        Assert.Contains("@(audit.AdminUser?.Email ?? \"Sistema\")", view);
        Assert.Contains("@audit.OccurredAt.ToLocalTime().ToString(\"dd/MM/yyyy HH:mm:ss\")", view);
        Assert.Contains("order-point-audit-transition--success", view);
        Assert.Contains("order-point-audit-transition--info", view);
        Assert.Contains("order-point-audit-transition--neutral", view);
        Assert.Contains("Model.StatusHistory.OrderByDescending(x => x.ChangedAt)", view);
        Assert.Contains("Model.WmcExportAudits.OrderByDescending(x => x.ExportedAt)", view);
        Assert.Contains("Nenhuma exporta&#231;&#227;o WMC registrada para este pedido.", view);
        Assert.DoesNotContain("asp-action=\"ReprocessIntegration\"", view);
        Assert.Contains("item.UnitPrice.ToString(\"C\")", view);
        Assert.Contains("item.Subtotal.ToString(\"C\")", view);
        Assert.Contains("item.Quantity", view);
        Assert.Contains("item.SkuSnapshot", view);
        Assert.Contains("item.ProductNameSnapshot", view);
    }

    [Fact]
    public void Order_details_highlights_customer_and_keeps_order_number_secondary()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Orders", "Details.cshtml"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-order-details.css"));

        Assert.Contains("<small>CLIENTE</small>", view);
        Assert.Contains("<h1>@Model.Customer?.TradeName</h1>", view);
        Assert.Contains("class=\"order-details-order-number\"", view);
        Assert.Contains("class=\"order-details-total\"", view);
        Assert.Contains("TOTAL DO PEDIDO", view);
        Assert.Contains(".order-details-order-number", styles);
        Assert.Contains(".order-details-total", styles);
        Assert.Contains("@media (max-width: 640px)", styles);
    }

    [Fact]
    public void Wmc_rejection_reason_is_requested_in_a_modal_and_actions_share_a_responsive_row()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var controller = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Controllers", "OrdersController.cs"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Orders", "Details.cshtml"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-order-details.css"));
        var rejectionModalIndex = view.IndexOf("id=\"reject-wmc-modal\"", StringComparison.Ordinal);
        var rejectionFormIndex = view.IndexOf("asp-action=\"RejectWmcTxt\" method=\"post\"", StringComparison.Ordinal);

        Assert.Contains("data-bs-target=\"#reject-wmc-modal\"", view);
        Assert.True(rejectionModalIndex >= 0);
        Assert.True(rejectionFormIndex > rejectionModalIndex);
        Assert.Contains("name=\"reason\"", view);
        Assert.Contains("maxlength=\"1000\" required", view);
        Assert.DoesNotContain("id=\"wmc-reject-@Model.Id\"", view);
        Assert.DoesNotContain("placeholder=\"Motivo da rejeição\"", view);
        Assert.Contains("Cancelar", view);
        Assert.Contains("Confirmar rejei&ccedil;&atilde;o", view);
        Assert.Contains("[HttpPost, ValidateAntiForgeryToken]", controller);
        Assert.Contains(".order-wmc-actions-layout", styles);
        Assert.Contains(".order-wmc-action-context", styles);
        Assert.Contains(".order-wmc-actions-bar", styles);
        Assert.Contains("justify-content: space-between;", styles);
        Assert.Contains("@media (max-width: 640px)", styles);
    }

    [Fact]
    public void Order_details_styles_are_page_scoped_compact_and_responsive()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-order-details.css"));

        Assert.Contains(".admin-authenticated .admin-order-details-page .order-details-layout", styles);
        Assert.Contains(".admin-authenticated .admin-order-details-page .order-details-summary", styles);
        Assert.Contains(".admin-authenticated .admin-order-details-page .order-payment-card", styles);
        Assert.Contains(".admin-authenticated .admin-order-details-page .order-point-audit-list", styles);
        Assert.Contains(".admin-authenticated .admin-order-details-page .order-actions-card", styles);
        Assert.Contains(".admin-authenticated .admin-order-details-page .order-history-grid", styles);
        Assert.Contains(".admin-authenticated .admin-order-details-page .order-items-heading", styles);
        Assert.Contains("@media (max-width: 900px)", styles);
        Assert.Contains("@media (max-width: 640px)", styles);
        Assert.Contains("overflow-wrap: anywhere", styles);
        Assert.Contains("padding: 6px 8px;", styles);
        Assert.Contains("background: #FFF8E5;", styles);
        Assert.Contains("background: #422F12;", styles);
        Assert.Contains("body.admin-authenticated:not(:has(.whatsapp-inbox-page)) .admin-order-details-page .order-payment-card", styles);
        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr));", styles);
        Assert.Contains("justify-content: flex-start;", styles);
        Assert.DoesNotContain("grid-template-columns: minmax(0, 520px) minmax(0, 360px);", styles);
        Assert.DoesNotContain("max-width: 280px;", styles);
        Assert.DoesNotContain("width: min(100%, 1080px);", styles);
        Assert.DoesNotContain("width: min(88%, 1200px);", styles);
        Assert.Matches(@"\.admin-authenticated \.admin-order-details-page \.order-point-audit-main\s*\{\s*align-items:\s*flex-start;\s*flex-direction:\s*column;", styles);
        Assert.Contains("width: 100%;", styles);
        Assert.Contains("grid-template-columns: minmax(80px, .5fr) minmax(0, 2.4fr) minmax(70px, .5fr) minmax(110px, .8fr) minmax(110px, .8fr);", styles);
        Assert.Contains("admin-order-details.css", File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Orders", "Details.cshtml")));
    }

    [Fact]
    public void Admin_order_details_keep_operations_right_and_move_histories_to_a_full_width_expander()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Orders", "Details.cshtml"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-order-details.css"));

        Assert.Contains("order-details-status-strip", view);
        Assert.Contains("order-details-primary", view);
        Assert.Contains("order-details-secondary", view);
        Assert.Contains("<details class=\"order-detail-section order-details-history-band\"", view);
        var secondaryStart = view.IndexOf("<aside class=\"order-details-secondary\"", StringComparison.Ordinal);
        var secondaryEnd = view.IndexOf("</aside>", secondaryStart, StringComparison.Ordinal);
        var orderActionsStart = view.IndexOf("<section class=\"order-actions-card\"", StringComparison.Ordinal);
        var historyBandStart = view.IndexOf("<details class=\"order-detail-section order-details-history-band\"", StringComparison.Ordinal);
        Assert.True(orderActionsStart > secondaryStart && orderActionsStart < secondaryEnd,
            "General order actions should be placed in the secondary actions column.");
        Assert.True(historyBandStart > secondaryEnd,
            "Status, WMC export, and fiscal snapshot history should appear in the full-width band below the two columns.");
        Assert.Contains("order-commercial-facts", view);
        Assert.Contains("order-payment-overview", view);
        Assert.Contains("<details class=\"order-history-panel order-cnpj-snapshot", view);
        Assert.Contains("<details class=\"order-detail-section order-point-audit", view);
        Assert.Contains("<details class=\"order-history-panel", view);
        Assert.Contains("<details class=\"order-history-panel\" aria-labelledby=\"order-status-history-title\">", view);
        Assert.Contains("<details class=\"order-history-panel\" aria-labelledby=\"order-wmc-history-title\">", view);
        Assert.True(view.IndexOf("order-status-history-title", historyBandStart, StringComparison.Ordinal) > secondaryEnd);
        Assert.True(view.IndexOf("order-wmc-history-title", historyBandStart, StringComparison.Ordinal) > secondaryEnd);
        Assert.True(view.IndexOf("data-customer-cnpj-snapshot", historyBandStart, StringComparison.Ordinal) > secondaryEnd);
        Assert.Contains("<details class=\"order-wmc-technical\"", view);
        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr));", styles);
        Assert.Contains("width: calc(100% - 24px);", styles);
        Assert.Contains("max-width: 1800px;", styles);
        Assert.Contains("@media (max-width: 1024px)", styles);
        Assert.Contains("font-size: 13px;", styles);
        Assert.Contains("min-width: 0;", styles);
    }

    [Fact]
    public void Order_details_have_compact_breadcrumb_and_responsive_wmc_action_colors()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Orders", "Details.cshtml"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-order-details.css"));

        Assert.Contains("<nav class=\"order-details-breadcrumb\"", view);
        Assert.Contains("<a asp-action=\"Index\"", view);
        Assert.Contains("Pedidos", view);
        Assert.Contains("aria-current=\"page\">Detalhes</span>", view);
        Assert.DoesNotContain("Voltar</a>", view);
        Assert.Contains("class=\"order-information-missing\"", view);
        Assert.Contains("fa-circle-info", view);
        Assert.Contains("order-wmc-download-button", view);
        Assert.Contains("order-wmc-reject-button", view);
        Assert.Contains("order-wmc-confirm-button", view);
        Assert.Matches(@"(?s)\.order-wmc-actions-bar\s*\{(?=[^}]*display:\s*flex)(?=[^}]*flex-wrap:\s*wrap)[^}]*\}", styles);
        Assert.Matches(@"(?s)\.admin-authenticated \.admin-order-details-page \.order-wmc-actions-bar > a,\s*\.admin-authenticated \.admin-order-details-page \.order-wmc-actions-bar > button,\s*\.admin-authenticated \.admin-order-details-page \.order-wmc-actions-bar > form\s*\{(?=[^}]*flex:\s*0 0 auto)(?=[^}]*white-space:\s*nowrap)[^}]*\}", styles);
        Assert.Contains("#38BDF8", styles);
        Assert.Contains("#2563EB", styles);
        Assert.Contains(":disabled", styles);
        Assert.Contains("@media (max-width: 640px)", styles);
    }

    [Fact]
    public void Wmc_actions_bound_long_filenames_and_keep_customer_cnpj_compact()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Orders", "Details.cshtml"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-order-details.css"));

        Assert.Contains("<strong title=\"@wmcLatestAudit.FileName\">@wmcLatestAudit.FileName</strong>", view);
        Assert.Contains("class=\"order-wmc-last-audit-meta\"", view);
        Assert.Contains("title=\"@(wmcLatestAudit.ExportedByEmail ??", view);
        Assert.Contains("class=\"order-wmc-fact-cnpj\"", view);
        Assert.Contains("class=\"order-wmc-cnpj\"", view);
        Assert.Matches(@"(?s)\.order-wmc-actions-layout\s*\{(?=[^}]*display:\s*grid)(?=[^}]*grid-template-columns:\s*minmax\(0,\s*1fr\))(?=[^}]*min-width:\s*0)[^}]*\}", styles);
        Assert.Matches(@"(?s)\.order-wmc-last-audit strong\s*\{(?=[^}]*min-width:\s*0)(?=[^}]*overflow:\s*hidden)(?=[^}]*text-overflow:\s*ellipsis)(?=[^}]*white-space:\s*nowrap)[^}]*\}", styles);
        Assert.Matches(@"(?s)\.order-wmc-fact-cnpj\s*\{(?=[^}]*grid-column:\s*1\s*/\s*-1)[^}]*\}", styles);
        Assert.Matches(@"(?s)\.order-wmc-cnpj\s*\{(?=[^}]*max-width:\s*100%)(?=[^}]*white-space:\s*nowrap)(?=[^}]*overflow:\s*hidden)[^}]*\}", styles);
        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr));", styles);
        Assert.Contains("@media (max-width: 1024px)", styles);
    }
}
