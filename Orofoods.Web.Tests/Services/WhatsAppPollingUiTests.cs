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
    public void Polling_keeps_refreshing_the_conversation_list_without_an_open_thread()
    {
        var root = FindRepositoryRoot();
        var view = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Areas", "Admin", "Views", "WhatsApp", "Index.cshtml"));
        var script = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "wwwroot", "js", "whatsapp-composer.js"));

        Assert.Contains("class=\"whatsapp-inbox\" data-updates-url=", view);
        Assert.Contains("data-selected-conversation-id=\"@(selected?.Id)\"", view);
        Assert.Contains("const updatesUrl = inbox?.dataset.updatesUrl || form?.dataset.updatesUrl", script);
        Assert.Contains("if (!conversationList || !updatesUrl) return", script);
        Assert.Contains("url.searchParams.delete('conversationId')", script);
        Assert.DoesNotContain("if (!form || !messageList || !conversationList) return", script);
    }

    [Fact]
    public void Polling_recovers_after_errors_and_resumes_immediately_when_visible()
    {
        var script = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Orofoods.Web", "wwwroot", "js", "whatsapp-composer.js"));

        Assert.Contains("setInterval(synchronize, 4000)", script);
        Assert.Contains("controller.abort()", script);
        Assert.Contains("catch (error)", script);
        Assert.Contains("finally", script);
        Assert.Contains("requestInFlight = false", script);
        Assert.Contains("document.addEventListener('visibilitychange'", script);
        Assert.Contains("if (!document.hidden) void synchronize()", script);
        Assert.Contains("cache: 'no-store'", script);
        Assert.Contains("content-type", script);
    }

    [Fact]
    public void Polling_inserts_messages_once_and_reorders_conversation_rows_from_the_latest_payload()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "wwwroot", "js", "whatsapp-composer.js"));

        Assert.Contains("updateConversations(payload.conversations || [])", script);
        Assert.Contains("conversationList.append(element)", script);
        Assert.Contains("visibleIds.has(String(element.dataset.conversationId))", script);
        Assert.Contains("messageList.querySelector(`[data-message-id=\"${message.id}\"]`)", script);
        Assert.Contains("if (!article)", script);
        Assert.Contains("messageList.append(article)", script);
        Assert.Contains("updateMessages(payload.messages || [])", script);
    }

    [Fact]
    public void Polling_has_no_client_cursor_and_renders_all_supported_media_types()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "wwwroot", "js", "whatsapp-composer.js"));
        var controller = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Areas", "Admin", "Controllers", "WhatsAppController.cs"));

        Assert.Contains("url.searchParams.set('conversationId', String(selectedConversationId))", script);
        Assert.Contains("url.searchParams.delete('conversationId')", script);
        Assert.DoesNotContain("searchParams.set('cursor'", script);
        Assert.DoesNotContain("searchParams.set('after'", script);
        Assert.Contains(".Take(100)", controller);
        Assert.Contains("message.type === 'image'", script);
        Assert.Contains("message.type === 'audio'", script);
        Assert.Contains("message.type === 'video'", script);
        Assert.Contains("message.type === 'document'", script);
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
        Assert.Contains("knownInboundIds", script);
        Assert.Contains("message.direction === 'inbound'", script);
        Assert.Contains("!initialState", script);
        Assert.Contains("playInboundBeep()", script);
        Assert.Contains("soundToggle", script);
        Assert.Contains("AudioContext", script);
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
}
