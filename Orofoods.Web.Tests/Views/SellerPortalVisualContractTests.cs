namespace Orofoods.Web.Tests.Views;

public sealed class SellerPortalVisualContractTests
{
    private static string ProjectRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));

    private static string CompactCss(string css) => System.Text.RegularExpressions.Regex.Replace(css, @"\s+", string.Empty);

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
    public void Seller_portal_density_and_order_editor_keep_the_summary_after_the_form_on_narrow_screens()
    {
        var shellStyles = File.ReadAllText(Path.Combine(ProjectRoot, "wwwroot", "css", "seller-area.css"));
        var draftStyles = File.ReadAllText(Path.Combine(ProjectRoot, "wwwroot", "css", "seller-order-drafts.css"));
        var editor = File.ReadAllText(Path.Combine(ProjectRoot, "Areas", "Vendedor", "Views", "OrderDrafts", "Editor.cshtml"));

        Assert.Contains("padding-block: .8rem 1.4rem", shellStyles);
        Assert.Contains("--seller-sidebar-width: 220px", shellStyles);
        Assert.Contains("@media (min-width: 992px)", draftStyles);
        Assert.Contains(".seller-order-drafts__summary { position:static; order:1; }", draftStyles);
        Assert.Contains("min-height:40px", draftStyles);
        Assert.Contains("grid-template-columns:1fr 1fr", draftStyles);
        Assert.Contains("rows=\"2\"", editor);
    }

    [Fact]
    public void Seller_order_editor_keeps_neutral_cancel_contrasting_disabled_send_and_both_themes()
    {
        var draftStyles = File.ReadAllText(Path.Combine(ProjectRoot, "wwwroot", "css", "seller-order-drafts.css"));

        Assert.Contains(".seller-order-drafts__outline { background:var(--draft-card); border:1px solid var(--draft-border); color:var(--draft-muted);", draftStyles);
        Assert.Contains(".seller-order-drafts__send:disabled { background:#64748B; border-color:#64748B; color:#FFFFFF; opacity:1;", draftStyles);
        Assert.Contains("html[data-theme=\"dark\"] .seller-order-drafts", draftStyles);
        Assert.Contains("html[data-theme=\"light\"] .seller-order-drafts", draftStyles);
    }

    [Fact]
    public void Access_center_uses_cyan_portal_actions_and_compacts_the_seller_context_form()
    {
        var accessCenter = File.ReadAllText(Path.Combine(ProjectRoot, "Views", "AdminPortalAccess", "Index.cshtml"));
        var sellerContext = File.ReadAllText(Path.Combine(ProjectRoot, "Views", "AdminPortalAccess", "SelectSeller.cshtml"));
        var accessStylesFile = File.ReadAllText(Path.Combine(ProjectRoot, "wwwroot", "css", "admin-portal-access.css"));
        var accessStyles = CompactCss(accessStylesFile);

        Assert.Equal(2, accessCenter.Split("Abrir portal</a>", StringSplitOptions.None).Length - 1);
        Assert.Contains("Abrir painel", accessCenter);
        Assert.Equal(3, accessCenter.Split("class=\"btn btn-portal-action mt-auto align-self-start\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("Abrir portal</button>", sellerContext);
        Assert.DoesNotContain("fa-arrow-right-to-bracket", sellerContext);
        Assert.Contains(".admin-authenticated .admin-portal-access-page .btn-portal-action", accessStylesFile);
        Assert.Contains("background:#0E7490;border:1pxsolid#0E7490;color:#fff;", accessStyles);
        Assert.Contains(".admin-portal-access-page .card.admin-portal-access__seller-form", accessStylesFile);
        Assert.Contains("padding:.85rem1rem!important;", accessStyles);
    }

    [Fact]
    public void Seller_order_editor_compacts_layout_and_keeps_responsive_actions_after_the_summary()
    {
        var editor = File.ReadAllText(Path.Combine(ProjectRoot, "Areas", "Vendedor", "Views", "OrderDrafts", "Editor.cshtml"));
        var draftStylesFile = File.ReadAllText(Path.Combine(ProjectRoot, "wwwroot", "css", "seller-order-drafts.css"));
        var draftStyles = CompactCss(draftStylesFile);

        Assert.True(editor.IndexOf("seller-order-drafts__summary", StringComparison.Ordinal)
            < editor.IndexOf("data-sticky-actions", StringComparison.Ordinal));
        Assert.Contains("--draft-action:#0E7490;", draftStyles);
        Assert.Contains(".seller-order-drafts__primary,.seller-order-drafts__save,.seller-order-drafts__send{background:var(--draft-action);", draftStyles);
        Assert.Contains(".seller-order-drafts__product-search-row .form-control", draftStylesFile);
        Assert.Contains("min-height:40px;", draftStyles);
        Assert.Contains(".seller-order-drafts__actions{display:flex;justify-content:flex-end;", draftStyles);
        Assert.Contains("seller-order-drafts__cancel", editor);
        Assert.Contains(".seller-order-drafts__cancel", draftStylesFile);
        Assert.Contains("@media(max-width:767.98px)", draftStyles);
        Assert.Contains("grid-template-columns:1fr1fr;", draftStyles);
        Assert.Contains("@media(max-width:359.98px)", draftStyles);
        Assert.Contains("min-height:44px;", draftStyles);
        Assert.Contains("grid-template-columns:repeat(2,minmax(0,1fr));", draftStyles);
    }

    [Fact]
    public void Seller_product_search_results_are_compact_responsive_and_keep_prices_legible_in_dark_theme()
    {
        var script = File.ReadAllText(Path.Combine(ProjectRoot, "wwwroot", "js", "seller-order-drafts.js"));
        var draftStylesFile = File.ReadAllText(Path.Combine(ProjectRoot, "wwwroot", "css", "seller-order-drafts.css"));
        var draftStyles = CompactCss(draftStylesFile);
        var editor = File.ReadAllText(Path.Combine(ProjectRoot, "Areas", "Vendedor", "Views", "OrderDrafts", "Editor.cshtml"));
        var addProductStart = script.IndexOf("function addProduct(product)", StringComparison.Ordinal);
        var removeProductStart = script.IndexOf("function removeProduct(productId)", StringComparison.Ordinal);
        var addProduct = script[addProductStart..removeProductStart];

        Assert.Contains("seller-order-drafts__product-identity", script);
        Assert.Contains("seller-order-drafts__product-details", script);
        Assert.Contains("btn seller-order-drafts__primary", script);
        Assert.DoesNotContain("searchInput.value", addProduct);
        Assert.Contains("WMC ou EAN", editor);
        Assert.Contains("html[data-theme=\"dark\"] .seller-order-drafts__product-price", draftStylesFile);
        Assert.Contains("html[data-theme=\"dark\"].seller-order-drafts__product-price,html[data-theme=\"dark\"].seller-order-drafts__unit-price{color:#FFFFFF;}", draftStyles);
        Assert.Contains("grid-template-columns:minmax(0,2fr)minmax(115px,.8fr)autoauto;", draftStyles);
        Assert.Contains("grid-template-columns:minmax(0,1fr)auto;", draftStyles);
        Assert.Contains(".seller-order-drafts__product-card .btn { grid-column:1/-1; justify-self:stretch; min-height:44px; }", draftStylesFile);
    }
}
