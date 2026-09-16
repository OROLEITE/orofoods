namespace Orofoods.Web.Tests.Views;

public class RazorQualityRegressionTests
{
    [Fact]
    public void Price_table_items_keeps_forms_outside_table_rows()
    {
        var view = ReadView("Areas", "Admin", "Views", "PriceTables", "Items.cshtml");

        Assert.DoesNotContain("<tr><form", view);
        Assert.Contains("form=\"price-row-@product.Id\"", view);
    }

    [Fact]
    public void Order_history_uses_portuguese_item_pluralization()
    {
        var view = ReadView("Views", "Portal", "Orders.cshtml");

        Assert.Contains("order.Items.Count == 1 ? \"item\" : \"itens\"", view);
        Assert.DoesNotContain("item(ns)", view);
    }

    [Fact]
    public void Login_uses_portuguese_copy_and_password_visibility_control()
    {
        var view = ReadView("Areas", "Identity", "Pages", "Account", "Login.cshtml");

        Assert.Contains("&Aacute;REA EXCLUSIVA", view);
        Assert.Contains("data-password-toggle=\"Input_Password\"", view);
        Assert.DoesNotContain("Ã", view);
    }

    private static string ReadView(params string[] segments)
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        return File.ReadAllText(Path.Combine([projectPath, .. segments]));
    }
}
