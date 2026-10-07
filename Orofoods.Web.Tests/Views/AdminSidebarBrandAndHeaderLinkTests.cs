using System.Text.RegularExpressions;

namespace Orofoods.Web.Tests.Views;

public class AdminSidebarBrandAndHeaderLinkTests
{
    [Fact]
    public void AdminSidebarUsesVersionedLogoAndRetainsItWhenCollapsed()
    {
        var navigation = ReadWebFile("Views", "Shared", "_AdminNavigation.cshtml");
        var styles = ReadWebFile("wwwroot", "css", "admin-navigation.css");
        var shellStyles = ReadWebFile("wwwroot", "css", "admin-shell.css");
        var shared = ReadWebFile("wwwroot", "css", "site.css");
        var layout = ReadWebFile("Views", "Shared", "_AdminLayout.cshtml");
        var rules = ReadRules(styles);
        var sharedRules = ReadRules(shared);

        Assert.Contains("class=\"admin-sidebar-logo\" src=\"~/images/logo-orofoods-transparent.png\" asp-append-version=\"true\" alt=\"Orofoods\"", navigation);
        Assert.DoesNotContain("<small>OROFOODS</small>", navigation);
        Assert.Contains("<h2>", navigation);
        Assert.Contains("<span>", navigation);
        Assert.Contains(sharedRules, rule => rule.Selector == ".admin-sidebar-header .admin-sidebar-logo" &&
            rule.Declarations.Contains("width:82px", StringComparison.Ordinal) &&
            rule.Declarations.Contains("height:auto", StringComparison.Ordinal) &&
            rule.Declarations.Contains("object-fit:contain", StringComparison.Ordinal));
        Assert.Contains(sharedRules, rule => rule.Selector == ".admin-sidebar-collapsed .admin-sidebar-header .admin-sidebar-logo" &&
            rule.Declarations.Contains("width:34px", StringComparison.Ordinal) &&
            rule.Declarations.Contains("height:auto", StringComparison.Ordinal) &&
            rule.Declarations.Contains("margin:50px auto 0", StringComparison.Ordinal));
        Assert.Contains(rules, rule => rule.Selector == "html[data-theme=\"dark\"] body.admin-authenticated:not(:has(.whatsapp-inbox-page)) .admin-shell.admin-sidebar-collapsed .admin-sidebar > .admin-sidebar-header" &&
            rule.Declarations.Contains("padding-inline: 0", StringComparison.Ordinal));
        Assert.Contains(rules, rule => rule.Selector.Contains(".admin-sidebar-collapsed .admin-sidebar-header h2", StringComparison.Ordinal) &&
            rule.Declarations.Contains("display: none", StringComparison.Ordinal));
        Assert.DoesNotContain(sharedRules, rule => rule.Selector == ".admin-sidebar-collapsed .admin-sidebar-header .admin-sidebar-logo" &&
            rule.Declarations.Contains("display: none", StringComparison.Ordinal));

        Assert.Contains("--admin-sidebar-width: 264px", styles);
        Assert.Contains("--admin-sidebar-collapsed-width: 80px", styles);
        Assert.Contains("--admin-sidebar-width: 248px", shellStyles);
        Assert.Contains("--admin-header-height: 64px", shellStyles);
        Assert.Contains("--admin-header-height: 64px", styles);
        Assert.Contains("body.admin-authenticated:not(:has(.whatsapp-inbox-page))", styles);
        Assert.DoesNotContain("body.admin-authenticated:has(.whatsapp-inbox-page)", styles);
        Assert.Contains("width: 72px", styles);
        Assert.Contains("margin: 8px auto 0", styles);
        Assert.Contains("data-admin-sidebar-toggle", layout);
        Assert.Contains(".admin-sidebar-collapsed .admin-nav-submenu.show,", styles);
        Assert.Contains(".admin-sidebar-collapsed .admin-nav-submenu.collapsing", styles);
        Assert.Contains(rules, rule => rule.Selector == ".admin-offcanvas .admin-sidebar-header" &&
            rule.Declarations.Contains("display: none", StringComparison.Ordinal));
    }

