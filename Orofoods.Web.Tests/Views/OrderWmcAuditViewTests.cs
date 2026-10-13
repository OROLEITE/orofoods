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
    public void Order_details_only_offer_integration_reprocessing_for_approved_orders_and_break_audit_ties()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Orders", "Details.cshtml"));

        Assert.Contains("@if (Model.Status == OrderStatus.Approved && Model.IntegrationStatus != IntegrationStatus.Processing)", view);
        Assert.Contains("Model.WmcExportAudits.OrderByDescending(x => x.ExportedAt).ThenByDescending(x => x.Id)", view);
    }
}
