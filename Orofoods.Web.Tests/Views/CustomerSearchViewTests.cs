using System.Text.Json;
using Jint;

namespace Orofoods.Web.Tests.Views;

public sealed class CustomerSearchViewTests
{
    private static string ProjectPath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));

    [Fact]
    public void Wallet_and_draft_customer_selectors_share_the_authorized_search_contract()
    {
        var wallet = File.ReadAllText(Path.Combine(ProjectPath, "Areas", "Vendedor", "Views", "Customers", "Index.cshtml"));
        var editor = File.ReadAllText(Path.Combine(ProjectPath, "Areas", "Vendedor", "Views", "OrderDrafts", "Editor.cshtml"));

        Assert.Contains("data-customer-search", wallet);
        Assert.Contains("data-search-url=\"/Vendedor/Clientes/Buscar\"", wallet);
        Assert.Contains("data-search-mode=\"cards\"", wallet);
        Assert.Contains("data-customer-search-results", wallet);
        Assert.Contains("role=\"list\"", wallet);
        Assert.Contains("~/js/customer-search.js", wallet);
        Assert.Contains("~/css/customer-search.css", wallet);
        Assert.Contains("data-customer-search", editor);
        Assert.Contains("data-search-url=\"/Vendedor/Rascunhos/Clientes\"", editor);
        Assert.Contains("data-search-mode=\"links\"", editor);
        Assert.Contains("data-customer-search-status", editor);
        Assert.Contains("role=\"list\"", editor);
        Assert.Contains("~/js/customer-search.js", editor);
        Assert.Contains("~/css/customer-search.css", editor);
    }

    [Fact]
    public void Shared_customer_search_is_debounced_cancellable_and_builds_safe_keyboard_accessible_results()
    {
        var scriptPath = Path.Combine(ProjectPath, "wwwroot", "js", "customer-search.js");
        Assert.True(File.Exists(scriptPath), "The shared customer-search script is required by all three selectors.");
        var script = File.ReadAllText(scriptPath);

        Assert.Contains("300", script);
        Assert.Contains("scheduledQuery", script);
        Assert.Contains("AbortController", script);
        Assert.Contains("thisRequest !== requestNumber", script);
        Assert.Contains("textContent", script);
        Assert.Contains("aria-expanded", script);
        Assert.DoesNotContain("innerHTML", script);
        Assert.Contains("query.length < 1", script);
        Assert.Contains("aria-selected", script);
        Assert.Contains("ArrowDown", script);
        Assert.Contains("Escape", script);
        Assert.Contains("data-customer-search-submit", script);
    }

    [Fact]
    public async Task First_typed_character_schedules_authorized_customer_search()
    {
        var scriptPath = Path.Combine(ProjectPath, "wwwroot", "js", "customer-search.js");
        var script = File.ReadAllText(scriptPath);
        const string harness = """
            var inputListeners = {};
            var scheduled = [];
            var requests = [];
            var input = { value: "", setAttribute() {}, addEventListener(name, callback) { inputListeners[name] = callback; }, focus() {} };
            var status = { textContent: "" };
            var results = {
                children: [], setAttribute() {}, replaceChildren() { this.children = []; }, append(item) { this.children.push(item); },
                querySelector() { return this.children[0] || null; }, querySelectorAll() { return this.children; }, addEventListener() {}
            };
            var root = {
                dataset: { searchMode: "select", searchUrl: "/Admin/Portais/Clientes" },
                querySelector(selector) {
                    return selector === "[data-customer-search-input]" ? input
                        : selector === "[data-customer-search-results]" ? results
                        : selector === "[data-customer-search-status]" ? status : null;
                },
                matches(selector) { return selector === "form"; }
            };
            globalThis.document = { querySelectorAll() { return [root]; } };
            globalThis.window = {
                location: { origin: "https://orofoods.test" },
                setTimeout(callback, delay) { scheduled.push({ callback, delay }); return scheduled.length; },
                clearTimeout() {}
            };
            globalThis.URL = function (path, origin) {
                this.searchParams = { set(name, value) { requests.push({ path, origin, name, value }); } };
            };
            globalThis.AbortController = function () {
                this.signal = { aborted: false };
                this.abort = () => { this.signal.aborted = true; };
            };
            globalThis.fetch = async () => ({ ok: true, json: async () => [] });
            __SCRIPT__
            (async () => {
                input.value = "M";
                inputListeners.input();
                const debounce = scheduled[0].delay;
                scheduled[0].callback();
                for (let index = 0; index < 5; index++) await Promise.resolve();
                return JSON.stringify({ debounce, requests, status: status.textContent });
            })();
            """;

        var result = await new Engine().EvaluateAsync(harness.Replace("__SCRIPT__", script, StringComparison.Ordinal));
        using var diagnostic = JsonDocument.Parse(result.AsString());
        var state = diagnostic.RootElement;

        Assert.Equal(300, state.GetProperty("debounce").GetInt32());
        var request = Assert.Single(state.GetProperty("requests").EnumerateArray());
        Assert.Equal("/Admin/Portais/Clientes", request.GetProperty("path").GetString());
        Assert.Equal("q", request.GetProperty("name").GetString());
        Assert.Equal("M", request.GetProperty("value").GetString());
        Assert.Equal("Nenhum cliente autorizado encontrado.", state.GetProperty("status").GetString());
    }
}
