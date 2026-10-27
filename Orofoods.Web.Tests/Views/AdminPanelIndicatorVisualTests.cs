namespace Orofoods.Web.Tests.Views;

public class AdminPanelIndicatorVisualTests
{
    [Fact]
    public void Orders_customer_commercial_and_reports_metrics_use_the_shared_indicator_class()
    {
        var views = AdminViews();
        var orders = Read(views, "Orders", "Index.cshtml");
        var customer = Read(views, "Customers", "Details.cshtml");
        var commercial = Read(views, "Commercial", "Index.cshtml");
        var reports = Read(views, "Reports", "Index.cshtml");

        Assert.Contains("orders-monitor-summary", orders);
        Assert.Contains("admin-dashboard-indicator", orders);
        Assert.Contains("ViewData[\"AwaitingActionCount\"]", orders);
        Assert.Contains("ViewData[\"WmcAwaitingConfirmationCount\"]", orders);
        Assert.Contains("ViewData[\"WmcAttentionCount\"]", orders);
        Assert.Contains("@Model.OrderCount", customer);
        Assert.Contains("@Model.TotalPurchased.ToString(\"C\")", customer);
        Assert.Contains("admin-dashboard-indicator", customer);
        Assert.Contains("@Model.Attention.OverdueReturns", commercial);
        Assert.Contains("admin-dashboard-indicator--warning", commercial);
        Assert.Contains("@Model.Report.Revenue.ToString(\"C\")", reports);
        Assert.Contains("admin-dashboard-indicator", reports);
    }

    [Fact]
    public void Wmc_import_summaries_use_normal_warning_and_critical_indicators()
    {
        var view = Read(AdminViews(), "Integrations", "Wmc.cshtml");

        Assert.Contains("aria-label=\"Resumo da seleção\"", view);
        Assert.Contains("class=\"wmc-import-summary\"", view);
        Assert.Contains("admin-dashboard-indicator--normal", view);
        Assert.Contains("admin-dashboard-indicator--warning", view);
        Assert.Contains("admin-dashboard-indicator--critical", view);
        Assert.Contains("@Model.ImportResult.Created", view);
        Assert.Contains("@Model.ImportResult.Errors", view);
    }

    [Fact]
    public void Shared_indicator_styles_are_global_to_admin_pages_and_keep_theme_and_mobile_tokens()
    {
        var webRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var styles = File.ReadAllText(Path.Combine(webRoot, "wwwroot", "css", "admin-dashboard-indicators.css"));
        var layout = File.ReadAllText(Path.Combine(webRoot, "Views", "Shared", "_AdminLayout.cshtml"));

        Assert.Contains("admin-dashboard-indicators.css", layout);
        Assert.Contains(".admin-shell .admin-dashboard-indicator", styles);
        Assert.Contains("border-left: 4px solid", styles);
        Assert.Contains("#38BDF8", styles);
        Assert.Contains("#F59E0B", styles);
        Assert.Contains("#F87171", styles);
        Assert.Contains("background: #111827;", styles);
        Assert.Contains("background: #FFFFFF;", styles);
        Assert.Contains("@media (max-width: 650px)", styles);
    }

    [Fact]
    public void Product_table_and_whatsapp_conversation_cards_keep_their_existing_surfaces()
    {
        var views = AdminViews();

        Assert.DoesNotContain("admin-dashboard-indicator", Read(views, "Products", "Index.cshtml"));
        Assert.DoesNotContain("admin-dashboard-indicator", Read(views, "Inventory", "Index.cshtml"));
        Assert.DoesNotContain("admin-dashboard-indicator", Read(views, "PriceTables", "Index.cshtml"));
        Assert.DoesNotContain("admin-dashboard-indicator", Read(views, "SalesRepresentatives", "Index.cshtml"));
        Assert.DoesNotContain("admin-dashboard-indicator", Read(views, "WhatsApp", "Index.cshtml"));
    }

    private static string AdminViews() => Path.Combine(
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web")),
        "Areas", "Admin", "Views");

    private static string Read(string root, string controller, string file) =>
        File.ReadAllText(Path.Combine(root, controller, file));
}
