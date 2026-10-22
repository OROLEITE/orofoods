namespace Orofoods.Web.Tests.Views;

public class GlobalNavigationTests
{
    [Fact]
    public void Shared_layout_places_the_back_control_inside_the_context_bar()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var layout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_Layout.cshtml"));
        var script = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "js", "site.js"));

        Assert.Contains("class=\"page-context-bar\"", layout);
        Assert.Contains("id=\"siteBackButton\"", layout);
        Assert.Contains("data-fallback-url=\"/\"", layout);
        Assert.Contains("siteBackButton", script);
    }

    [Fact]
    public void Shared_layout_does_not_render_the_context_bar_in_admin()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var layout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_Layout.cshtml"));
        var adminLayout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_AdminLayout.cshtml"));
        var adminStyles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-shell.css"));

        Assert.Contains("ViewData[\"BodyClass\"] = \"admin-authenticated\"", adminLayout);
        Assert.Contains("@if (!isCustomerExperience && !isPortalCustomerSelect && !isAdminExperience)", layout);
        Assert.DoesNotContain(".page-context-bar", adminStyles);
    }

    [Fact]
    public void Shared_layout_and_header_use_correct_portuguese_accentuation()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var layout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_Layout.cshtml"));
        var header = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_SiteHeader.cshtml"));

        Assert.Contains("In&#237;cio", header);
        Assert.Contains("Sobre n&#243;s", header);
        Assert.Contains("Navega&#231;&#227;o segura", layout);
    }

    [Fact]
    public void Shared_layout_loads_local_font_awesome_and_uses_icons_for_navigation()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var layout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_Layout.cshtml"));

        Assert.Contains("~/lib/fontawesome/css/all.min.css", layout);
        Assert.Contains("fa-arrow-left", layout);
    }

    [Fact]
    public void Shared_layout_uses_a_compact_structured_footer()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var layout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_Layout.cshtml"));

        Assert.Contains("site-footer-grid", layout);
        Assert.Contains("footer-brand", layout);
        Assert.Contains("footer-legal", layout);
        Assert.Contains("fa-building", layout);
    }

    [Fact]
    public void Shared_header_targets_public_home_routes()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var header = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_SiteHeader.cshtml"));

        Assert.Equal(6, header.Split("asp-area=\"\" asp-controller=\"Home\"").Length - 1);
    }

    [Fact]
    public void Light_header_has_high_contrast_links_and_keeps_the_dark_navigation_frame()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var header = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "header.css"));
        var navigation = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-navigation.css"));
        var globalStyles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "site.css"));

        Assert.Contains("html:not([data-theme=\"dark\"]) .site-header-modern .main-nav .nav-link", header);
        Assert.Contains("color: #1E293B;", header);
        Assert.Contains("background: var(--surface-card);", header);
        Assert.Contains("color: var(--nav-text);", navigation);
        Assert.Contains("color: var(--nav-text-active);", navigation);
        Assert.Contains("background: var(--admin-sidebar-accent);", navigation);
        Assert.Contains("--cream: var(--surface-muted);", globalStyles);
        Assert.Contains("html:not([data-theme=\"dark\"]) :is(.portal-shell, .order-page, .success-page, .registration-page)", globalStyles);
        Assert.Contains("html:not([data-theme=\"dark\"]) .customer-commercial-attention", globalStyles);
    }

    [Fact]
    public void Public_header_links_keep_aa_contrast_in_normal_hover_focus_and_active_states()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var header = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "header.css"));
        var compact = System.Text.RegularExpressions.Regex.Replace(header, @"\s+", string.Empty);

        Assert.Contains("body:not(.portal-authenticated):not(.portal-customer-select):not(.admin-authenticated):not(.seller-app-page).site-header-modern.main-nav.nav-link", compact);
        Assert.Contains("body:not(.portal-authenticated):not(.portal-customer-select):not(.admin-authenticated):not(.seller-app-page).site-header-modern.main-nav.nav-link:hover", compact);
        Assert.Contains("body:not(.portal-authenticated):not(.portal-customer-select):not(.admin-authenticated):not(.seller-app-page).site-header-modern.main-nav.nav-link:focus-visible", compact);
        Assert.Contains("body:not(.portal-authenticated):not(.portal-customer-select):not(.admin-authenticated):not(.seller-app-page).site-header-modern.main-nav.nav-link.active", compact);
        Assert.Contains("color:#1E293B!important", compact);
        Assert.Contains("color:#0F172A!important", compact);
        Assert.True(ContrastRatio("#1E293B", "#FFFFFF") >= 4.5);
        Assert.True(ContrastRatio("#0F172A", "#FFFFFF") >= 4.5);
        Assert.True(ContrastRatio("#F8FAFC", "#111827") >= 4.5);
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
}
