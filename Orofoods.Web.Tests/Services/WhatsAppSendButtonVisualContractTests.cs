using System.Text.RegularExpressions;

namespace Orofoods.Web.Tests.Services;

public class WhatsAppSendButtonVisualContractTests
{
    [Fact]
    public void Send_button_uses_a_shared_control_height_and_fixed_blue_states_in_both_themes()
    {
        var root = FindRepositoryRoot();
        var view = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Areas", "Admin", "Views", "WhatsApp", "Index.cshtml"));
        var css = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "wwwroot", "css", "whatsapp-composer.css"));
        const string scope = "html .whatsapp-inbox-page .whatsapp-compose";
        const string textareaSelector = scope + " textarea";
        const string buttonSelector = scope + " .whatsapp-send-button";
        var composerRule = ReadCssRule(css, scope);
        var textareaRule = ReadCssRule(css, textareaSelector);
        var buttonRule = ReadCssRule(css, buttonSelector);
        var hoverRule = ReadCssRule(css, buttonSelector + ":hover:not(:disabled)");
        var activeRule = ReadCssRule(css, buttonSelector + ":active:not(:disabled)");
        var focusRule = ReadCssRule(css, buttonSelector + ":focus-visible");
        var disabledRule = ReadCssRule(css, buttonSelector + ":disabled");

        Assert.Contains("class=\"btn whatsapp-send-button\"", view);
        Assert.Contains("--whatsapp-composer-control-height: 46px", composerRule);
        Assert.Contains("height: var(--whatsapp-composer-control-height)", textareaRule);
        Assert.Contains("min-height: var(--whatsapp-composer-control-height)", textareaRule);
        Assert.Contains("height: var(--whatsapp-composer-control-height)", buttonRule);
        Assert.Contains("min-height: var(--whatsapp-composer-control-height)", buttonRule);
        Assert.Contains("--whatsapp-send-blue: #2563EB", composerRule);
        Assert.Contains("background: var(--whatsapp-send-blue)", buttonRule);
        Assert.Contains("color: #fff", buttonRule);
        Assert.Contains("background: var(--whatsapp-send-blue-hover)", hoverRule);
        Assert.Contains("background: var(--whatsapp-send-blue-active)", activeRule);
        Assert.Contains("outline: 3px solid", focusRule);
        Assert.Contains("background: var(--whatsapp-send-blue)", disabledRule);
        Assert.Contains("@media (max-width: 1199.98px)", css);
        Assert.Contains("@media (max-width: 767.98px)", css);
        Assert.Contains("grid-template-columns: minmax(0, 1fr) 46px", css);
        Assert.DoesNotContain("btn-gold", Regex.Match(view, "<button class=\"btn whatsapp-send-button\"[\\s\\S]*?</button>").Value);
    }

    [Fact]
    public void Visual_contract_keeps_keyboard_polling_and_observability_hooks()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "wwwroot", "js", "whatsapp-composer.js"));
        var keydown = ExtractBetween(script, "const handleComposerKeydown = event =>", "textarea?.addEventListener('keydown', handleComposerKeydown);");

        Assert.Contains("event.key !== 'Enter'", keydown);
        Assert.Contains("event.shiftKey", keydown);
        Assert.Contains("form.requestSubmit(sendButton ?? undefined)", keydown);
        Assert.Contains("setInterval(synchronize, 4000)", script);
        Assert.Contains("console.error('WhatsApp sync failed.'", script);
        Assert.Contains("console.error('WhatsApp message reconciliation failed.'", script);
    }

    private static string ReadCssRule(string css, string selector)
    {
        var escapedSelector = Regex.Escape(selector);
        var matches = Regex.Matches(css, $"(?m)^{escapedSelector}\\s*\\{{(?<body>[^{{}}]*)\\}}");
        Assert.NotEmpty(matches);
        return matches[matches.Count - 1].Groups["body"].Value;
    }

    private static string ExtractBetween(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Could not find '{start}'.");
        startIndex += start.Length;
        var endIndex = source.IndexOf(end, startIndex, StringComparison.Ordinal);
        Assert.True(endIndex >= 0, $"Could not find '{end}'.");
        return source[startIndex..endIndex];
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Orofoods.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
