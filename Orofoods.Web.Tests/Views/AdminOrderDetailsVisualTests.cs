namespace Orofoods.Web.Tests.Views;

public class AdminOrderDetailsVisualTests
{
    [Fact]
    public void Order_details_groups_operational_information_without_changing_actions()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
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
        Assert.Contains("asp-action=\"ReprocessIntegration\"", view);
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
        Assert.Contains("Integração ERP", view);
        Assert.Contains("item.UnitPrice.ToString(\"C\")", view);
        Assert.Contains("item.Subtotal.ToString(\"C\")", view);
        Assert.Contains("item.Quantity", view);
        Assert.Contains("item.SkuSnapshot", view);
        Assert.Contains("item.ProductNameSnapshot", view);
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
        Assert.Contains("grid-template-columns: minmax(0, 1fr) minmax(0, 1.15fr);", styles);
        Assert.Contains("justify-content: flex-start;", styles);
        Assert.Contains("grid-template-columns: minmax(0, 520px) minmax(0, 360px);", styles);
        Assert.Contains("max-width: 280px;", styles);
        Assert.Contains("width: min(100%, 1080px);", styles);
        Assert.Contains("width: min(88%, 1200px);", styles);
        Assert.Matches(@"\.admin-authenticated \.admin-order-details-page \.order-point-audit-main\s*\{\s*align-items:\s*flex-start;\s*flex-direction:\s*column;", styles);
        Assert.Contains("width: 100%;", styles);
        Assert.Contains("grid-template-columns: minmax(0, 1.7fr) minmax(90px, .55fr) minmax(130px, .8fr) minmax(120px, .75fr);", styles);
        Assert.Contains("admin-order-details.css", File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Orders", "Details.cshtml")));
    }
}
