namespace Orofoods.Web.Tests.Views;

public class ResponsiveLayoutTests
{
    [Fact]
    public void Cart_uses_a_styled_compact_update_action()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Portal", "Cart.cshtml"));

        Assert.Contains("class=\"btn btn-dark qty-update\"", markup);
    }

    [Fact]
    public void Global_styles_prevent_horizontal_page_overflow()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "site.css"));

        Assert.Contains("overflow-x:hidden", styles);
        Assert.Contains(".order-layout{grid-template-columns:minmax(0,1fr)", styles);
    }
}
