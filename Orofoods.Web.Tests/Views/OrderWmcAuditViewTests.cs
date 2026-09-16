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
}
