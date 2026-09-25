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
    public void Home_order_and_portal_ctas_keep_role_aware_destinations_and_login_return_urls()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var home = File.ReadAllText(Path.Combine(projectPath, "Views", "Home", "Index.cshtml"));

        Assert.Contains("""User.Identity?.IsAuthenticated == true""", home);
        Assert.Contains("""Url.Action("Catalog", "Portal", new { area = "" })""", home);
        Assert.Contains("""Url.Page("/Account/Login", values: new { area = "Identity", returnUrl = customerCatalogUrl })""", home);
        Assert.Contains("""?? "/Identity/Account/Login?returnUrl=%2FPortal%2FCatalog""", home);
        Assert.Contains("""Url.Page("/Account/Login", values: new { area = "Identity", returnUrl = customerDashboardUrl })""", home);
        Assert.Contains("""Url.Action("Index", "Dashboard", new { area = "Admin" })""", home);
        Assert.Contains("""Url.Action("Index", "Dashboard", new { area = "Vendedor" })""", home);
        Assert.Contains("""Url.Action("Index", "Commercial", new { area = "Admin" })""", home);
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
        Assert.Contains("data-portal-sidebar-toggle", layout);
        Assert.Contains("portal-navigation.css", layout);
        Assert.DoesNotContain(">01<", layout);
    }

    [Fact]
    public void Portal_shell_keeps_its_single_semantic_main_inside_the_shell()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var layout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_Layout.cshtml"));
        var portalLayout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_PortalLayout.cshtml"));

        Assert.Contains("ViewData[\"LayoutBodyHasMain\"] = true", portalLayout);
        Assert.Contains("ViewData[\"LayoutBodyHasMain\"] is true", layout);
        Assert.Contains("if (layoutBodyHasMain)", layout);
        Assert.Contains("<main class=\"portal-app-main\">", portalLayout);
        Assert.Contains("<main class=\"site-main\">@RenderBody()</main>", layout);
    }

    [Fact]
    public void Portal_sidebar_uses_one_width_for_the_sidebar_and_main_content()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var navigation = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "portal-navigation.css"));
        var density = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "portal-density.css"));
        var shell = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "portal-shell.css"));
        var catalog = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "catalog.css"));

        Assert.Contains("--portal-sidebar-width: 248px", navigation);
        Assert.Contains("--portal-sidebar-collapsed-width: 76px", navigation);
        Assert.Contains("grid-template-columns: var(--portal-sidebar-width) minmax(0, 1fr)", navigation);
        Assert.Contains("inline-size: 0", navigation);
        Assert.Contains("opacity: 0", navigation);
        Assert.DoesNotContain("--portal-sidebar-width", density);
        Assert.DoesNotContain("245px", density);
        Assert.DoesNotContain("264px", navigation);
        Assert.DoesNotContain("80px", navigation);
        Assert.DoesNotContain("--portal-sidebar-width", catalog);
        Assert.DoesNotContain("grid-template-columns:var(--portal-sidebar-width)", density);
        Assert.DoesNotContain(".portal-app-sidebar{", shell);
    }
}
