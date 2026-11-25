namespace Orofoods.Web.Tests.Views;

public class ResponsiveLayoutTests
{
    [Fact]
    public void Cart_uses_a_styled_compact_update_action()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Portal", "Cart.cshtml"));
        var customerStyles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "customer-experience.css"));
        var actionStyles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "portal-actions.css"));

        Assert.Contains("class=\"btn btn-gold qty-update\"", markup);
        Assert.Contains(".customer-experience .qty-control :is(button, input)", customerStyles);
        Assert.Contains("min-height: 40px", customerStyles);
        Assert.Contains(".portal-authenticated .btn-gold:hover", actionStyles);
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

    [Fact]
    public void Admin_navigation_preference_has_a_mobile_offcanvas_and_desktop_style_contract()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var layout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_AdminLayout.cshtml"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-navigation.css"));

        Assert.Contains("class=\"admin-sidebar d-none d-xl-flex\"", layout);
        Assert.Contains("class=\"offcanvas offcanvas-start admin-offcanvas\"", layout);
        Assert.Contains(".admin-navigation-preference__select", styles);
        Assert.Contains(".admin-sidebar-collapsed .admin-sidebar .admin-navigation-preference", styles);
    }
}
