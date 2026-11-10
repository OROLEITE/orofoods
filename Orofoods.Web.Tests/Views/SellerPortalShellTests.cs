namespace Orofoods.Web.Tests.Views;

public class SellerPortalShellTests
{
    private static string ProjectRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));

    [Fact]
    public void Seller_area_uses_its_own_shell_layout()
    {
        var viewStart = File.ReadAllText(Path.Combine(ProjectRoot, "Areas", "Vendedor", "Views", "_ViewStart.cshtml"));

        Assert.Contains("_SellerLayout.cshtml", viewStart);
    }

    [Fact]
    public void Seller_shell_exposes_an_accessible_mobile_offcanvas_and_skip_link()
    {
        var layoutPath = Path.Combine(ProjectRoot, "Areas", "Vendedor", "Views", "_SellerLayout.cshtml");

        Assert.True(File.Exists(layoutPath), "The seller area needs its own shell layout.");
        var layout = File.ReadAllText(layoutPath);

        Assert.Contains("offcanvas-md", layout);
        Assert.Contains("aria-controls=\"sellerSidebar\"", layout);
        Assert.Contains("data-bs-target=\"#sellerSidebar\"", layout);
        Assert.Contains("href=\"#sellerMain\"", layout);
        Assert.Contains("aria-label=\"Navegação do vendedor\"", layout);
    }

    [Fact]
    public void Seller_sidebar_close_button_targets_the_offcanvas()
    {
        var layout = File.ReadAllText(Path.Combine(ProjectRoot, "Areas", "Vendedor", "Views", "_SellerLayout.cshtml"));

        Assert.Contains("data-bs-dismiss=\"offcanvas\" data-bs-target=\"#sellerSidebar\"", layout);
    }

    [Fact]
    public void Seller_shell_styles_support_mobile_tablet_desktop_and_dark_theme_focus()
    {
        var styles = File.ReadAllText(Path.Combine(ProjectRoot, "wwwroot", "css", "seller-area.css"));

        Assert.Contains("@media (max-width: 767.98px)", styles);
        Assert.Contains("@media (min-width: 768px)", styles);
        Assert.Contains("html[data-theme=\"dark\"] body.seller-app-page", styles);
        Assert.Contains("#38BDF8", styles);
        Assert.Contains(":focus-visible", styles);
        Assert.Contains("min-height: 44px", styles);
    }

    [Fact]
    public void Seller_dashboard_uses_compact_commercial_sections_without_changing_its_data_contract()
    {
        var dashboard = File.ReadAllText(Path.Combine(ProjectRoot, "Areas", "Vendedor", "Views", "Dashboard", "Index.cshtml"));

        Assert.Contains("class=\"seller-dashboard\"", dashboard);
        Assert.Contains("Model.ActiveCustomerCount", dashboard);
        Assert.Contains("Acesso administrativo · @Model.SellerName", dashboard);
        Assert.Contains("asp-controller=\"Customers\"", dashboard);
        Assert.Contains("asp-controller=\"OrderDrafts\"", dashboard);
    }

    [Fact]
    public void Admin_seller_shell_displays_the_selected_seller_and_safe_context_destinations()
    {
        var layout = File.ReadAllText(Path.Combine(ProjectRoot, "Areas", "Vendedor", "Views", "_SellerLayout.cshtml"));

        Assert.Contains("SalesRepresentativeAccessService", layout);
        Assert.Contains("GetActiveRepresentativeAsync(User)", layout);
        Assert.Contains("selectedRepresentative?.Name", layout);
        Assert.Contains("Painel Administrativo", layout);
        Assert.Contains("Central de Acesso", layout);
        Assert.Contains("Trocar vendedor", layout);
        Assert.Contains("Portal do Cliente", layout);
        Assert.Contains("/Portal/SelectCustomer", layout);
        Assert.Contains("seller-app-admin-nav", layout);
        Assert.Contains("aria-label=\"Acesso administrativo\"", layout);
        Assert.Contains("fa-table-cells-large\" aria-hidden=\"true\"></i><span>Central de Acesso", layout);
    }

    [Fact]
    public void Seller_sidebar_uses_compact_navigation_dimensions()
    {
        var styles = File.ReadAllText(Path.Combine(ProjectRoot, "wwwroot", "css", "seller-area.css"));

        Assert.Contains("--seller-sidebar-width: 220px", styles);
        Assert.Contains(".seller-app-nav a {", styles);
        Assert.Contains("min-height: 40px", styles);
        Assert.Contains(".seller-app-page .seller-app-nav a { min-height: 44px; }", styles);
    }

    [Fact]
    public void Seller_desktop_content_width_reserves_the_sidebar()
    {
        var styles = File.ReadAllText(Path.Combine(ProjectRoot, "wwwroot", "css", "seller-area.css"));
        var desktopContentRule = System.Text.RegularExpressions.Regex.Match(
            styles,
            @"(?s)@media \(min-width: 768px\).*?\.seller-app-content\s*\{(?<declarations>[^}]+)\}");
        var scopedContentRule = System.Text.RegularExpressions.Regex.Match(
            styles,
            @"body\.seller-app-page \.seller-app-content\s*\{(?<declarations>[^}]+)\}");

        Assert.True(desktopContentRule.Success);
        Assert.Contains("width: calc(100% - var(--seller-sidebar-width));", desktopContentRule.Groups["declarations"].Value);
        Assert.True(scopedContentRule.Success);
        Assert.DoesNotMatch(@"(?m)^\s*width\s*:", scopedContentRule.Groups["declarations"].Value);
    }

    [Fact]
    public void Seller_editor_reserves_space_for_fixed_mobile_actions()
    {
        var view = File.ReadAllText(Path.Combine(ProjectRoot, "Areas", "Vendedor", "Views", "OrderDrafts", "Editor.cshtml"));
        var styles = File.ReadAllText(Path.Combine(ProjectRoot, "wwwroot", "css", "seller-order-drafts.css"));

        Assert.Contains("seller-order-drafts--editor", view);
        Assert.Contains("body.seller-app-page .seller-order-drafts--editor { padding-bottom:calc(170px + env(safe-area-inset-bottom)); }", styles);
    }
}
