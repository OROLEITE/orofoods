using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Areas.Vendedor.Controllers;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Tests.Controllers;

public sealed class SellerPortalCompleteRegressionTests
{
    private static string ProjectRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));

    private static string WebFile(params string[] segments) => Path.Combine(ProjectRoot, Path.Combine(segments));

    private static IReadOnlyList<string> MediaBlocks(string css, string condition)
    {
        var blocks = new List<string>();
        var searchAt = 0;
        while ((searchAt = css.IndexOf(condition, searchAt, StringComparison.Ordinal)) >= 0)
        {
            var openingBrace = css.IndexOf('{', searchAt);
            if (openingBrace < 0) break;
            var depth = 1;
            var end = openingBrace + 1;
            while (end < css.Length && depth > 0)
            {
                if (css[end] == '{') depth++;
                else if (css[end] == '}') depth--;
                end++;
            }
            if (depth == 0) blocks.Add(css[searchAt..end]);
            searchAt = end;
        }
        return blocks;
    }

    private static double ContrastRatio(string foreground, string background)
    {
        static double Luminance(string color)
        {
            var channels = Enumerable.Range(0, 3)
                .Select(index => Convert.ToInt32(color.Substring(1 + index * 2, 2), 16) / 255d)
                .Select(channel => channel <= .04045 ? channel / 12.92 : Math.Pow((channel + .055) / 1.055, 2.4))
                .ToArray();
            return .2126 * channels[0] + .7152 * channels[1] + .0722 * channels[2];
        }

        var first = Luminance(foreground);
        var second = Luminance(background);
        return (Math.Max(first, second) + .05) / (Math.Min(first, second) + .05);
    }

    [Fact]
    public async Task Legacy_seller_routes_redirect_into_order_draft_entry_without_using_cart_or_checkout_services()
    {
        const int customerId = 42;
        var catalog = new CatalogController();
        var cart = new CartController();
        var checkout = new CheckoutController();

        var results = new IActionResult[]
        {
            await catalog.Index(customerId, null, CancellationToken.None),
            await cart.Index(customerId, CancellationToken.None),
            await cart.Add(customerId, 10, 2, CancellationToken.None),
            await cart.Update(customerId, 10, 3, CancellationToken.None),
            await cart.Remove(customerId, 10, CancellationToken.None),
            await checkout.Index(customerId, CancellationToken.None),
            await checkout.Index(customerId, new SellerCheckoutViewModel { CustomerId = customerId }, CancellationToken.None)
        };

        Assert.All(results, result =>
        {
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("New", redirect.ActionName);
            Assert.Equal("OrderDrafts", redirect.ControllerName);
            Assert.Equal(customerId, redirect.RouteValues!["customerId"]);
        });
    }

    [Fact]
    public void Seller_customer_workspace_keeps_only_order_entry_drafts_and_history_actions()
    {
        var workspace = File.ReadAllText(WebFile("Areas", "Vendedor", "Views", "Customers", "Details.cshtml"));
        var sidebar = File.ReadAllText(WebFile("Areas", "Vendedor", "Views", "_SellerLayout.cshtml"));
        var dashboard = File.ReadAllText(WebFile("Areas", "Vendedor", "Views", "Dashboard", "Index.cshtml"));

        Assert.Contains("Model.SalesRepresentativeName", workspace);
        Assert.Contains("Model.Customer.Status", workspace);
        Assert.Contains("Model.PaymentTerms", workspace);
        Assert.Contains("Digitar pedido", workspace);
        Assert.Contains("Meus rascunhos", workspace);
        Assert.Contains("Pedidos", workspace);
        Assert.DoesNotContain("Ver catÃ¡logo", workspace);
        Assert.DoesNotContain("Carrinho", workspace);
        Assert.DoesNotContain("Checkout", workspace);
        Assert.DoesNotContain("Ver catÃ¡logo", sidebar + dashboard);
        Assert.DoesNotContain("Carrinho", sidebar + dashboard);
        Assert.DoesNotContain("Checkout", sidebar + dashboard);
        var headerStyles = File.ReadAllText(WebFile("wwwroot", "css", "seller-area.css"));
        var compactHeaderStyles = System.Text.RegularExpressions.Regex.Replace(headerStyles, @"\s+", string.Empty);
        var header = File.ReadAllText(WebFile("Views", "Shared", "_SiteHeader.cshtml"));
        var cartComponent = File.ReadAllText(WebFile("ViewComponents", "CatalogCartViewComponent.cs"));
        Assert.Contains("body.seller-app-page .site-header-modern .main-nav", headerStyles);
        Assert.Contains("navbar-toggler", header);
        Assert.DoesNotContain("body.seller-app-page.site-header-modern.navbar-toggler{display:none;}", compactHeaderStyles);
        Assert.Contains("User.IsInRole(\"Vendedor\")", cartComponent);
    }

    [Fact]
    public void Seller_orders_have_dark_table_styles_and_a_mobile_card_presentation()
    {
        var view = File.ReadAllText(WebFile("Areas", "Vendedor", "Views", "Orders", "Index.cshtml"));
        var styles = File.ReadAllText(WebFile("wwwroot", "css", "seller-area.css"));

        Assert.Contains("seller-orders-mobile", view);
        Assert.Contains("Ver pedido", view);
        Assert.DoesNotContain("asp-controller=\"Catalog\"", view);
        Assert.Contains("body.seller-app-page .seller-orders-table", styles);
        Assert.Contains("body.seller-app-page .seller-orders-mobile", styles);
        Assert.Contains("#111827", styles);
        Assert.Contains("#374151", styles);
        Assert.Contains("seller-orders-table", styles);
    }

    [Fact]
    public void Seller_palette_and_mobile_shell_remove_sidebar_space_at_all_requested_phone_widths()
    {
        var styles = System.Text.RegularExpressions.Regex.Replace(
            File.ReadAllText(WebFile("wwwroot", "css", "seller-area.css")), @"\s+", string.Empty);
        var draftStyles = System.Text.RegularExpressions.Regex.Replace(
            File.ReadAllText(WebFile("wwwroot", "css", "seller-order-drafts.css")), @"\s+", string.Empty);

        Assert.Contains("--seller-canvas:#0B132B;", styles);
        Assert.Contains("--seller-surface:#111827;", styles);
        Assert.Contains("--seller-border:#374151;", styles);
        Assert.Contains("--seller-text:#E5E7EB;", styles);
        Assert.Contains("--seller-muted:#94A3B8;", styles);
        Assert.Contains("--seller-icon:#38BDF8;", styles);
        Assert.Contains("@media(max-width:767.98px){body.seller-app-page.seller-app-shell{display:block;", styles);
        Assert.Contains("body.seller-app-page.seller-app-content,body.seller-app-page.seller-app-main{width:100%;min-width:0;margin-inline:0;}", styles);
        Assert.Contains(".seller-dashboard__metrics{grid-template-columns:repeat(2,minmax(0,1fr));", styles);
        Assert.Contains("@media(max-width:767.98px)", draftStyles);
        Assert.Contains("@media(max-width:575.98px)", draftStyles);
        Assert.Contains("@media(max-width:359.98px)", draftStyles);
        Assert.Contains("env(safe-area-inset-bottom)", draftStyles);
        Assert.Contains("min-height:44px;", draftStyles);
        Assert.True(ContrastRatio("#E5E7EB", "#111827") >= 4.5);
        Assert.True(ContrastRatio("#38BDF8", "#0B132B") >= 4.5);
    }

    [Theory]
    [InlineData(360)]
    [InlineData(390)]
    [InlineData(400)]
    [InlineData(430)]
    public void Seller_mobile_contracts_apply_at_requested_phone_widths(int viewportWidth)
    {
        var sellerCss = File.ReadAllText(WebFile("wwwroot", "css", "seller-area.css"));
        var draftCss = File.ReadAllText(WebFile("wwwroot", "css", "seller-order-drafts.css"));
        var shellMobileRules = string.Join('\n', MediaBlocks(sellerCss, "@media (max-width: 767.98px)"));
        var draftMobileRules = string.Join('\n', MediaBlocks(draftCss, "@media (max-width: 767.98px)"));
        var narrowPhoneRules = string.Join('\n', MediaBlocks(draftCss, "@media (max-width: 575.98px)"));

        Assert.InRange(viewportWidth, 360, 430);
        Assert.True(viewportWidth <= 767.98);
        Assert.True(viewportWidth <= 575.98);
        Assert.True(viewportWidth > 359.98);
        Assert.Contains(".seller-app-shell", shellMobileRules);
        Assert.Contains(".seller-app-content", shellMobileRules);
        Assert.Contains(".seller-orders-mobile", shellMobileRules);
        Assert.Contains(".seller-order-drafts__actions", draftMobileRules);
        Assert.Contains(".seller-order-drafts__review-item", narrowPhoneRules);
    }

    [Fact]
    public void Order_review_and_action_buttons_use_the_seller_palette_in_dark_mode()
    {
        var styles = System.Text.RegularExpressions.Regex.Replace(
            File.ReadAllText(WebFile("wwwroot", "css", "seller-order-drafts.css")), @"\s+", string.Empty);

        Assert.Contains("--draft-bg:#0B132B;", styles);
        Assert.Contains("--draft-card:#111827;", styles);
        Assert.Contains("--draft-border:#374151;", styles);
        Assert.Contains("--draft-text:#E5E7EB;", styles);
        Assert.Contains("--draft-muted:#94A3B8;", styles);
        Assert.Contains("--draft-blue:#38BDF8;", styles);
        Assert.Contains("--draft-action:#0E7490;", styles);
        Assert.Contains("background:#0B5D75;", styles);
        Assert.Contains(".seller-order-drafts__review-totalstrong{color:#38BDF8;", styles);
        Assert.Contains("html[data-theme=\"dark\"] .seller-order-drafts__notice--success", File.ReadAllText(WebFile("wwwroot", "css", "seller-order-drafts.css")));
    }

    [Fact]
    public void Submitted_draft_uses_a_get_confirmation_and_offers_seller_safe_next_actions()
    {
        var controller = File.ReadAllText(WebFile("Areas", "Vendedor", "Controllers", "OrderDraftsController.cs"));
        var confirmation = WebFile("Areas", "Vendedor", "Views", "OrderDrafts", "Submitted.cshtml");

        Assert.Contains("RedirectToAction(nameof(Submitted)", controller);
        Assert.Contains("[HttpGet(\"/Vendedor/Rascunhos/{id:int}/Confirmacao\")]", controller);
        Assert.True(File.Exists(confirmation));
        var view = File.ReadAllText(confirmation);
        Assert.Contains("Pedido enviado", view);
        Assert.Contains("OrderNumber", view);
        Assert.Contains("Model.Total", view);
        Assert.Contains("Dashboard", view);
        Assert.Contains("Details", view);
        Assert.Contains("OrderDrafts", view);
        Assert.DoesNotContain("Checkout", view);
        Assert.DoesNotContain("Catalog", view);
    }
}
