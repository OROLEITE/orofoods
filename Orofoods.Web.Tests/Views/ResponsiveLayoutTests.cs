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

    [Fact]
    public void Collapsed_global_navigation_stays_inside_an_opaque_auto_height_header()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "site.css"));

        Assert.Contains("@media(max-width:991.98px){.site-header{height:auto}", styles);
        Assert.Contains(".site-header .navbar-collapse{background:#fff", styles);
        Assert.Contains(".site-header .navbar-nav .btn{display:flex;width:100%", styles);
    }
}
