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

    [Fact]
    public void Seller_header_and_search_actions_use_the_scoped_dark_cyan_visual_contract()
    {
        var shellStyles = File.ReadAllText(Path.Combine(ProjectRoot, "wwwroot", "css", "seller-area.css"));
        var publicHeader = File.ReadAllText(Path.Combine(ProjectRoot, "Views", "Shared", "_SiteHeader.cshtml"));

        Assert.Contains("body.seller-app-page .site-header-modern .navbar", shellStyles);
        Assert.Contains("#111827", shellStyles);
        Assert.Contains("body.seller-app-page .site-header-modern .main-nav .nav-link", shellStyles);
        Assert.Contains("#CBD5E1", shellStyles);
        Assert.Contains("body.seller-app-page .customer-search__form-row .btn", shellStyles);
        Assert.Contains("body.seller-app-page .seller-order-drafts__search-row .seller-order-drafts__primary", shellStyles);
        Assert.Contains("#0E7490", shellStyles);
        Assert.Contains("#0B5D75", shellStyles);
        Assert.Contains("body.seller-app-page .btn-gold:focus-visible", shellStyles);
        Assert.Contains("body.seller-app-page .btn-gold:disabled", shellStyles);
        Assert.Contains("body.seller-app-page .seller-app-main .section .text-muted", shellStyles);
        Assert.Contains("navbar-light bg-white", publicHeader);
    }

    [Fact]
    public void Seller_dashboard_keeps_order_and_draft_actions_and_leaves_wallet_in_sidebar()
    {
        var dashboard = File.ReadAllText(Path.Combine(ProjectRoot, "Areas", "Vendedor", "Views", "Dashboard", "Index.cshtml"));
        var sellerLayout = File.ReadAllText(Path.Combine(ProjectRoot, "Areas", "Vendedor", "Views", "_SellerLayout.cshtml"));

        Assert.Contains("Digitar pedido", dashboard);
        Assert.Contains("Meus rascunhos", dashboard);
        Assert.DoesNotContain("seller-button--gold", dashboard);
        Assert.DoesNotContain("Ver carteira", dashboard);
        Assert.Contains("asp-controller=\"Customers\" asp-action=\"Index\"", sellerLayout);
    }

    [Fact]
    public void Access_center_uses_portal_action_labels_without_icons()
    {
        var accessView = File.ReadAllText(Path.Combine(ProjectRoot, "Views", "AdminPortalAccess", "Index.cshtml"));
        var accessStyles = File.ReadAllText(Path.Combine(ProjectRoot, "wwwroot", "css", "admin-portal-access.css"));

        Assert.Equal(2, accessView.Split("Abrir portal").Length - 1);
        Assert.Contains(">Abrir painel</a>", accessView);
        Assert.DoesNotContain("Selecionar cliente</a>", accessView);
        Assert.DoesNotContain("Selecionar vendedor</a>", accessView);
        Assert.DoesNotContain("Abrir portal <i", accessView);
        Assert.Contains("btn-portal-action", accessView);
        Assert.Contains("#0E7490", accessStyles);
        Assert.Contains("#0B5D75", accessStyles);
        Assert.Contains("min-height: 44px", accessStyles);
        Assert.Contains(".btn-portal-action:hover", accessStyles);
        Assert.Contains(".btn-portal-action:focus-visible", accessStyles);
        Assert.Contains(".btn-portal-action:disabled", accessStyles);
    }
}
