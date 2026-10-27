using System.Text.Json;
using Jint;

namespace Orofoods.Web.Tests.Views;

public class AdminOrdersNewOrderViewTests
{
    [Fact]
    public void Orders_header_opens_order_entry_directly()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var ordersView = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Orders", "Index.cshtml"));

        Assert.Contains("Novo pedido", ordersView);
        Assert.Contains("asp-controller=\"Customers\"", ordersView);
        Assert.Contains("asp-action=\"NewOrder\"", ordersView);
        Assert.DoesNotContain("asp-route-context=\"new-order\"", ordersView);
        Assert.Contains("class=\"btn admin-primary-action", ordersView);
    }

    [Fact]
    public void Customer_list_exposes_assisted_order_only_for_approved_active_customers()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var customersView = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Customers", "Index.cshtml"));

        Assert.Contains("customer.IsActive && customer.Status == CustomerStatus.Approved", customersView);
        Assert.Contains("asp-action=\"NewOrder\"", customersView);
        Assert.Contains("asp-route-id=\"@customer.Id\"", customersView);
        Assert.Contains("class=\"btn btn-sm admin-primary-action\"", customersView);
        Assert.Contains("class=\"btn btn-sm btn-gold\"", customersView);
        Assert.Contains("table table-sm customer-list-table", customersView);
        Assert.Contains("btn-gold", customersView);
        Assert.Contains("Clientes B2B", customersView);
        Assert.DoesNotContain("Selecionar cliente para novo pedido", customersView);
    }

    [Fact]
    public void Assisted_order_starts_with_customer_search_and_focuses_product_lookup_after_selection()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Customers", "NewOrder.cshtml"));
        var script = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "js", "assisted-order-customer-search.js"));

        Assert.Contains("data-assisted-customer-search", view);
        Assert.Contains("data-search-url", view);
        Assert.Contains("data-order-url-template", view);
        Assert.Contains("id=\"assisted-customer-query\"", view);
        Assert.Contains("id=\"assisted-product-search\"", view);
        Assert.Contains("data-focus-product-search", view);
        Assert.Contains("productSearch.focus()", script);
        Assert.Contains("assisted-order-customer-search.js", view);
    }

    [Fact]
    public async Task Enter_and_mouse_selection_open_the_selected_customer_order_entry()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var script = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "js", "assisted-order-customer-search.js"));
        const string harness = """
            var inputListeners = {};
            var resultListeners = {};
            var scheduled = [];
            var requests = [];
            var navigated = [];
            const makeElement = kind => ({
                kind, dataset: {}, children: [], attributes: {}, textContent: "", hidden: false,
                setAttribute(name, value) { this.attributes[name] = value; },
                removeAttribute(name) { delete this.attributes[name]; },
                addEventListener(name, callback) { (this.listeners ??= {})[name] = callback; },
                append(element) { this.children.push(element); },
                replaceChildren() { this.children = []; },
                contains(element) { return this.children.includes(element); },
                querySelectorAll() { return this.children; },
                querySelector() { return null; },
                closest(selector) { return selector === "[data-customer-id]" ? this : null; },
                focus() { this.focused = true; }
            });
            const input = makeElement("input");
            const productSearch = makeElement("product-search");
            const results = makeElement("results");
            const status = makeElement("status");
            const more = makeElement("more");
            const root = {
                dataset: { searchUrl: "/Admin/Customers/SearchOrderCustomers", orderUrlTemplate: "/Admin/Customers/NewOrder?id=__id__", focusProductSearch: "true" },
                querySelector(selector) {
                    return selector === "[data-assisted-customer-query]" ? input
                        : selector === "[data-assisted-customer-results]" ? results
                        : selector === "[data-assisted-customer-status]" ? status
                        : selector === "[data-assisted-customer-more]" ? more : null;
                }
            };
            globalThis.document = {
                querySelector(selector) {
                    return selector === "[data-assisted-customer-search]" ? root
                        : selector === "#assisted-product-search" ? productSearch : null;
                },
                createElement: makeElement
            };
            globalThis.window = {
                location: { origin: "https://orofoods.test", assign(url) { navigated.push(url); } },
                setTimeout(callback, delay) { scheduled.push({ callback, delay }); return scheduled.length; },
                clearTimeout() {}
            };
            globalThis.URL = function(path, origin) {
                this.path = path;
                this.origin = origin;
                this.searchParams = { set(name, value) { requests.push({ path, origin, name, value }); } };
            };
            globalThis.AbortController = function() { this.signal = { aborted: false }; this.abort = () => { this.signal.aborted = true; }; };
            globalThis.fetch = async () => ({
                ok: true,
                json: async () => ({ items: [{ id: 42, tradeName: "Stág Alimentos", legalName: "Stág Ltda", cnpj: "12.345", wmcCode: "WMC-42" }], hasMore: false, page: 1 })
            });
            __SCRIPT__
            (async () => {
                input.value = "stag";
                input.listeners.input();
                scheduled[0].callback();
                for (let index = 0; index < 8; index++) await Promise.resolve();
                const keyboardEvent = { key: "Enter", preventDefault() { this.prevented = true; } };
                input.listeners.keydown(keyboardEvent);
                const keyboardUrl = navigated[0];
                navigated.length = 0;
                results.listeners.click({ target: results.children[0] });
                return JSON.stringify({ keyboardUrl, mouseUrl: navigated[0], keyboardPrevented: keyboardEvent.prevented, productFocused: productSearch.focused, requests });
            })();
            """;

        var result = await new Engine().EvaluateAsync(harness.Replace("__SCRIPT__", script, StringComparison.Ordinal));
        using var state = JsonDocument.Parse(result.AsString());
        var root = state.RootElement;

        Assert.Equal("/Admin/Customers/NewOrder?id=42", root.GetProperty("keyboardUrl").GetString());
        Assert.Equal("/Admin/Customers/NewOrder?id=42", root.GetProperty("mouseUrl").GetString());
        Assert.True(root.GetProperty("keyboardPrevented").GetBoolean());
        Assert.True(root.GetProperty("productFocused").GetBoolean());
        Assert.Equal("stag", root.GetProperty("requests")[0].GetProperty("value").GetString());
    }

    [Fact]
    public void Assisted_order_preserves_posted_lines_when_the_backend_returns_a_validation_error()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Customers", "NewOrder.cshtml"));
        var script = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "js", "assisted-order.js"));

        Assert.Contains("name=\"ProductIds\"", view, StringComparison.Ordinal);
        Assert.Contains("name=\"Quantities\"", view, StringComparison.Ordinal);
        Assert.Contains("form.querySelectorAll('input[name=\"ProductIds\"]')", script, StringComparison.Ordinal);
        Assert.Contains("lines.set(id", script, StringComparison.Ordinal);
    }
}
