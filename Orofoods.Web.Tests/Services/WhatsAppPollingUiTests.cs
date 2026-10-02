using System.Text.Json;
using Jint;

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

    [Theory]
    [InlineData(true, "open", true, 2)]
    [InlineData(false, "pending", false, 2)]
    [InlineData(true, "closed", false, 0)]
    [InlineData(false, "pending", true, 0)]
    [InlineData(false, "open", true, 100)]
    public async Task Sync_continues_after_optional_unread_badge_is_missing(
        bool identified, string status, bool initialBadgePresent, int unreadCount)
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "wwwroot", "js", "whatsapp-composer.js"));
        var view = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Areas", "Admin", "Views", "WhatsApp", "Index.cshtml"));

        Assert.Contains("var hasUnread = conversation.UnreadCount > 0;", view);
        Assert.Contains("@if (hasUnread)", view);
        Assert.Contains("class=\"whatsapp-unread-badge\"", view);

        var updateConversation = ExtractBetween(script, "    const updateConversation = (element, conversation) =>", "    const applyConversationFilter =");
        var updateConversations = ExtractBetween(script, "    const updateConversations = conversations =>", "    const synchronize = async () =>");
        var updateMessages = ExtractBetween(script, "    const updateMessages = messages =>", "    const conversationElement = conversation =>");
        var synchronize = ExtractBetween(script, "    const synchronize = async () =>", "    form.addEventListener('submit'");
        var scenario = JsonSerializer.Serialize(new { identified, status, initialBadgePresent, unreadCount });

        const string harness = """
            const scenario = __SCENARIO__;
            const selectedConversationId = 42;
            const updatesUrl = '/Admin/WhatsApp/Updates';
            let requestInFlight = false;
            let initialState = false;
            const notificationSeenInboundIds = new Set();
            const renderedMessageElements = new Map();
            const newMessageButton = null;
            const testConversation = {
                id: selectedConversationId,
                identified: scenario.identified,
                name: scenario.identified ? 'Cliente de teste' : 'Contato de teste',
                status: scenario.status,
                unreadCount: scenario.unreadCount,
                lastMessageAt: '2026-10-02T12:00:00Z',
                preview: 'Prévia sintética',
                messageType: 'text',
                lastMessageDirection: 'inbound',
                lastMessageId: 7001
            };
            const makeNode = () => ({
                get textContent() { return this._textContent ?? ''; },
                set textContent(value) { this._textContent = String(value); },
                dateTime: '', hidden: false, className: '', attributes: {}, dataset: {}, children: [],
                classList: { toggle() {}, remove() {}, add() {} },
                setAttribute(name, value) { this.attributes[name] = value; },
                replaceChildren() { this.children = []; },
                append(...nodes) { this.children.push(...nodes); }
            });
            const title = makeNode();
            const time = makeNode();
            const previewText = makeNode();
            let unreadBadge = scenario.initialBadgePresent ? makeNode() : null;
            const previewContainer = {
                append(node) {
                    if (node.className === 'whatsapp-unread-badge') unreadBadge = node;
                }
            };
            previewText.parentElement = previewContainer;
            const conversationMeta = makeNode();
            const existingConversation = {
                dataset: { conversationId: String(selectedConversationId) },
                classList: { toggle() {}, remove() {}, add() {} },
                setAttribute() {},
                querySelector(selector) {
                    if (selector === '.whatsapp-conversation-title strong') return title;
                    if (selector === '.whatsapp-conversation-title time') return time;
                    if (selector === '.whatsapp-conversation-preview > span:first-child') return previewText;
                    if (selector === '.whatsapp-conversation-preview') return previewContainer;
                    if (selector === '.whatsapp-unread-badge') return unreadBadge;
                    if (selector === '.whatsapp-conversation-meta') return conversationMeta;
                    return null;
                }
            };
            const conversationList = {
                querySelector(selector) {
                    return selector.startsWith('[data-conversation-id=') ? existingConversation : null;
                },
                querySelectorAll(selector) {
                    return selector === '[data-conversation-id]' ? [existingConversation] : [];
                },
                append() {}
            };
            const appendedMessages = [];
            const messageList = {
                scrollHeight: 0, scrollTop: 0, clientHeight: 100,
                querySelector() { return null; },
                querySelectorAll() { return []; },
                append(article) { appendedMessages.push(article); }
            };
            const conversationSearch = null;
            const applyConversationFilter = () => {};
            const isNearBottom = () => false;
            const scrollToBottom = () => {};
            const statusLabel = value => ({ open: 'Em atendimento', pending: 'Novo', closed: 'Finalizado' })[value];
            const formatTime = value => value ? '12:00' : '';
            const iconForType = () => null;
            const appendIcon = () => {};
            const renderMessage = (article, message) => {
                article.dataset.renderKey = String(message.id);
                article.textBody = message.textBody;
            };
            const document = {
                hidden: false,
                createElement() { return makeNode(); },
                createTextNode(text) { return { textContent: String(text) }; }
            };
            const window = {
                location: { origin: 'https://example.test' },
                setTimeout() { return 1; },
                clearTimeout() {}
            };
            const AbortController = class { constructor() { this.signal = {}; } abort() {} };
            const URL = class { constructor() { this.searchParams = { set() {} }; } };
            const console = { errors: [], error(...args) { this.errors.push(args); } };
            const playInboundBeep = () => {};
            const fetch = async () => ({
                ok: true,
                json: async () => ({
                    conversations: [testConversation],
                    messages: [{ id: 7001, direction: 'inbound', textBody: 'Mensagem sintética' }]
                })
            });
            __UPDATE_CONVERSATION__
            __UPDATE_CONVERSATIONS__
            __UPDATE_MESSAGES__
            __SYNCHRONIZE__
            (async () => {
                await synchronize();
                return JSON.stringify({
                    syncErrors: console.errors.length,
                    conversationUpdated: title.textContent === testConversation.name
                        && previewText.children.map(node => node.textContent).join('') === testConversation.preview
                        && conversationMeta.textContent === statusLabel(testConversation.status),
                    badgePresent: unreadBadge !== null,
                    badgeHidden: unreadBadge?.hidden ?? null,
                    badgeText: unreadBadge?.textContent ?? null,
                    messageInserted: appendedMessages.length === 1
                        && appendedMessages[0].dataset.messageId === '7001'
                        && appendedMessages[0].textBody === 'Mensagem sintética'
                });
            })()
            """;

        var executable = harness
            .Replace("__SCENARIO__", scenario, StringComparison.Ordinal)
            .Replace("__UPDATE_CONVERSATION__", updateConversation, StringComparison.Ordinal)
            .Replace("__UPDATE_CONVERSATIONS__", updateConversations, StringComparison.Ordinal)
            .Replace("__UPDATE_MESSAGES__", updateMessages, StringComparison.Ordinal)
            .Replace("__SYNCHRONIZE__", synchronize, StringComparison.Ordinal);
        var result = await new Engine().EvaluateAsync(executable);
        using var diagnostic = JsonDocument.Parse(result.AsString());
        var state = diagnostic.RootElement;

        Assert.Equal(0, state.GetProperty("syncErrors").GetInt32());
        Assert.True(state.GetProperty("conversationUpdated").GetBoolean());
        Assert.True(state.GetProperty("messageInserted").GetBoolean());
        Assert.Equal(initialBadgePresent || unreadCount > 0, state.GetProperty("badgePresent").GetBoolean());
        if (state.GetProperty("badgePresent").GetBoolean())
        {
            Assert.Equal(unreadCount == 0, state.GetProperty("badgeHidden").GetBoolean());
            Assert.Equal(unreadCount > 99 ? "99+" : unreadCount.ToString(), state.GetProperty("badgeText").GetString());
        }
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

    private static string ExtractBetween(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find {startMarker}.");
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"Could not find {endMarker} after {startMarker}.");
        return source[start..end];
    }
}
