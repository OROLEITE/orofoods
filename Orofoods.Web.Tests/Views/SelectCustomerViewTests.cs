namespace Orofoods.Web.Tests.Views;

public class SelectCustomerViewTests
{
    [Fact]
    public void Customer_selector_uses_bootstrap_form_controls()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Portal", "SelectCustomer.cshtml"));

        Assert.Contains("form-select", markup);
        Assert.Contains("form-label", markup);
        Assert.Contains("form-text", markup);
    }

    [Fact]
    public void Customer_selector_marks_the_field_and_action_for_baseline_alignment()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Portal", "SelectCustomer.cshtml"));

        Assert.Contains("customer-selector-field", markup);
        Assert.Contains("customer-selector-action", markup);
    }

    [Fact]
    public void Customer_selector_offsets_the_action_to_the_select_top_edge()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "site.css"));

        Assert.Contains(".customer-selector-action{padding-top:37px}", styles);
    }
}
