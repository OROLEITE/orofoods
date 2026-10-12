using System.Text.Json;
using Jint;

namespace Orofoods.Web.Tests.Services;

public class CommercialKanbanInteractionTests
{
    [Fact]
    public async Task Rejected_move_restores_original_column_and_blocks_a_second_drag_while_pending()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var script = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "js", "commercial-kanban.js"));
        const string harness = """
            class Element {
                constructor(kind, dataset = {}) {
                    this.kind = kind;
                    this.dataset = { ...dataset };
                    this.children = [];
                    this.parentElement = null;
                    this.style = {};
                    this.textContent = '';
                    this.hidden = false;
                    this.classes = new Set();
                    this.classList = {
                        add: value => this.classes.add(value),
                        remove: value => this.classes.delete(value),
                        contains: value => this.classes.has(value),
                        toggle: (value, force) => {
                            const enabled = force ?? !this.classes.has(value);
                            if (enabled) this.classes.add(value); else this.classes.delete(value);
                            return enabled;
                        }
                    };
                }
                matches(selector) {
                    return (selector === '.commercial-activity-card[data-activity-id]' && this.kind === 'card') ||
                        (selector === '[data-kanban-cards]' && this.kind === 'cards') ||
                        (selector === '[data-kanban-column]' && this.kind === 'column') ||
                        (selector === '[data-status-count]' && this.kind === 'count') ||
                        (selector === '[data-kanban-feedback]' && this.kind === 'feedback') ||
                        (selector === '[data-kanban-empty]' && this.dataset.kanbanEmpty !== undefined);
                }
                closest(selector) {
                    for (let current = this; current; current = current.parentElement)
                        if (current.matches(selector)) return current;
                    return null;
                }
                querySelector(selector) {
                    if (this.kind === 'board' && selector === '[data-kanban-feedback]') return feedback;
                    if (this.kind === 'column' && selector === '[data-kanban-cards]') return this.cardsContainer;
                    if (this.kind === 'column' && selector === '[data-status-count]') return this.count;
                    if (this.kind === 'cards' && selector === '[data-kanban-empty]') return this.children.find(child => child.matches(selector)) ?? null;
                    return null;
                }
                querySelectorAll(selector) {
                    if (this.kind === 'board' && selector === '.commercial-activity-card[data-activity-id]')
                        return columns.flatMap(column => column.cardsContainer.children.filter(child => child.kind === 'card'));
                    if (this.kind === 'board' && selector === '.commercial-kanban-column.is-drop-target')
                        return columns.filter(column => column.classList.contains('is-drop-target'));
                    if (this.kind === 'cards' && selector === ':scope > .commercial-activity-card')
                        return this.children.filter(child => child.kind === 'card');
                    return [];
                }
                addEventListener(name, handler) { listeners[name] = handler; }
                appendChild(child) {
                    child.remove();
                    this.children.push(child);
                    child.parentElement = this;
                    return child;
                }
                insertBefore(child, sibling) {
                    child.remove();
                    const index = this.children.indexOf(sibling);
                    if (index < 0) return this.appendChild(child);
                    this.children.splice(index, 0, child);
                    child.parentElement = this;
                    return child;
                }
                remove() {
                    if (!this.parentElement) return;
                    const siblings = this.parentElement.children;
                    const index = siblings.indexOf(this);
                    if (index >= 0) siblings.splice(index, 1);
                    this.parentElement = null;
                }
                contains(element) {
                    for (let current = element; current; current = current.parentElement)
                        if (current === this) return true;
                    return false;
                }
                get nextElementSibling() {
                    if (!this.parentElement) return null;
                    const siblings = this.parentElement.children;
                    return siblings[siblings.indexOf(this) + 1] ?? null;
                }
            }
            const listeners = {};
            const feedback = new Element('feedback');
            const board = new Element('board');
            board.querySelector = selector => selector === '[data-kanban-feedback]' ? feedback : null;
            const statusForm = { action: '/Admin/Customers/CompleteActivity' };
            const makeColumn = (status, label) => {
                const column = new Element('column', { status, statusLabel: label });
                column.count = new Element('count');
                column.cardsContainer = new Element('cards');
                column.appendChild(column.cardsContainer);
                return column;
            };
            const source = makeColumn('0', 'Aguardando');
            const target = makeColumn('1', 'Em negociação');
            const card = new Element('card', { activityId: '45', customerId: '12', currentStatus: '0' });
            source.cardsContainer.appendChild(card);
            const targetEmpty = new Element('empty', { kanbanEmpty: '' });
            target.cardsContainer.appendChild(targetEmpty);
            const columns = [source, target];
            board.querySelectorAll = selector => {
                if (selector === '.commercial-activity-card[data-activity-id]')
                    return columns.flatMap(column => column.cardsContainer.children.filter(child => child.kind === 'card'));
                if (selector === '.commercial-kanban-column.is-drop-target')
                    return columns.filter(column => column.classList.contains('is-drop-target'));
                return [];
            };
            const document = {
                querySelector: selector => selector === '[data-commercial-kanban]' ? board : selector === '#commercial-kanban-status-form' ? statusForm : null,
                createElement: kind => new Element(kind)
            };
            const window = { matchMedia: () => ({ matches: true }) };
            class FormData { forEach(callback) { callback('antiforgery-token', '__RequestVerificationToken'); } }
            class URLSearchParams {
                constructor() { this.values = {}; }
                append(key, value) { this.values[key] = value; }
                set(key, value) { this.values[key] = value; }
                toString() { return Object.entries(this.values).map(([key, value]) => `${key}=${value}`).join('&'); }
            }
            let resolveFetch;
            const fetch = () => new Promise(resolve => { resolveFetch = resolve; });
            (async () => {
                __SCRIPT__
                const transfer = { value: '', setData(_type, value) { this.value = value; }, getData() { return this.value; } };
                listeners.dragstart({ target: card, dataTransfer: transfer, preventDefault() {} });
                let dragOverPrevented = 0;
                listeners.dragover({ target, dataTransfer: transfer, preventDefault() { dragOverPrevented++; } });
                const targetHighlightedDuringDrag = target.classList.contains('is-drop-target');
                const move = listeners.drop({ target, dataTransfer: transfer, preventDefault() {} });
                let duplicateDragPrevented = 0;
                listeners.dragstart({ target: card, dataTransfer: transfer, preventDefault() { duplicateDragPrevented++; } });
                resolveFetch({ ok: false, json: () => Promise.resolve({ success: false }) });
                await move;
                const rollback = {
                    status: card.dataset.currentStatus,
                    parentStatus: card.closest('[data-kanban-column]').dataset.status,
                    sourceCount: source.count.textContent,
                    targetCount: target.count.textContent,
                    feedback: feedback.textContent,
                    isError: feedback.classList.contains('is-error'),
                    pending: card.dataset.statusUpdatePending ?? null
                };

                listeners.dragstart({ target: card, dataTransfer: transfer, preventDefault() {} });
                listeners.dragover({ target, dataTransfer: transfer, preventDefault() {} });
                const successfulMove = listeners.drop({ target, dataTransfer: transfer, preventDefault() {} });
                resolveFetch({ ok: true, json: () => Promise.resolve({ success: true, status: 1 }) });
                await successfulMove;
                return JSON.stringify({
                    rollback,
                    status: rollback.status,
                    parentStatus: rollback.parentStatus,
                    sourceCount: rollback.sourceCount,
                    targetCount: rollback.targetCount,
                    feedback: rollback.feedback,
                    isError: rollback.isError,
                    successStatus: card.dataset.currentStatus,
                    successParentStatus: card.closest('[data-kanban-column]').dataset.status,
                    successSourceCount: source.count.textContent,
                    successTargetCount: target.count.textContent,
                    successFeedback: feedback.textContent,
                    successIsError: feedback.classList.contains('is-error'),
                    targetHighlightedDuringDrag,
                    dragOverPrevented,
                    duplicateDragPrevented,
                    pending: card.dataset.statusUpdatePending ?? null
                });
            })()
            """;

        var result = await new Engine().EvaluateAsync(harness.Replace("__SCRIPT__", script, StringComparison.Ordinal));
        using var state = JsonDocument.Parse(result.AsString());
        var root = state.RootElement;

        Assert.Equal("0", root.GetProperty("status").GetString());
        Assert.Equal("0", root.GetProperty("parentStatus").GetString());
        Assert.Equal("1", root.GetProperty("sourceCount").GetString());
        Assert.Equal("0", root.GetProperty("targetCount").GetString());
        Assert.Equal("Não foi possível atualizar o atendimento. O card voltou à coluna original.", root.GetProperty("feedback").GetString());
        Assert.True(root.GetProperty("isError").GetBoolean());
        Assert.True(root.GetProperty("targetHighlightedDuringDrag").GetBoolean());
        Assert.Equal(1, root.GetProperty("dragOverPrevented").GetInt32());
        Assert.Equal(1, root.GetProperty("duplicateDragPrevented").GetInt32());
        Assert.Null(root.GetProperty("pending").GetString());
        Assert.Equal("1", root.GetProperty("successStatus").GetString());
        Assert.Equal("1", root.GetProperty("successParentStatus").GetString());
        Assert.Equal("0", root.GetProperty("successSourceCount").GetString());
        Assert.Equal("1", root.GetProperty("successTargetCount").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("successFeedback").GetString()));
        Assert.False(root.GetProperty("successIsError").GetBoolean());
    }
}
