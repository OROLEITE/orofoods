namespace Orofoods.Web.Tests.Services;

public class WhatsAppPollingUiTests
{
    [Fact]
    public void Polling_is_incremental_single_flight_and_preserves_the_composer_value()
    {
        var script = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Orofoods.Web", "wwwroot", "js", "whatsapp-composer.js"));

        Assert.Contains("setInterval(synchronize, 4000)", script);
        Assert.Contains("requestInFlight", script);
        Assert.Contains("document.hidden", script);
        Assert.Contains("AbortController", script);
        Assert.Contains("cache: 'no-store'", script);
        Assert.Contains("America/Sao_Paulo", script);
        Assert.Contains("data-message-id", script);
        Assert.Contains("article.dataset.messageId", script);
        Assert.Contains("textarea.value === submittedText", script);
        Assert.Contains("textarea.value = ''", script);
        Assert.DoesNotContain("location.reload", script);
    }

    [Fact]
    public void Sending_is_async_with_a_traditional_form_fallback_and_early_theme_bootstrap()
    {
        var root = FindRepositoryRoot();
        var view = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Areas", "Admin", "Views", "WhatsApp", "Index.cshtml"));
        var layout = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Views", "Shared", "_Layout.cshtml"));
        var script = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "wwwroot", "js", "whatsapp-composer.js"));
        var styles = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "wwwroot", "css", "whatsapp-composer.css"));
        var themeBootstrap = layout.IndexOf("savedCrmTheme", StringComparison.Ordinal);
        var firstStylesheet = layout.IndexOf("<link rel=\"stylesheet\"", StringComparison.Ordinal);

        Assert.Contains("<form asp-action=\"Send\" method=\"post\"", view);
        Assert.Contains("form.addEventListener('submit', async event =>", script);
        Assert.Contains("event.preventDefault()", script);
        Assert.Contains("new FormData(form)", script);
        Assert.Contains("if (sendInFlight)", script);
        Assert.Contains("await synchronize()", script);
        Assert.Contains("whatsapp-send-feedback", view);
        Assert.True(themeBootstrap >= 0 && themeBootstrap < firstStylesheet);
        Assert.Contains("orofoods.crm.theme", layout);
        Assert.Contains("html[data-theme=\"dark\"]:has(.whatsapp-inbox-page)", styles);
        Assert.Contains("--crm-page-bg", styles);
        Assert.Contains("--crm-chat-bg", styles);
    }

    [Fact]
    public void Customer_actions_share_a_spaced_container_and_theme_controls_use_shared_tokens()
    {
        var root = FindRepositoryRoot();
        var view = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Areas", "Admin", "Views", "WhatsApp", "Index.cshtml"));
        var styles = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "wwwroot", "css", "whatsapp-composer.css"));

        Assert.Contains("whatsapp-customer-actions", view);
        Assert.Contains("whatsapp-unlink-customer", view);
        Assert.Contains(".whatsapp-customer-actions {", styles);
        Assert.Contains("gap: 12px", styles);
        Assert.Contains(".whatsapp-unlink-customer:hover", styles);
        Assert.Contains(".whatsapp-sound-toggle", styles);
        Assert.Contains(".whatsapp-theme-toggle", styles);
        Assert.Contains(".whatsapp-channel-badge", styles);
        Assert.Contains("--crm-message-in", styles);
        Assert.Contains("--crm-message-out", styles);
    }

    [Fact]
    public void Inbound_sound_is_opt_in_to_new_ids_and_batch_coalesced()
    {
        var script = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Orofoods.Web", "wwwroot", "js", "whatsapp-composer.js"));

        Assert.Contains("localStorage", script);
        Assert.Contains("notificationSeenInboundIds", script);
        Assert.Contains("message.direction === 'inbound'", script);
        Assert.Contains("!initialState", script);
        Assert.Contains("playInboundBeep()", script);
        Assert.Contains("soundToggle", script);
        Assert.Contains("AudioContext", script);
    }

    [Fact]
    public void Message_reconciliation_deduplicates_against_rendered_dom_and_isolates_render_failures()
    {
        var script = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Orofoods.Web", "wwwroot", "js", "whatsapp-composer.js"));
        var reconcileStart = script.IndexOf("const updateMessages = messages =>", StringComparison.Ordinal);
        var reconcileEnd = script.IndexOf("const conversationElement =", reconcileStart, StringComparison.Ordinal);
        var renderMessageStart = script.IndexOf("const renderMessage = (article, message) =>", StringComparison.Ordinal);
        var renderKeyPosition = script.IndexOf("article.dataset.renderKey = key;", renderMessageStart, StringComparison.Ordinal);
        var appendFooterPosition = script.IndexOf("article.append(footer);", renderMessageStart, StringComparison.Ordinal);
        var appendMessagePosition = script.IndexOf("messageList.append(article);", reconcileStart, StringComparison.Ordinal);
        var renderMessagePosition = script.IndexOf("renderMessage(article, message);", reconcileStart, StringComparison.Ordinal);
        var reconcile = script[reconcileStart..reconcileEnd];

        Assert.Contains("const renderedMessageElements = new Map(", script);
        Assert.Contains("messageList.querySelectorAll('[data-message-id]')", script);
        Assert.Contains("renderedMessageElements.get(messageId)", reconcile);
        Assert.DoesNotContain("notificationSeenInboundIds.has", reconcile);
        Assert.Contains("console.error('WhatsApp message reconciliation failed.'", reconcile);
        Assert.Contains("messageId,\n                    name: error?.name ?? null,\n                    message: error?.message ?? null", reconcile);
        Assert.True(appendFooterPosition >= 0 && renderKeyPosition > appendFooterPosition,
            "A message must receive its render key only after its DOM has been fully built.");
        Assert.True(renderMessagePosition >= 0 && appendMessagePosition > renderMessagePosition,
            "New messages must be rendered successfully before they are appended.");
    }

    [Fact]
    public void Sync_failure_reports_the_current_stage_and_keeps_abort_silent()
    {
        var sync = ReadSynchronizeFunction();
        var catchStart = sync.IndexOf("} catch (error) {", StringComparison.Ordinal);
        var finallyStart = sync.IndexOf("} finally {", catchStart, StringComparison.Ordinal);
        var catchBlock = sync[catchStart..finallyStart];

        Assert.Contains("let syncStage = 'start';", sync);
        Assert.Contains("if (error?.name !== 'AbortError')", catchBlock);
        Assert.Contains("console.error('WhatsApp sync failed.'", catchBlock);
        Assert.Contains("stage: syncStage", catchBlock);
        Assert.Contains("name: error?.name ?? null", catchBlock);
        Assert.Contains("message: error?.message ?? null", catchBlock);
        Assert.DoesNotContain("WhatsApp sync unavailable.", sync);
        Assert.DoesNotContain("payload", catchBlock);
        Assert.DoesNotContain("textBody", catchBlock);
        Assert.DoesNotContain("caption", catchBlock);
        Assert.DoesNotContain("fileName", catchBlock);
        Assert.DoesNotContain("phoneNumber", catchBlock);
    }

    [Fact]
    public void Sync_stage_markers_identify_each_operation_and_release_the_polling_lock()
    {
        var sync = ReadSynchronizeFunction();

        AssertStagePrecedes(sync, "syncStage = 'fetch';", "await fetch(url,");
        AssertStagePrecedes(sync, "syncStage = 'parse';", "await response.json()");
        AssertStagePrecedes(sync, "syncStage = 'update-conversations';", "updateConversations(payload.conversations || [])");
        AssertStagePrecedes(sync, "syncStage = 'update-messages';", "updateMessages(payload.messages || [])");
        AssertStagePrecedes(sync, "syncStage = 'notification';", "const newInbound =");
        AssertStagePrecedes(sync, "syncStage = 'beep';", "playInboundBeep()");

        var finallyStart = sync.IndexOf("} finally {", StringComparison.Ordinal);
        var finallyBlock = sync[finallyStart..];
        Assert.Contains("window.clearTimeout(timeoutId);", finallyBlock);
        Assert.Contains("requestInFlight = false;", finallyBlock);
        Assert.True(finallyBlock.IndexOf("window.clearTimeout(timeoutId);", StringComparison.Ordinal)
            < finallyBlock.IndexOf("requestInFlight = false;", StringComparison.Ordinal));
    }

    [Fact]
    public void Per_message_reconciliation_diagnostic_contains_only_technical_error_fields()
    {
        var script = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Orofoods.Web", "wwwroot", "js", "whatsapp-composer.js"));
        var reconcileStart = script.IndexOf("const updateMessages = messages =>", StringComparison.Ordinal);
        var reconcileEnd = script.IndexOf("const conversationElement =", reconcileStart, StringComparison.Ordinal);
        var reconcile = script[reconcileStart..reconcileEnd];
        var diagnosticStart = reconcile.IndexOf("console.error('WhatsApp message reconciliation failed.'", StringComparison.Ordinal);
        var diagnosticEnd = reconcile.IndexOf("continue;", diagnosticStart, StringComparison.Ordinal);
        var diagnostic = reconcile[diagnosticStart..diagnosticEnd];

        Assert.Contains("messageId", diagnostic);
        Assert.Contains("name: error?.name ?? null", diagnostic);
        Assert.Contains("message: error?.message ?? null", diagnostic);
        Assert.DoesNotContain("payload", diagnostic);
        Assert.DoesNotContain("textBody", diagnostic);
        Assert.DoesNotContain("caption", diagnostic);
        Assert.DoesNotContain("fileName", diagnostic);
        Assert.DoesNotContain("phoneNumber", diagnostic);
        Assert.DoesNotContain("customer", diagnostic);
    }

    [Fact]
    public void Customer_linking_uses_search_modal_instead_of_loading_a_permanent_select()
    {
        var view = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Orofoods.Web", "Areas", "Admin", "Views", "WhatsApp", "Index.cshtml"));

        Assert.Contains("whatsappCustomerDialog", view);
        Assert.Contains("CustomerSearch", view);
        Assert.DoesNotContain("Model.CustomerChoices", view);
        Assert.DoesNotContain("<select id=\"customerId\"", view);
    }

    [Fact]
    public void Theme_and_conversation_search_are_additive_to_the_existing_whatsapp_page()
    {
        var root = FindRepositoryRoot();
        var view = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Areas", "Admin", "Views", "WhatsApp", "Index.cshtml"));
        var script = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "wwwroot", "js", "whatsapp-composer.js"));
        var styles = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "wwwroot", "css", "whatsapp-composer.css"));

        Assert.Contains("whatsapp-theme-toggle", view);
        Assert.Contains("orofoods.crm.theme", script);
        Assert.Contains("whatsapp-search-input", view);
        Assert.Contains("data-search-phone", view);
        Assert.Contains("conversation.textContent", script);
        Assert.Contains("has-explicit-selection", view);
        Assert.Contains("whatsapp-mobile-back", view);
        Assert.Contains("grid-template-columns: minmax(300px, 330px) minmax(0, 1fr) minmax(300px, 330px)", styles);
        Assert.Contains("[data-theme=\"dark\"]", styles);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Orofoods.Web")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static string ReadSynchronizeFunction()
    {
        var script = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Orofoods.Web", "wwwroot", "js", "whatsapp-composer.js"));
        var syncStart = script.IndexOf("const synchronize = async () =>", StringComparison.Ordinal);
        var syncEnd = script.IndexOf("form.addEventListener('submit'", syncStart, StringComparison.Ordinal);
        return script[syncStart..syncEnd];
    }

    private static void AssertStagePrecedes(string sync, string stageMarker, string operation)
    {
        var stagePosition = sync.IndexOf(stageMarker, StringComparison.Ordinal);
        var operationPosition = sync.IndexOf(operation, stagePosition, StringComparison.Ordinal);

        Assert.True(stagePosition >= 0 && operationPosition > stagePosition,
            $"Expected {stageMarker} before {operation}.");
    }
}
