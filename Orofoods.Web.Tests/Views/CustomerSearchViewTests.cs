namespace Orofoods.Web.Tests.Views;

public sealed class CustomerSearchViewTests
{
    private static string ProjectPath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));

    [Fact]
    public void Wallet_and_draft_customer_selectors_share_the_authorized_search_contract()
    {
        var wallet = File.ReadAllText(Path.Combine(ProjectPath, "Areas", "Vendedor", "Views", "Customers", "Index.cshtml"));
        var editor = File.ReadAllText(Path.Combine(ProjectPath, "Areas", "Vendedor", "Views", "OrderDrafts", "Editor.cshtml"));

        Assert.Contains("data-customer-search", wallet);
        Assert.Contains("data-search-url=\"/Vendedor/Clientes/Buscar\"", wallet);
        Assert.Contains("data-search-mode=\"cards\"", wallet);
        Assert.Contains("data-customer-search-results", wallet);
        Assert.Contains("role=\"list\"", wallet);
        Assert.Contains("~/js/customer-search.js", wallet);
        Assert.Contains("~/css/customer-search.css", wallet);
        Assert.Contains("data-customer-search", editor);
        Assert.Contains("data-search-url=\"/Vendedor/Rascunhos/Clientes\"", editor);
        Assert.Contains("data-search-mode=\"links\"", editor);
        Assert.Contains("data-customer-search-status", editor);
        Assert.Contains("role=\"list\"", editor);
        Assert.Contains("~/js/customer-search.js", editor);
        Assert.Contains("~/css/customer-search.css", editor);
    }

    [Fact]
    public void Shared_customer_search_is_debounced_cancellable_and_builds_safe_keyboard_accessible_results()
    {
        var scriptPath = Path.Combine(ProjectPath, "wwwroot", "js", "customer-search.js");
        Assert.True(File.Exists(scriptPath), "The shared customer-search script is required by all three selectors.");
        var script = File.ReadAllText(scriptPath);

        Assert.Contains("300", script);
        Assert.Contains("scheduledQuery", script);
        Assert.Contains("AbortController", script);
        Assert.Contains("thisRequest !== requestNumber", script);
        Assert.Contains("textContent", script);
        Assert.Contains("aria-expanded", script);
        Assert.DoesNotContain("innerHTML", script);
        Assert.Contains("query.length < 1", script);
        Assert.Contains("aria-selected", script);
        Assert.Contains("ArrowDown", script);
        Assert.Contains("Escape", script);
        Assert.Contains("data-customer-search-submit", script);
    }
}
