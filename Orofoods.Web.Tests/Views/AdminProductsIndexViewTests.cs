namespace Orofoods.Web.Tests.Views;

public sealed class AdminProductsIndexViewTests
{
    [Fact]
    public void Product_table_separates_quantity_availability_and_active_status()
    {
        var view = ReadView("Index.cshtml");

        Assert.Contains("<th>Disponibilidade</th>", view, StringComparison.Ordinal);
        Assert.Contains("<th>Status</th>", view, StringComparison.Ordinal);
        Assert.Contains("stockByProductId.TryGetValue", view, StringComparison.Ordinal);
        Assert.Contains("PriceUnitLabel", view, StringComparison.Ordinal);
        Assert.Contains("Disponível", view, StringComparison.Ordinal);
        Assert.Contains("Indisponível", view, StringComparison.Ordinal);
        Assert.Contains("Ativo", view, StringComparison.Ordinal);
        Assert.Contains("Inativo", view, StringComparison.Ordinal);
        Assert.Contains("admin-mobile-table", view, StringComparison.Ordinal);
        Assert.DoesNotContain("IndisponÃ", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Product_operation_feedback_and_context_survive_post_redirect_get()
    {
        var index = ReadView("Index.cshtml");
        var edit = ReadView("Edit.cshtml");

        Assert.Contains("ProductSuccess", index, StringComparison.Ordinal);
        Assert.Contains("ProductError", index, StringComparison.Ordinal);
        Assert.Contains("role=\"status\"", index, StringComparison.Ordinal);
        Assert.Contains("role=\"alert\"", index, StringComparison.Ordinal);
        Assert.Contains("name=\"q\" value=\"@ViewBag.Query\"", index, StringComparison.Ordinal);
        Assert.Contains("name=\"includeInactive\"", index, StringComparison.Ordinal);
        Assert.Contains("name=\"page\" value=\"@ViewBag.Page\"", index, StringComparison.Ordinal);
        Assert.Contains("type=\"hidden\" name=\"q\" value=\"@ViewBag.Query\"", edit, StringComparison.Ordinal);
        Assert.Contains("type=\"hidden\" name=\"includeInactive\" value=\"@ViewBag.IncludeInactive\"", edit, StringComparison.Ordinal);
        Assert.Contains("type=\"hidden\" name=\"page\" value=\"@ViewBag.Page\"", edit, StringComparison.Ordinal);
        Assert.DoesNotContain("Ã", index + edit, StringComparison.Ordinal);
        Assert.DoesNotContain("�", index + edit, StringComparison.Ordinal);
    }

    private static string ReadView(string name)
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        return File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Products", name));
    }
}
