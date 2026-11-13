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
        Assert.Contains("--crm-surface: #FFFFFF;", tokens);
        Assert.Contains("--crm-surface: #1b2632;", tokens);
    }

    [Fact]
    public void Light_crm_uses_the_shared_slate_and_blue_palette_while_dark_tokens_remain_intact()
    {
        var css = Read("Orofoods.Web", "wwwroot", "css", "whatsapp-composer.css");
        var globals = Read("Orofoods.Web", "wwwroot", "css", "site.css");

        Assert.Contains("--crm-page-bg: #F8FAFC;", css);
        Assert.Contains("--crm-surface: #FFFFFF;", css);
        Assert.Contains("--crm-border: #E2E8F0;", css);
        Assert.Contains("--crm-text: #0F172A;", css);
        Assert.Contains("--crm-muted: #475569;", css);
        Assert.Contains("--crm-accent: #2563EB;", css);
        Assert.Contains("--crm-accent-hover: #1D4ED8;", css);
        Assert.Contains("--crm-accent-soft: #EFF6FF;", css);
        Assert.Contains("--crm-page-bg: #111720;", css);
        Assert.Contains("--crm-surface: #1b2632;", css);
        Assert.Contains("--crm-accent: #d2a24e;", css);

        Assert.Contains("--surface-canvas: #F8FAFC;", globals);
        Assert.Contains("--surface-card: #FFFFFF;", globals);
        Assert.Contains("--border-subtle: #E2E8F0;", globals);
        Assert.Contains("--text-primary: #0F172A;", globals);
        Assert.Contains("--action-primary-soft: #EFF6FF;", globals);
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

    [Fact]
    public void New_conversation_and_service_window_controls_have_compact_cyan_theme_styles()
    {
        var markup = Read("Orofoods.Web", "Areas", "Admin", "Views", "WhatsApp", "Index.cshtml");
        var css = Read("Orofoods.Web", "wwwroot", "css", "whatsapp-composer.css");

        Assert.Contains("+ Nova conversa", markup);
        Assert.Contains("data-start-url=", markup);
        Assert.Contains("type=\"tel\"", markup);
        Assert.Contains("Model.ServiceWindowOpen", markup);
        Assert.Contains("asp-action=\"SendTemplate\"", markup);
        Assert.Contains("Model.TransferCandidates", markup);
        Assert.Contains("#0E7490", css);

        var primary = GetRule(css, "body:has(.whatsapp-inbox-page) .whatsapp-inbox-page .whatsapp-primary-action");
        Assert.Contains("background-color: #0E7490;", primary);
        Assert.Contains("color: #fff;", primary);
        Assert.Contains("@media (max-width: 767.98px)", css);
    }

    [Fact]
    public void New_conversation_dialog_buttons_have_clear_theme_states_and_mobile_layout()
    {
        var markup = Read("Orofoods.Web", "Areas", "Admin", "Views", "WhatsApp", "Index.cshtml");
        var css = Read("Orofoods.Web", "wwwroot", "css", "whatsapp-composer.css");

        Assert.Contains("class=\"whatsapp-customer-search-section\"", markup);
        Assert.Contains("class=\"whatsapp-customer-dialog__divider\"", markup);
        Assert.Contains("class=\"btn btn-sm whatsapp-dialog-cancel\"", markup);
        Assert.Contains("aria-live=\"polite\"", markup);

        var primary = GetRule(css, "body:has(.whatsapp-inbox-page) .whatsapp-customer-dialog .whatsapp-primary-action");
        Assert.Contains("background-color: #0E7490;", primary);
        Assert.Contains("color: #fff;", primary);

        var hover = GetRule(css, "body:has(.whatsapp-inbox-page) .whatsapp-customer-dialog .whatsapp-primary-action:hover:not(:disabled)");
        Assert.Contains("background-color: #0B5D75;", hover);
        Assert.Contains("color: #fff;", hover);

        var disabled = GetRule(css, "body:has(.whatsapp-inbox-page) .whatsapp-customer-dialog .whatsapp-primary-action:disabled");
        Assert.Contains("background-color: var(--crm-surface-muted);", disabled);
        Assert.Contains("color: var(--crm-muted);", disabled);
        Assert.Contains("cursor: not-allowed;", disabled);

        var cancel = GetRule(css, "body:has(.whatsapp-inbox-page) .whatsapp-customer-dialog .whatsapp-dialog-cancel");
        Assert.Contains("background-color: var(--crm-surface-muted);", cancel);
        Assert.Contains("color: var(--crm-text);", cancel);
        Assert.Contains("@media (max-width: 520px)", css);
    }

    [Fact]
    public void Service_header_controls_use_aligned_cyan_actions_and_responsive_wrapping()
    {
        var markup = Read("Orofoods.Web", "Areas", "Admin", "Views", "WhatsApp", "Index.cshtml");
        var css = Read("Orofoods.Web", "wwwroot", "css", "whatsapp-composer.css");
        var triggerStart = markup.IndexOf("id=\"whatsappNewConversationTrigger\"", StringComparison.Ordinal);
        var triggerEnd = markup.IndexOf("</button>", triggerStart, StringComparison.Ordinal);
        var trigger = markup[triggerStart..triggerEnd];

        Assert.Contains("<i class=\"fa-solid fa-plus\" aria-hidden=\"true\"></i> Nova conversa", trigger);
        Assert.DoesNotContain("+ Nova conversa", trigger);

        var actions = GetRule(css, "body:has(.whatsapp-inbox-page) .whatsapp-inbox-page .whatsapp-thread-actions");
        Assert.Contains("display: flex;", actions);
        Assert.Contains("flex: 1 1 430px;", actions);
        Assert.Contains("flex-wrap: wrap;", actions);

        var button = GetRule(css, "body:has(.whatsapp-inbox-page) .whatsapp-inbox-page .whatsapp-thread-actions .whatsapp-primary-action");
        Assert.Contains("max-width: none;", button);
        Assert.Contains("min-height: 38px;", button);
        Assert.Contains("border-radius: 8px;", button);
        Assert.Contains("background-color: #0E7490;", button);
        Assert.Contains("color: #fff;", button);

        var hover = GetRule(css, "body:has(.whatsapp-inbox-page) .whatsapp-inbox-page .whatsapp-thread-actions .whatsapp-primary-action:hover:not(:disabled)");
        Assert.Contains("background-color: #0B5D75;", hover);
        Assert.Contains("color: #fff;", hover);

        var select = GetRule(css, "body:has(.whatsapp-inbox-page) .whatsapp-inbox-page .whatsapp-transfer-form select");
        Assert.Contains("min-height: 38px;", select);
        Assert.Contains("border-radius: 8px;", select);
        Assert.Contains("min-width: 0;", select);

        var header = GetRule(css, "body:has(.whatsapp-inbox-page) .whatsapp-inbox-page .whatsapp-thread-header");
        Assert.Contains("flex-wrap: wrap;", header);
        Assert.Contains("body:has(.whatsapp-inbox-page) .whatsapp-inbox-page .whatsapp-header-actions .whatsapp-channel-badge", css);
        Assert.Contains("@media (max-width: 767.98px)", css);
        Assert.Contains("@media (max-width: 420px)", css);
    }

    [Fact]
    public void Customer_search_only_shows_not_found_after_a_successful_empty_response()
    {
        var script = Read("Orofoods.Web", "wwwroot", "js", "whatsapp-composer.js");
        var searchStart = script.IndexOf("const searchCustomers = async () =>", StringComparison.Ordinal);
        var searchEnd = script.IndexOf("const openCustomerDialog =", searchStart, StringComparison.Ordinal);
        var search = script[searchStart..searchEnd];

        Assert.Contains("const renderCustomerResults = (results, emptyMessage = 'Nenhum cliente encontrado.')", script);
        Assert.Contains("renderCustomerResults([], 'Digite pelo menos 2 caracteres para buscar.')", search);
        Assert.Contains("renderCustomerResults([], 'Buscando clientes...')", search);
        Assert.Contains("renderCustomerResults((await response.json()).results || []);", search);
        Assert.Contains("renderCustomerResults([], 'Não foi possível buscar clientes. Tente novamente.')", search);
        Assert.DoesNotContain("renderCustomerResults([]);", search);
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
