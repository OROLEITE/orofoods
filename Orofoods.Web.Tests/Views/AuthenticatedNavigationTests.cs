namespace Orofoods.Web.Tests.Views;

public class AuthenticatedNavigationTests
{
    [Fact]
    public void Shared_layout_switches_public_actions_for_authenticated_users()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var layout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_Layout.cshtml"));

        Assert.Contains("User.Identity?.IsAuthenticated", layout);
        Assert.Contains("User.IsInRole(\"Administrador\")", layout);
        Assert.Contains("asp-area=\"Admin\" asp-controller=\"Dashboard\" asp-action=\"Index\"", layout);
        Assert.Contains("Sair", layout);
        Assert.Contains("/Account/Logout", layout);
    }

    [Fact]
    public void Shared_layout_includes_an_accessible_authenticated_account_menu()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var layout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_Layout.cshtml"));

        Assert.Contains("auth-user-menu", layout);
        Assert.Contains("data-bs-toggle=\"dropdown\"", layout);
        Assert.Contains("aria-haspopup=\"menu\"", layout);
        Assert.Contains("PortalCustomerName", layout);
        Assert.Contains("asp-page=\"/Account/Logout\"", layout);
    }

    [Fact]
    public void Site_header_uses_a_bootstrap_account_dropdown_with_a_safe_name_fallback()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var header = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_SiteHeader.cshtml"));

        Assert.Contains("navbar-expand-lg", header);
        Assert.Contains("container-fluid site-header-container", header);
        Assert.Contains("dropdown account-dropdown", header);
        Assert.Contains("Minha conta", header);
        Assert.Contains("asp-page=\"/Account/Logout\"", header);
    }

    [Fact]
    public void Site_header_routes_the_account_panel_by_role_and_keeps_customer_context_separate()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var header = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_SiteHeader.cshtml"));

        Assert.Contains("Url.Action(\"Index\", \"Dashboard\", new { area = \"Admin\" })", header);
        Assert.Contains("Url.Action(\"Index\", \"Commercial\", new { area = \"Admin\" })", header);
        Assert.Contains("Url.Action(\"Dashboard\", \"Portal\", new { area = \"\" })", header);
        Assert.Contains("if (isAdministrator)", header);
        Assert.Contains("asp-action=\"SelectCustomer\"", header);
        Assert.Contains("Acessar portal de cliente", header);
    }

    [Fact]
    public void Portal_layout_uses_font_awesome_icons_for_the_shared_navigation()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var layout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_PortalLayout.cshtml"));

        Assert.Contains("portal-nav-icon", layout);
        Assert.Contains("fa-house", layout);
        Assert.Contains("fa-box-open", layout);
        Assert.Contains("fa-receipt", layout);
        Assert.Contains("fa-heart", layout);
        Assert.Contains("fa-cart-shopping", layout);
        Assert.DoesNotContain(">01<", layout);
    }
}
