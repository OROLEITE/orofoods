namespace Orofoods.Web.Tests.Views;

public class OrderHistoryPresentationTests
{
    [Fact]
    public void Customer_order_history_keeps_existing_filters_and_order_actions_in_a_compact_list()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Views", "Portal", "Orders.cshtml"));

        Assert.Contains("orders-history-row", view);
        Assert.Contains("name=\"q\"", view);
        Assert.Contains("name=\"status\"", view);
        Assert.Contains("asp-action=\"Order\"", view);
        Assert.Contains("asp-action=\"Repeat\"", view);
        Assert.Contains("orders-status-badge--@statusClass", view);
        Assert.Contains("order.Items.Count", view);
        Assert.Contains("order.Total.ToString(\"C\")", view);
    }

    [Fact]
    public void Customer_order_history_styles_desktop_rows_and_stacks_them_without_horizontal_overflow()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "customer-experience.css"));

        Assert.Contains(".orders-history-row", styles);
        Assert.Contains("grid-template-columns:", styles);
        Assert.Contains("grid-template-areas:", styles);
        Assert.Contains("@media (max-width: 1049.98px)", styles);
        Assert.Contains("overflow-wrap: anywhere", styles);
    }
}
