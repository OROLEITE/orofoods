namespace Orofoods.Web.Tests.Views;

public class SelectCustomerViewTests
{
    [Fact]
    public void Customer_selector_uses_bootstrap_form_controls()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Portal", "SelectCustomer.cshtml"));

        Assert.Contains("form-control", markup);
        Assert.Contains("form-label", markup);
        Assert.Contains("role=\"status\"", markup);
    }

    [Fact]
    public void Customer_selector_uses_authorized_suggestions_without_rendering_the_customer_directory()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Portal", "SelectCustomer.cshtml"));

        Assert.Contains("data-customer-search", markup);
        Assert.Contains("data-search-url=\"/Admin/Portais/Clientes\"", markup);
        Assert.Contains("data-search-mode=\"select\"", markup);
        Assert.Contains("data-customer-search-selected-id", markup);
        Assert.Contains("data-customer-search-submit disabled", markup);
        Assert.DoesNotContain("@foreach (var customer in Model)", markup);
        Assert.Contains("~/js/customer-search.js", markup);
        Assert.Contains("~/css/customer-search.css", markup);
    }

    [Fact]
    public void Customer_selector_marks_the_field_and_action_for_baseline_alignment()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Portal", "SelectCustomer.cshtml"));

        Assert.Contains("data-customer-search-input", markup);
        Assert.Contains("customer-search__submit-row", markup);
    }

    [Fact]
    public void Customer_selector_offsets_the_action_to_the_select_top_edge()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "customer-search.css"));

        Assert.Contains(".customer-search__submit-row", styles);
    }
}
