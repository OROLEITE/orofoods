using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Models.Validation;

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
        Assert.Contains("type=\"hidden\" name=\"includeInactive\" value=\"@((bool)ViewBag.IncludeInactive ? \"true\" : \"false\")\"", edit, StringComparison.Ordinal);
        Assert.Contains("type=\"hidden\" name=\"page\" value=\"@ViewBag.Page\"", edit, StringComparison.Ordinal);
        Assert.DoesNotContain("Ã", index + edit, StringComparison.Ordinal);
        Assert.DoesNotContain("�", index + edit, StringComparison.Ordinal);
    }

    [Fact]
    public void Product_table_keeps_actions_inside_table_cell_and_stabilizes_rows_and_dividers()
    {
        var index = ReadView("Index.cshtml");
        var styles = File.ReadAllText(Path.Combine(WebProjectPath(), "wwwroot", "css", "admin-products.css"));

        Assert.Contains("class=\"admin-product-actions\"", index, StringComparison.Ordinal);
        Assert.Contains(".admin-products-table td.admin-table-actions {", styles, StringComparison.Ordinal);
        Assert.Contains("display: table-cell", styles, StringComparison.Ordinal);
        Assert.Contains(".admin-product-actions {", styles, StringComparison.Ordinal);
        Assert.Contains(".admin-products-table > tbody > tr {", styles, StringComparison.Ordinal);
        Assert.Contains(".admin-products-table > tbody > tr > td {", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Product_list_hides_brand_and_keeps_compact_actions_aligned_without_desktop_wrap()
    {
        var index = ReadView("Index.cshtml");
        var styles = File.ReadAllText(Path.Combine(WebProjectPath(), "wwwroot", "css", "admin-products.css"));

        Assert.DoesNotContain("<small>@product.Brand</small>", index, StringComparison.Ordinal);
        Assert.Contains("class=\"admin-product-actions\"", index, StringComparison.Ordinal);
        Assert.Contains("justify-content: flex-end", styles, StringComparison.Ordinal);
        Assert.Contains("white-space: nowrap", styles, StringComparison.Ordinal);
        Assert.Contains("overflow-x: auto", styles, StringComparison.Ordinal);
        Assert.Contains("min-width: 1120px", styles, StringComparison.Ordinal);
        Assert.Contains("height: 56px", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Product_search_supports_autocomplete_off_debounce_and_server_pagination()
    {
        var index = ReadView("Index.cshtml");
        var scriptPath = Path.Combine(WebProjectPath(), "wwwroot", "js", "admin-products.js");
        var scripts = File.ReadAllText(scriptPath);

        Assert.Contains("autocomplete=\"off\"", index, StringComparison.Ordinal);
        Assert.Contains("Filtrar", index, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"page\"", index[..index.IndexOf("<table", StringComparison.Ordinal)], StringComparison.Ordinal);
        Assert.Contains("Nenhum produto encontrado", index, StringComparison.Ordinal);
        Assert.Contains("asp-route-page=\"@pageNumber\"", index, StringComparison.Ordinal);
        Assert.Contains("setTimeout", scripts, StringComparison.Ordinal);
        Assert.Contains(", 300)", scripts, StringComparison.Ordinal);
        Assert.Contains("requestSubmit()", scripts, StringComparison.Ordinal);
    }

    [Fact]
    public void Product_table_keeps_all_requested_columns_and_single_line_product_layout()
    {
        var index = ReadView("Index.cshtml");
        var styles = File.ReadAllText(Path.Combine(WebProjectPath(), "wwwroot", "css", "admin-products.css"));

        Assert.Contains("data-label=\"SKU\"", index, StringComparison.Ordinal);
        Assert.Contains("data-label=\"Categoria\"", index, StringComparison.Ordinal);
        Assert.Contains("@product.BasePrice.ToString(\"C\")", index, StringComparison.Ordinal);
        Assert.Contains("data-label=\"Estoque\"", index, StringComparison.Ordinal);
        Assert.Contains("data-label=\"Disponibilidade\"", index, StringComparison.Ordinal);
        Assert.Contains("data-label=\"Status\"", index, StringComparison.Ordinal);
        Assert.Contains("admin-products-table thead th:last-child", styles, StringComparison.Ordinal);
        Assert.Contains("text-align: right", styles, StringComparison.Ordinal);
        Assert.Contains("white-space: nowrap", styles, StringComparison.Ordinal);
        Assert.Contains("overflow-x: auto", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Product_edit_uses_portuguese_model_binding_messages()
    {
        var program = File.ReadAllText(Path.Combine(WebProjectPath(), "Program.cs"));
        var options = new MvcOptions();
        AdminModelBindingMessages.Configure(options);

        Assert.Contains("AdminModelBindingMessages.Configure", program, StringComparison.Ordinal);
        Assert.Equal(
            "O valor 'value' não é válido para o campo pacotes por caixa.",
            options.ModelBindingMessageProvider.AttemptedValueIsInvalidAccessor("value", "Product.PackagesPerCase"));
        Assert.Equal(
            "O valor 'value' não é válido.",
            options.ModelBindingMessageProvider.NonPropertyAttemptedValueIsInvalidAccessor("value"));
        Assert.Equal(
            "Informe um número válido para preço base.",
            options.ModelBindingMessageProvider.ValueMustBeANumberAccessor("BasePrice"));
    }

    private static string ReadView(string name)
    {
        return File.ReadAllText(Path.Combine(WebProjectPath(), "Areas", "Admin", "Views", "Products", name));
    }

    private static string WebProjectPath() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
}
