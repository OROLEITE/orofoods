namespace Orofoods.Web.Tests.Views;

public sealed class SellerPortalVisualContractTests
{
    private static string ProjectRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));

    [Fact]
    public void Admin_portal_routes_share_the_admin_theme_and_local_access_styles()
    {
        var layout = File.ReadAllText(Path.Combine(ProjectRoot, "Views", "Shared", "_Layout.cshtml"));
        var adminLayout = File.ReadAllText(Path.Combine(ProjectRoot, "Views", "Shared", "_AdminLayout.cshtml"));
        var accessSelector = File.ReadAllText(Path.Combine(ProjectRoot, "Views", "AdminPortalAccess", "SelectSeller.cshtml"));
        var accessStyles = File.ReadAllText(Path.Combine(ProjectRoot, "wwwroot", "css", "admin-portal-access.css"));

        Assert.Contains("isAdminExperience", layout);
        Assert.Contains("admin-authenticated", layout);
        Assert.Contains("~/css/admin-portal-access.css", adminLayout);
        Assert.Contains("href=\"/Admin/Portais\"", accessSelector);
        Assert.Contains("Central de Acesso", accessSelector);
        Assert.Contains("html[data-theme=\"dark\"]", accessStyles);
        Assert.Contains("html[data-theme=\"light\"]", accessStyles);
        Assert.Contains("@media (max-width: 575.98px)", accessStyles);
    }

    [Fact]
    public void Seller_portal_contracts_cover_the_active_seller_search_pages_and_both_themes()
    {
        var shellStyles = File.ReadAllText(Path.Combine(ProjectRoot, "wwwroot", "css", "seller-area.css"));
        var draftStyles = File.ReadAllText(Path.Combine(ProjectRoot, "wwwroot", "css", "seller-order-drafts.css"));
        var searchStyles = File.ReadAllText(Path.Combine(ProjectRoot, "wwwroot", "css", "customer-search.css"));
        var wallet = File.ReadAllText(Path.Combine(ProjectRoot, "Areas", "Vendedor", "Views", "Customers", "Index.cshtml"));
        var dashboard = File.ReadAllText(Path.Combine(ProjectRoot, "Areas", "Vendedor", "Views", "Dashboard", "Index.cshtml"));
        var drafts = File.ReadAllText(Path.Combine(ProjectRoot, "Areas", "Vendedor", "Views", "OrderDrafts", "Index.cshtml"));

        Assert.Contains("seller-app-admin-context", shellStyles);
        Assert.Contains("seller-dashboard__empty", shellStyles);
        Assert.Contains("@media (prefers-color-scheme: dark)", shellStyles);
        Assert.Contains("@media (prefers-color-scheme: dark)", draftStyles);
        Assert.Contains("Model.SellerName", dashboard);
        Assert.Contains("data-customer-search", wallet);
        Assert.Contains("customer-search__status", wallet);
        Assert.Contains("seller-order-drafts__empty", drafts);
        Assert.Contains("@media (max-width: 767.98px)", draftStyles);
        Assert.Contains("min-height: calc(100dvh - var(--seller-header-height, 64px))", draftStyles);
        Assert.Contains("@media (max-width: 575.98px)", searchStyles);
        Assert.Contains("@media (min-width: 768px)", shellStyles);
    }
}
