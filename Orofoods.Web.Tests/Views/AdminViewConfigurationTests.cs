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
    public void Admin_sidebar_supports_persistent_collapse_and_route_aware_menu_groups()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var layout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_AdminLayout.cshtml"));
        var navigation = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_AdminNavigation.cshtml"));
        var script = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "js", "admin-navigation.js"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-navigation.css"));

        Assert.Contains("data-admin-shell", layout);
        Assert.Contains("data-admin-sidebar-toggle", layout);
        Assert.Contains("~/js/admin-navigation.js", layout);
        Assert.Contains("localStorage", script);
        Assert.Contains("admin-sidebar-collapsed", script);
        Assert.Contains("margin-left: var(--admin-sidebar-width)", styles);
        Assert.Contains("width: calc(100% - var(--admin-sidebar-width))", styles);
        Assert.Contains("margin-left: var(--admin-sidebar-collapsed-width)", styles);
        Assert.Equal(6, navigation.Split("class=\"admin-nav-group").Length - 1);
        Assert.Contains("admin-nav-link admin-nav-link--nested @Active(\"Reports\")", navigation);
        Assert.Contains("admin-nav-link admin-nav-link--nested @Active(\"Integrations\")", navigation);
    }

    [Fact]
    public void Reports_view_uses_compact_bi_components_with_existing_aggregations()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Reports", "Index.cshtml"));
        var ranking = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Reports", "_Ranking.cshtml"));

        Assert.Contains("reports-period-selector", view);
        Assert.Contains("reports-kpi-grid", view);
        Assert.Contains("reports-category-chart", view);
        Assert.Contains("Model.Report.TopProducts", view);
        Assert.Contains("Model.Report.BySalesRepresentative", view);
        Assert.Contains("Model.Report.TopCustomers", view);
        Assert.Contains("Gest&atilde;o", view);
        Assert.Contains("Model.Report.BuyerCount", view);
        Assert.Contains("Model.Report.SoldItemCount", view);
        Assert.Contains("reports-customer-ranking", view);
        Assert.Contains("report-ranking-bar", ranking);
        Assert.DoesNotContain("<table", ranking);
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
    public void Categories_index_uses_the_reusable_light_registration_table()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var layout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_AdminLayout.cshtml"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Categories", "Index.cshtml"));

        Assert.Contains("~/css/admin-registrations.css", layout);
        Assert.Contains("admin-registration-page", view);
        Assert.Contains("admin-table-card", view);
        Assert.Contains("admin-table", view);
        Assert.Contains("status-badge", view);
        Assert.Contains("admin-primary-action", view);
        Assert.Contains("admin-edit-action", view);
        Assert.Contains("admin-secondary-action", view);
        Assert.DoesNotContain("table-dark", view);
    }

    [Theory]
    [InlineData("PriceTables")]
    [InlineData("PaymentTerms")]
    public void Registration_indexes_and_forms_share_the_approved_visual_pattern(string viewFolder)
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var index = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", viewFolder, "Index.cshtml"));
        var edit = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", viewFolder, "Edit.cshtml"));

        Assert.Contains("admin-registration-page", index);
        Assert.Contains("admin-table-card", index);
        Assert.Contains("status-badge", index);
        Assert.DoesNotContain("table-dark", index);
        Assert.Contains("admin-registration-form-page", edit);
        Assert.Contains("admin-primary-action", edit);
        Assert.Contains("admin-secondary-action", edit);
    }

    [Theory]
    [InlineData("Inventory")]
    [InlineData("Products")]
    [InlineData("SalesRepresentatives")]
    [InlineData("Users")]
    public void Remaining_admin_lists_use_the_shared_light_table_pattern(string viewFolder)
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var index = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", viewFolder, "Index.cshtml"));

        Assert.Contains("admin-registration-page", index);
        Assert.Contains("admin-table-card", index);
        Assert.Contains("admin-table", index);
        Assert.DoesNotContain("table-dark", index);
    }

    [Fact]
    public void Operational_admin_tables_use_light_headers_and_non_dark_filters()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-registrations.css"));
        var viewFolders = new[] { "Customers", "Orders", "Integrations" };

        foreach (var viewFolder in viewFolders)
        {
            var index = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", viewFolder, "Index.cshtml"));
            Assert.DoesNotContain("btn-dark", index);
        }

        foreach (var viewPath in Directory.GetFiles(Path.Combine(projectPath, "Areas", "Admin", "Views"), "*.cshtml", SearchOption.AllDirectories))
        {
            Assert.DoesNotContain("btn-dark", File.ReadAllText(viewPath));
        }

        Assert.Contains(".admin-orders-table thead th", styles);
        Assert.Contains(".customer-list-table thead th", styles);
        Assert.Contains(".orders-table thead th", styles);
        Assert.Contains(".erp-table thead th", styles);
        Assert.Contains("background: #f4ead2", styles);
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
