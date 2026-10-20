using System.Text.Json;
using Jint;

namespace Orofoods.Web.Tests.Views;

public sealed class PublicCatalogModernizationTests
{
    private static readonly string WebRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));

    [Fact]
    public void Products_page_uses_the_local_banner_and_approved_catalog_copy_without_a_second_cart_action()
    {
        var view = ReadWebFile("Views", "Home", "Products.cshtml");
        var header = ReadWebFile("Views", "Shared", "_SiteHeader.cshtml");
        var styles = ReadWebFile("wwwroot", "css", "catalog.css");

        Assert.Contains("CATÁLOGO OROFOODS", view, StringComparison.Ordinal);
        Assert.Contains("Produtos para sua opera&ccedil;&atilde;o.", view, StringComparison.Ordinal);

        Assert.Contains("P&atilde;es de alta qualidade para hamburguerias e food service.", view, StringComparison.Ordinal);
        Assert.Contains("catalogo-banner-orofoods.jpg", styles, StringComparison.Ordinal);
        Assert.Contains("catalog-header-cart", header, StringComparison.Ordinal);
        Assert.Contains("action is \"Index\" or \"Products\"", header, StringComparison.Ordinal);
        Assert.DoesNotContain("public-catalog-cart-row", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Quick_filters_include_tradicional_and_public_catalog_has_a_five_column_desktop_grid()
    {
        var view = ReadWebFile("Views", "Home", "Products.cshtml");
        var card = ReadWebFile("Views", "Shared", "_PublicProductCard.cshtml");
        var styles = ReadWebFile("wwwroot", "css", "catalog.css");

        Assert.Contains("data-catalog-filter=\"tradicional\"", view, StringComparison.Ordinal);
        Assert.Contains(">Tradicional</button>", view, StringComparison.Ordinal);
        Assert.DoesNotContain("data-catalog-filter=\"congelados\">Congelados", view, StringComparison.Ordinal);
        Assert.Contains("quickFilterTags.Add(\"tradicional\")", card, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: repeat(5, minmax(0, 1fr))", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 575.98px)", styles, StringComparison.Ordinal);
        Assert.Contains("#F4F7FC", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Public_products_banner_uses_compact_desktop_and_mobile_layout()
    {
        var styles = ReadWebFile("wwwroot", "css", "catalog.css");

        Assert.Contains("body.catalog-modern-page .public-products-hero {", styles, StringComparison.Ordinal);
        Assert.Contains("min-height: 220px;", styles, StringComparison.Ordinal);
        Assert.Contains("font-size: 42px;", styles, StringComparison.Ordinal);
        Assert.Contains("font-size: 16px;", styles, StringComparison.Ordinal);
        Assert.Contains("body.catalog-modern-page .public-products-eyebrow::after", styles, StringComparison.Ordinal);
        Assert.Contains("body.catalog-modern-page .public-products-hero + .public-products-section", styles, StringComparison.Ordinal);
        Assert.Contains("font-size: clamp(32px, 6.5vw, 38px);", styles, StringComparison.Ordinal);
        Assert.Contains("font-size: clamp(30px, 8vw, 34px);", styles, StringComparison.Ordinal);
        Assert.Contains("background-position: 70% center !important;", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_card_keeps_add_label_and_confirms_only_successful_additions()
    {
        var card = ReadWebFile("Views", "Shared", "_PublicProductCard.cshtml");
        var script = ReadWebFile("wwwroot", "js", "catalog.js");
        var styles = ReadWebFile("wwwroot", "css", "catalog.css");

        Assert.DoesNotContain("data-catalog-allow-quantity-update", card, StringComparison.Ordinal);
        Assert.Contains("type=\"submit\">Adicionar</button>", card, StringComparison.Ordinal);
        Assert.Contains("value=\"@product.MinimumSalesQuantity\"", card, StringComparison.Ordinal);
        Assert.Contains("payload.message", script, StringComparison.Ordinal);
        Assert.Contains("Produto adicionado ao carrinho.", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Quantidade do carrinho atualizada.", script, StringComparison.Ordinal);
        Assert.Contains("catalog-quantity-button", styles, StringComparison.Ordinal);
        Assert.Contains("width: 36px", styles, StringComparison.Ordinal);
        Assert.Contains("height: 36px", styles, StringComparison.Ordinal);
        Assert.Contains("gap: 6px", styles, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Catalog_stepper_changes_selection_and_successful_add_updates_header()
    {
        var script = ReadWebFile("wwwroot", "js", "catalog.js");
        const string harness = """
            var listeners = {};
            var input = { min: "1", max: "", value: "2", disabled: false, addEventListener(name, callback) { listeners["input:" + name] = callback; }, dispatchEvent() {} };
            var decrease = { disabled: false, addEventListener(name, callback) { listeners["decrease:" + name] = callback; } };
            var increase = { disabled: false, addEventListener(name, callback) { listeners["increase:" + name] = callback; } };
            var stepper = { querySelector(selector) { return selector === 'input[name="quantity"]' ? input : selector.includes('decrease') ? decrease : increase; } };
            var button = { disabled: false, innerHTML: "Adicionar", attributes: {}, setAttribute(name, value) { this.attributes[name] = value; }, removeAttribute(name) { delete this.attributes[name]; } };
            var status = { textContent: "" };
            var submit;
            var form = { action: "/Portal/AddToCart", dataset: {}, addEventListener(name, callback) { if (name === "submit") submit = callback; }, querySelector(selector) { return selector === 'button[type="submit"]' ? button : selector === 'input[name="quantity"]' ? input : status; } };
            var count = { textContent: "2" };
            var cart = { querySelector() { return count; }, setAttribute(name, value) { this[name] = value; } };
            globalThis.document = { querySelectorAll(selector) { return selector.includes("stepper") ? [stepper] : [form]; }, querySelector() { return cart; } };
            globalThis.window = {};
            globalThis.Event = function() {};
            globalThis.FormData = function() {};
            globalThis.fetch = async () => ({ ok: true, json: async () => ({ quantity: 3, cartQuantity: 3 }) });
            __SCRIPT__
            (async () => {
                listeners["increase:click"]();
                const afterIncrease = input.value;
                listeners["decrease:click"]();
                const afterDecrease = input.value;
                const beforeSubmitStatus = status.textContent;
                await submit({ preventDefault() {} });
                return JSON.stringify({ afterIncrease, afterDecrease, beforeSubmitStatus, label: button.innerHTML, selected: input.value, controlsEnabled: !input.disabled && !increase.disabled, buttonEnabled: !button.disabled, status: status.textContent, count: count.textContent, cartLabel: cart["aria-label"] });
            })();
            """;

        var result = await new Engine().EvaluateAsync(harness.Replace("__SCRIPT__", script, StringComparison.Ordinal));
        using var state = JsonDocument.Parse(result.AsString());
        var root = state.RootElement;

        Assert.Equal("3", root.GetProperty("afterIncrease").ToString());
        Assert.Equal("2", root.GetProperty("afterDecrease").ToString());
        Assert.Equal("", root.GetProperty("beforeSubmitStatus").GetString());
        Assert.Equal("Adicionar", root.GetProperty("label").GetString());
        Assert.Equal("2", root.GetProperty("selected").ToString());
        Assert.True(root.GetProperty("controlsEnabled").GetBoolean());
        Assert.True(root.GetProperty("buttonEnabled").GetBoolean());
        Assert.Equal("Produto adicionado ao carrinho.", root.GetProperty("status").GetString());
        Assert.Equal("3", root.GetProperty("count").ToString());
        Assert.Equal("Carrinho com 3 unidades", root.GetProperty("cartLabel").GetString());
    }

    private static string ReadWebFile(params string[] segments) => File.ReadAllText(Path.Combine([WebRoot, .. segments]));
}
