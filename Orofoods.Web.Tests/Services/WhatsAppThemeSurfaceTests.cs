using System.Text.RegularExpressions;

namespace Orofoods.Web.Tests.Services;

public class WhatsAppThemeSurfaceTests
{
    [Fact]
    public void Dark_color_scheme_rule_is_closed_before_later_crm_theme_rules()
    {
        var css = Read("Orofoods.Web", "wwwroot", "css", "whatsapp-composer.css");

        Assert.Matches(
            new Regex("(?s)\\[data-theme=\\\"dark\\\"] body:has\\(\\.whatsapp-inbox-page\\) \\.whatsapp-inbox-page :is\\(input, textarea, select\\)\\s*\\{\\s*color-scheme:\\s*dark;\\s*\\}"),
            css);
    }

    [Fact]
    public void Customer_dialog_and_search_input_have_opaque_theme_surfaces_and_separate_backdrop()
    {
        var markup = Read("Orofoods.Web", "Areas", "Admin", "Views", "WhatsApp", "Index.cshtml");
        var css = Read("Orofoods.Web", "wwwroot", "css", "whatsapp-composer.css");

        Assert.Contains("class=\"whatsapp-customer-dialog\"", markup);
        Assert.Contains("id=\"whatsappCustomerSearch\"", markup);

        var dialogRule = GetRule(css, "body:has(.whatsapp-inbox-page) .whatsapp-customer-dialog");
        Assert.Contains("background-color: var(--crm-surface);", dialogRule);
        Assert.Contains("color: var(--crm-text);", dialogRule);
        Assert.DoesNotContain("opacity:", dialogRule);

        var inputRule = GetRule(css, """body:has(.whatsapp-inbox-page) .whatsapp-customer-dialog .whatsapp-customer-search-form input[type="search"]""");
        Assert.Contains("background-color: var(--crm-surface-muted);", inputRule);
        Assert.Contains("color: var(--crm-text);", inputRule);

        var backdropRule = GetRule(css, "body:has(.whatsapp-inbox-page) .whatsapp-customer-dialog::backdrop");
        Assert.Contains("background: rgba(0, 0, 0, .72);", backdropRule);
        Assert.DoesNotContain("opacity:", backdropRule);

        var tokens = Read("Orofoods.Web", "wwwroot", "css", "whatsapp-composer.css");
        Assert.Contains("--crm-surface: #fffdf9;", tokens);
        Assert.Contains("--crm-surface: #1b2632;", tokens);
    }

    [Fact]
    public void Sound_toggle_uses_light_dark_tokens_for_each_state_without_changing_its_script()
    {
        var markup = Read("Orofoods.Web", "Areas", "Admin", "Views", "WhatsApp", "Index.cshtml");
        var css = Read("Orofoods.Web", "wwwroot", "css", "whatsapp-composer.css");
        var script = Read("Orofoods.Web", "wwwroot", "js", "whatsapp-composer.js");

        Assert.Contains("class=\"whatsapp-sound-toggle\"", markup);
        Assert.Contains("aria-pressed=\"true\"", markup);
        Assert.Contains("body:has(.whatsapp-inbox-page) .whatsapp-header-actions .whatsapp-sound-toggle[aria-pressed=\"true\"]", css);
        Assert.Contains("body:has(.whatsapp-inbox-page) .whatsapp-header-actions .whatsapp-sound-toggle[aria-pressed=\"false\"]", css);
        Assert.Contains("html[data-theme=\"dark\"] .whatsapp-inbox-page > .container > .whatsapp-page-heading > .whatsapp-header-actions > .whatsapp-sound-toggle[aria-pressed=\"true\"]", css);
        Assert.Contains("html[data-theme=\"dark\"] .whatsapp-inbox-page > .container > .whatsapp-page-heading > .whatsapp-header-actions > .whatsapp-sound-toggle[aria-pressed=\"false\"]", css);
        Assert.Contains(".whatsapp-sound-toggle:hover", css);
        Assert.Contains(".whatsapp-sound-toggle:focus-visible", css);
        Assert.Contains(".whatsapp-sound-toggle:active", css);

        Assert.Contains("soundToggle?.addEventListener('click'", script);
        Assert.Contains("unlockAudio()", script);
        Assert.Contains("soundPreferenceKey", script);
    }

    private static string GetRule(string css, string selector)
    {
        var escapedSelector = Regex.Escape(selector);
        var matches = Regex.Matches(css, $"(?m)^{escapedSelector}\\s*\\{{(?<body>[^{{}}]*)\\}}");
        Assert.NotEmpty(matches);
        return matches[matches.Count - 1].Groups["body"].Value;
    }

    private static string Read(params string[] segments) => File.ReadAllText(Path.Combine(FindRepositoryRoot(), Path.Combine(segments)));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Orofoods.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
