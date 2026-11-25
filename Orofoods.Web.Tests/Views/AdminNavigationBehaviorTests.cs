namespace Orofoods.Web.Tests.Views;

public class AdminNavigationBehaviorTests
{
    [Fact]
    public void Admin_navigation_exposes_a_new_tab_default_and_a_same_tab_option()
    {
        var partial = ReadWebFile("Views", "Shared", "_AdminNavigation.cshtml");

        Assert.Contains("data-admin-navigation-menu", partial);
        Assert.Contains("data-admin-navigation-mode", partial);
        Assert.Contains("<option value=\"new\" selected", partial);
        Assert.Contains("<option value=\"same\"", partial);
        Assert.Contains("Ações de gravação", partial);
        Assert.Contains("aria-label=\"Preferência de navegação administrativa\"", partial);
        Assert.Contains("if (!isWhatsAppPage)", partial);
    }

    [Fact]
    public void Admin_layout_renders_the_shared_navigation_for_desktop_and_mobile()
    {
        var layout = ReadWebFile("Views", "Shared", "_AdminLayout.cshtml");
        var navigation = ReadWebFile("Views", "Shared", "_AdminNavigation.cshtml");

        Assert.Equal(2, layout.Split("PartialAsync(\"_AdminNavigation\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("AdminNavPrefix\"] = \"desktop\"", layout);
        Assert.Contains("AdminNavPrefix\"] = \"mobile\"", layout);
        Assert.Contains("data-admin-navigation-menu", navigation);
    }

    [Fact]
    public void Admin_navigation_script_changes_only_menu_link_targets_and_keeps_native_gestures()
    {
        var script = ReadWebFile("wwwroot", "js", "admin-navigation.js");

        Assert.Contains("[data-admin-navigation-menu] a.admin-nav-link[href]", script);
        Assert.Contains("target", script);
        Assert.Contains("noopener", script);
        Assert.Contains("addEventListener('beforeunload'", script);
        Assert.Contains("event.defaultPrevented", script);
        Assert.Contains("setTimeout", script);
        Assert.Contains("const intentionalSubmits = new WeakSet()", script);
        Assert.Contains("intentionalSubmits.add(form)", script);
        Assert.Contains("!intentionalSubmits.has(form)", script);
        Assert.Contains("field.name || field.id", script);
        Assert.DoesNotContain("let intentionalSubmit", script);
        Assert.DoesNotContain("navigationLink.addEventListener('click'", script);
        Assert.DoesNotContain("navigationLink.addEventListener(\"click\"", script);
        Assert.DoesNotContain("sessionStorage", script);
    }

    [Fact]
    public void Admin_navigation_script_uses_only_the_two_non_sensitive_preference_values()
    {
        var script = ReadWebFile("wwwroot", "js", "admin-navigation.js");

        Assert.Contains("const navigationStorageKey", script);
        Assert.Contains("localStorage.setItem(navigationStorageKey, normalizedMode)", script);
        Assert.Contains("mode === 'same' ? 'same' : 'new'", script);
        Assert.Contains("'password'", script);
        Assert.Contains("'hidden'", script);
        Assert.Contains("'file'", script);
        Assert.DoesNotContain("localStorage.setItem(navigationStorageKey, form", script);
        Assert.DoesNotContain("localStorage.setItem(navigationStorageKey, JSON", script);
    }

    [Fact]
    public void Admin_navigation_styles_keep_the_preference_compact_responsive_dark_and_cyan()
    {
        var styles = ReadWebFile("wwwroot", "css", "admin-navigation.css");

        Assert.Contains(".admin-navigation-preference", styles);
        Assert.Contains("@media (max-width: 1199.98px)", styles);
        Assert.Contains("html[data-theme=\"dark\"] body.admin-authenticated", styles);
        Assert.Contains("#38BDF8", styles);
        Assert.Contains("focus-visible", styles);
    }

    private static string ReadWebFile(params string[] path)
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        return File.ReadAllText(Path.Combine(new[] { projectPath }.Concat(path).ToArray()));
    }
}