    [Fact]
    public void AdminDarkHeaderLinksHaveScopedColorsAndKeepPublicLightContracts()
    {
        var header = ReadWebFile("wwwroot", "css", "header.css");
        var shared = ReadWebFile("wwwroot", "css", "site.css");
        const string scope = "html[data-theme=\"dark\"] body.admin-authenticated:not(:has(.whatsapp-inbox-page)) .site-header-modern .main-nav .nav-link";

        var normalRule = FindRule(header, scope);
        var interactionSelector = scope + ":hover,\r\n" + scope + ":focus-visible,\r\n" + scope + ".active";
        var interactionRule = FindRule(header, interactionSelector);
        Assert.Matches(@"color:\s*#CBD5E1", normalRule);
        Assert.Matches(@"color:\s*#F8FAFC", interactionRule);
        Assert.DoesNotContain("!important", normalRule + interactionRule, StringComparison.Ordinal);

        Assert.Contains(".nav-link:where(:not(html[data-theme=\"dark\"] body.admin-authenticated:not(:has(.whatsapp-inbox-page)) .site-header-modern .main-nav .nav-link)){color:#4d4e49!important", shared);
        Assert.Contains("html:not([data-theme=\"dark\"]) .site-header-modern .main-nav .nav-link", header);
        Assert.Contains("color: #1E293B;", header);
        Assert.Contains("outline: 2px solid var(--nav-focus);", header);
        Assert.Contains("@media(max-width:991.98px)", header);
        Assert.Contains(".site-header-modern .account-menu", header);
    }

    [Fact]
    public void WhatsAppAdminDarkTopNavigationOverridesLegacyColorOnlyInsideItsHeader()
    {
        var header = ReadWebFile("wwwroot", "css", "header.css");
        var shared = ReadWebFile("wwwroot", "css", "site.css");
        const string scope = "html[data-theme=\"dark\"] body.admin-authenticated:has(.whatsapp-inbox-page) .site-header-modern .main-nav .nav-link";
        var normalRule = FindRule(header, scope);
        var interactionSelector = scope + ":hover,\r\n" + scope + ":focus-visible,\r\n" + scope + ".active";
        var interactionRule = FindRule(header, interactionSelector);

        Assert.Matches(@"color:\s*#CBD5E1\s*!important", normalRule);
        Assert.Matches(@"color:\s*#F8FAFC\s*!important", interactionRule);
        Assert.Contains(".site-header-modern .main-nav .nav-link", scope);
        Assert.Contains("html[data-theme=\"dark\"]", scope);
        Assert.Contains("body.admin-authenticated", scope);
        Assert.DoesNotContain(".whatsapp-inbox-page .nav-link", scope);
        Assert.Contains("#4d4e49!important", shared);
        Assert.DoesNotContain("html[data-theme=\"dark\"] body.admin-authenticated:has(.whatsapp-inbox-page) .whatsapp-inbox-page", header);
    }

    [Fact]
    public void WhatsAppFailedStatusKeepsCompactLabelAndRendersEncodedDetailWithFallback()
    {
        var view = ReadWebFile("Areas", "Admin", "Views", "WhatsApp", "Index.cshtml");

        Assert.Contains("Falha no envio", view);
        Assert.Contains("whatsapp-failure-details", view);
        Assert.Contains("Motivo não informado pela Meta.", view);
        Assert.Contains("@message.ErrorCode", view);
        Assert.Contains("message.ErrorMessage", view);
        Assert.DoesNotContain("Html.Raw", view);
    }

    private static string ReadWebFile(params string[] segments)
    {
        var webProject = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        return File.ReadAllText(Path.Combine(new[] { webProject }.Concat(segments).ToArray()));
    }

    private static IReadOnlyList<CssRule> ReadRules(string styles)
    {
        return Regex.Matches(styles, @"(?<selector>[^{}]+)\{(?<declarations>[^{}]*)\}")
            .Select(match => new CssRule(match.Groups["selector"].Value.Trim(), match.Groups["declarations"].Value))
            .ToArray();
    }

    private static string FindRule(string styles, string selector)
    {
        var normalizedSelector = Regex.Replace(selector.Trim(), @"\s+", " ");
        var selectorPattern = Regex.Escape(normalizedSelector).Replace(@"\ ", @"\s+", StringComparison.Ordinal);
        var match = Regex.Match(styles, selectorPattern + @"\s*\{(?<declarations>[^{}]*)\}");
        Assert.True(match.Success, $"Missing CSS rule for {selector}");
        return match.Groups["declarations"].Value;
    }

    private sealed record CssRule(string Selector, string Declarations);
}
