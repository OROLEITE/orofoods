namespace Orofoods.Web.Tests.Views;

public class PriceTableItemsViewTests
{
    [Fact]
    public void Price_table_items_uses_the_standard_back_button()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "PriceTables", "Items.cshtml"));

        Assert.Contains("asp-action=\"Index\" class=\"btn admin-secondary-action\"", view);
        Assert.Contains("fa-arrow-left", view);
    }
}
