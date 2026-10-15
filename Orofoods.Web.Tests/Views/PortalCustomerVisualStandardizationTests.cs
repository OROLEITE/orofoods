namespace Orofoods.Web.Tests.Views;

public sealed class PortalCustomerVisualStandardizationTests
{
    private static string ProjectRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));

    [Fact]
    public void Dashboard_exposes_the_requested_portal_shortcuts()
    {
        var dashboard = Read("Views", "Portal", "Dashboard.cshtml");

        Assert.Contains("Digitar pedido", dashboard);
        Assert.Contains("class=\"customer-dashboard-shortcuts\"", dashboard);
        Assert.Contains("asp-action=\"SavedOrders\"", dashboard);
        Assert.Contains("Meus rascunhos", dashboard);
        Assert.Contains("asp-action=\"Catalog\"", dashboard);
        Assert.Contains("Ver cat&aacute;logo", dashboard);
        Assert.Contains("asp-action=\"Cart\"", dashboard);
        Assert.Contains("Carrinho", dashboard);
        Assert.Contains("asp-action=\"Orders\"", dashboard);
        Assert.Contains("Pedidos</a>", dashboard);
    }

    [Fact]
    public void Cart_keeps_empty_state_checkout_and_update_actions()
    {
        var cart = Read("Views", "Portal", "Cart.cshtml");

        Assert.Contains("class=\"empty-state customer-cart-empty\"", cart);
        Assert.Contains("Seu carrinho est&aacute; vazio.", cart);
        Assert.Contains("asp-action=\"Catalog\"", cart);
        Assert.Contains("asp-action=\"Checkout\"", cart);
        Assert.Contains("asp-action=\"UpdateCart\"", cart);
        Assert.Contains("asp-action=\"RemoveFromCart\"", cart);
        Assert.Contains("asp-action=\"SaveCartAsModel\"", cart);
    }

    [Fact]
    public void Orders_and_saved_drafts_keep_filters_and_post_actions()
    {
        var orders = Read("Views", "Portal", "Orders.cshtml");
        var savedOrders = Read("Views", "Portal", "SavedOrders.cshtml");

        Assert.Contains("name=\"q\"", orders);
        Assert.Contains("name=\"status\"", orders);
        Assert.Contains("asp-action=\"Order\"", orders);
        Assert.Contains("asp-action=\"Repeat\"", orders);
        Assert.Contains("portal-page saved-orders-page", savedOrders);
        Assert.Contains("asp-action=\"LoadSavedOrder\"", savedOrders);
        Assert.Contains("asp-action=\"EditSavedOrder\"", savedOrders);
        Assert.Contains("asp-action=\"DeactivateSavedOrder\"", savedOrders);
        Assert.Contains("name=\"id\"", savedOrders);
    }

    [Fact]
    public void Portal_order_and_draft_layout_has_mobile_rules_for_long_content()
    {
        var customerStyles = ReadCss("customer-experience.css");
        var catalogStyles = ReadCss("catalog.css");
        var darkStyles = ReadCss("portal-dark.css");

        Assert.Contains(".customer-experience .customer-dashboard-shortcuts", customerStyles);
        Assert.Contains("@media (max-width: 767.98px)", customerStyles);
        Assert.Contains(".customer-experience .saved-orders-page .product-grid", customerStyles);
        Assert.Contains(".customer-experience .saved-orders-page .product-card h2", customerStyles);
        Assert.Contains("overflow-wrap: anywhere;", customerStyles);
        Assert.Contains("@media (max-width: 575.98px)", customerStyles);
        Assert.Contains("minmax(0, 1fr)", catalogStyles);
        Assert.Contains("body.portal-authenticated .portal-app-main .customer-cart-empty__icon", darkStyles);
        Assert.Contains("body.portal-authenticated .site-header-modern .main-nav .nav-link.active::before", darkStyles);
        Assert.Contains("background: var(--authenticated-nav-active, #38BDF8);", darkStyles);
    }

    [Fact]
    public void Portal_primary_buttons_keep_white_aa_contrast_in_all_interactive_states()
    {
        var actionStyles = ReadCss("portal-actions.css");
        var darkStyles = ReadCss("portal-dark.css");
        var actionTokens = Declarations(actionStyles, "body.portal-authenticated");
        var darkActionTokens = Declarations(actionStyles, "html[data-theme=\"dark\"] body.portal-authenticated");
        var normalButton = Declarations(actionStyles, ".portal-authenticated .btn-gold");
        var hoverButton = Declarations(actionStyles, ".portal-authenticated .btn-gold:hover");
        var focusButton = Declarations(actionStyles, ".portal-authenticated .btn-gold:focus-visible");
        var text = Value(normalButton, "color").ToUpperInvariant();
        var normalBackground = Value(actionTokens, "--portal-action");
        var hoverBackground = Value(actionTokens, "--portal-action-hover");
        var lightFocus = Value(actionTokens, "--portal-action-focus");
        var darkFocus = Value(darkActionTokens, "--portal-action-focus");

        Assert.Equal("#FFFFFF", text);
        Assert.Contains("background: var(--portal-action);", normalButton);
        Assert.Equal("#FFFFFF", Value(hoverButton, "color").ToUpperInvariant());
        Assert.Contains("background: var(--portal-action-hover);", hoverButton);
        Assert.Contains("outline: 3px solid var(--portal-action-focus);", focusButton);
        Assert.Contains("--portal-dark-primary: #0E7490", darkStyles);
        Assert.Contains("--portal-dark-primary-hover: #155E75", darkStyles);
        Assert.Contains("body.portal-authenticated .portal-app-main .btn-gold:focus-visible", darkStyles);
        Assert.Contains("outline: 3px solid var(--portal-action-focus);", darkStyles);

        Assert.True(ContrastRatio(text, normalBackground) >= 4.5, "Primary button text must meet WCAG AA in its normal state.");
        Assert.True(ContrastRatio(text, hoverBackground) >= 4.5, "Primary button text must meet WCAG AA on hover.");
        Assert.True(ContrastRatio(text, normalBackground) >= 4.5, "Primary button text must meet WCAG AA while focused.");
        Assert.True(ContrastRatio(lightFocus, "#FFFFFF") >= 3, "The light-theme focus ring must remain visible.");
        Assert.True(ContrastRatio(darkFocus, "#0F172A") >= 3, "The dark-theme focus ring must remain visible.");
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { ProjectRoot }.Concat(parts).ToArray()));

    private static string ReadCss(string name) => Read("wwwroot", "css", name);

    private static string Declarations(string css, string selector)
    {
        var rule = System.Text.RegularExpressions.Regex.Match(
            css,
            @"(?s)" + System.Text.RegularExpressions.Regex.Escape(selector) + @"\s*\{(?<declarations>[^}]*)\}");
        Assert.True(rule.Success, $"Expected CSS rule for '{selector}'.");
        return rule.Groups["declarations"].Value;
    }

    private static string Value(string declarations, string property)
    {
        var declaration = System.Text.RegularExpressions.Regex.Match(
            declarations,
            System.Text.RegularExpressions.Regex.Escape(property) + @"\s*:\s*(#[0-9A-Fa-f]{6})");
        Assert.True(declaration.Success, $"Expected '{property}' to have a six-digit hex value.");
        return declaration.Groups[1].Value.ToUpperInvariant();
    }

    private static double ContrastRatio(string foreground, string background)
    {
        var first = RelativeLuminance(foreground);
        var second = RelativeLuminance(background);
        return (Math.Max(first, second) + .05) / (Math.Min(first, second) + .05);
    }

    private static double RelativeLuminance(string color)
    {
        var red = Linearize(Convert.ToInt32(color.Substring(1, 2), 16) / 255d);
        var green = Linearize(Convert.ToInt32(color.Substring(3, 2), 16) / 255d);
        var blue = Linearize(Convert.ToInt32(color.Substring(5, 2), 16) / 255d);
        return .2126 * red + .7152 * green + .0722 * blue;
    }

    private static double Linearize(double component) =>
        component <= .04045 ? component / 12.92 : Math.Pow((component + .055) / 1.055, 2.4);
}
