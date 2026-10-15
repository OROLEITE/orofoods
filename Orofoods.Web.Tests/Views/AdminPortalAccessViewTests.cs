namespace Orofoods.Web.Tests.Views;

public class AdminPortalAccessViewTests
{
    private static string ProjectPath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));

    [Fact]
    public void Access_center_exposes_admin_customer_and_seller_destinations_and_keeps_audit()
    {
        var view = File.ReadAllText(Path.Combine(ProjectPath, "Views", "AdminPortalAccess", "Index.cshtml"));

        Assert.Contains("/Admin/Dashboard", view);
        Assert.Contains("/Portal/SelectCustomer", view);
        Assert.Contains("/Admin/Portais/SelecionarVendedor", view);
        Assert.Contains("Acessos recentes", view);
        Assert.Contains("@foreach (var audit in Model)", view);
    }

    [Fact]
    public void Admin_customer_portal_and_sidebar_return_to_the_access_center()
    {
        var portalLayout = File.ReadAllText(Path.Combine(ProjectPath, "Views", "Shared", "_PortalLayout.cshtml"));
        var navigation = File.ReadAllText(Path.Combine(ProjectPath, "Views", "Shared", "_AdminNavigation.cshtml"));

        Assert.Contains("href=\"/Admin/Portais\">Central de Acesso</a>", portalLayout);
        Assert.Contains("href=\"/Admin/Portais\"", navigation);
        Assert.Contains("Central de Acesso", navigation);
    }

    [Fact]
    public void Access_center_cards_and_audit_use_compact_spacing_and_theme_tokens()
    {
        var view = File.ReadAllText(Path.Combine(ProjectPath, "Views", "AdminPortalAccess", "Index.cshtml"));
        var styles = File.ReadAllText(Path.Combine(ProjectPath, "wwwroot", "css", "admin-portal-access.css"));

        Assert.Equal(3, view.Split("class=\"card h-100 p-3\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("class=\"card p-3 mt-3\"", view);
        Assert.Contains("admin-portal-access-page .card.p-3", styles);
        Assert.Contains("color: var(--access-muted)", styles);
        Assert.Contains("html[data-theme=\"dark\"] body.admin-authenticated .admin-portal-access-page", styles);
        Assert.Contains("@media (max-width: 575.98px)", styles);
    }
}
