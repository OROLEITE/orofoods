namespace Orofoods.Web.Tests.Views;

public class AdminViewConfigurationTests
{
    [Fact]
    public void Admin_views_use_admin_layout_and_mvc_tag_helpers()
    {
        var viewsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web/Areas/Admin/Views"));
        var viewStart = File.ReadAllText(Path.Combine(viewsPath, "_ViewStart.cshtml"));
        var viewImports = File.ReadAllText(Path.Combine(viewsPath, "_ViewImports.cshtml"));

        Assert.Contains("/Views/Shared/_AdminLayout.cshtml", viewStart);
        Assert.Contains("@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers", viewImports);
    }

    [Fact]
    public void Admin_layout_uses_shared_navigation_with_role_aware_groups()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var layout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_AdminLayout.cshtml"));
        var navigation = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_AdminNavigation.cshtml"));

        Assert.Contains("admin-sidebar", layout);
        Assert.Contains("offcanvas offcanvas-start", layout);
        Assert.Contains("User.IsInRole(\"Administrador\")", navigation);
        Assert.Contains("User.IsInRole(\"Vendedor\")", navigation);
        Assert.Contains("data-bs-toggle=\"collapse\"", navigation);
        Assert.Contains("asp-controller=\"Integrations\"", navigation);
        Assert.Contains("asp-controller=\"Commercial\"", navigation);
    }

    [Fact]
    public void Customers_view_uses_the_compact_admin_filter_controls()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Customers", "Index.cshtml"));

        Assert.Contains("customer-list-tools", view);
        Assert.Contains("customerQuery", view);
        Assert.Contains("customerStatus", view);
        Assert.Contains("Empresa, CNPJ ou e-mail", view);
    }

    [Fact]
    public void Customer_actions_define_readable_secondary_button_states()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-customer-actions.css"));

        Assert.Contains("btn.btn-outline-dark:hover", styles);
        Assert.Contains("--bs-btn-hover-color:#fff", styles);
        Assert.Contains("btn.btn-outline-dark:focus", styles);
        Assert.Contains("btn:disabled", styles);
        Assert.Contains("min-height:44px", styles);
    }
}
