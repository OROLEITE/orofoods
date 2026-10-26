using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Areas.Vendedor.Models;
using Orofoods.Web.Authorization;
using Orofoods.Web.Areas.Vendedor.Controllers;
using Xunit;

namespace Orofoods.Web.Tests.Controllers;

public sealed class SellerOrderDraftEntryContractTests
{
    [Fact]
    public void Bimbo_product_access_routes_sellers_to_order_entry_with_the_selected_customer_and_product()
    {
        var catalogView = File.ReadAllText(ProjectFile("Orofoods.Web", "Areas", "Vendedor", "Views", "Catalog", "Index.cshtml"));
        var productView = File.ReadAllText(ProjectFile("Orofoods.Web", "Views", "Home", "Product.cshtml"));
        var catalogController = File.ReadAllText(ProjectFile("Orofoods.Web", "Areas", "Vendedor", "Controllers", "CatalogController.cs"));
        var orderDraftController = File.ReadAllText(ProjectFile("Orofoods.Web", "Areas", "Vendedor", "Controllers", "OrderDraftsController.cs"));

        Assert.Contains("asp-controller=\"OrderDrafts\" asp-action=\"New\"", catalogView, StringComparison.Ordinal);
        Assert.Contains("asp-route-customerId=\"@Model.CustomerId\"", catalogView, StringComparison.Ordinal);
        Assert.Contains("asp-route-productId=\"@product.Id\"", catalogView, StringComparison.Ordinal);
        Assert.DoesNotContain("asp-controller=\"Cart\" asp-action=\"Add\"", catalogView, StringComparison.Ordinal);
        Assert.DoesNotContain("asp-area=\"Vendedor\" asp-controller=\"Cart\"", productView, StringComparison.Ordinal);
        Assert.Contains("asp-controller=\"OrderDrafts\" asp-action=\"New\"", productView, StringComparison.Ordinal);
        Assert.Contains("asp-route-customerId=\"@selectedCustomerId\"", productView, StringComparison.Ordinal);
        Assert.Contains("asp-route-productId=\"@Model.Id\"", productView, StringComparison.Ordinal);
        Assert.Contains("RedirectToAction(\"New\", \"OrderDrafts\"", catalogController, StringComparison.Ordinal);
        Assert.Contains("int? productId", orderDraftController, StringComparison.Ordinal);
        Assert.Contains("GetSelectableProductsByIdsAsync", orderDraftController, StringComparison.Ordinal);
        Assert.Contains("Quantity = selectedProduct.MinimumCases", orderDraftController, StringComparison.Ordinal);
        Assert.Contains(": postedItems ?? []", orderDraftController, StringComparison.Ordinal);
    }

    [Fact]
    public void Draft_entry_controller_requires_seller_policy_and_antiforgery_on_every_post()
    {
        var controllerType = typeof(CustomersController).Assembly
            .GetType("Orofoods.Web.Areas.Vendedor.Controllers.OrderDraftsController");

        Assert.NotNull(controllerType);
        var authorization = controllerType!.GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal(OrofoodsPolicies.LinkedSalesRepresentative, authorization?.Policy);

        var postActions = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.GetCustomAttribute<HttpPostAttribute>() is not null)
            .ToArray();
        Assert.NotEmpty(postActions);
        Assert.All(postActions, method => Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>()));
        var submit = Assert.Single(postActions, method => method.Name == "Submit");
        Assert.Contains("/Vendedor/Rascunhos/{id:int}/Enviar", submit.GetCustomAttribute<HttpPostAttribute>()!.Template);
    }

    [Fact]
    public void Entry_view_posts_submission_with_antiforgery_and_only_saved_drafts_can_send()
    {
        var view = File.ReadAllText(ProjectFile("Orofoods.Web", "Areas", "Vendedor", "Views", "OrderDrafts", "Editor.cshtml"));

        Assert.Contains("Gravar pedido", view, StringComparison.Ordinal);
        Assert.Contains("Confirmar gravação", view, StringComparison.Ordinal);
        Assert.Contains("id=\"seller-order-submit-form\"", view, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"Submit\"", view, StringComparison.Ordinal);
        Assert.Contains("form=\"seller-order-submit-form\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"seller-order-review-dialog\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"confirm-seller-order-button\"", view, StringComparison.Ordinal);
        var reviewButtonStart = view.IndexOf("id=\"submit-seller-order-button\"", StringComparison.Ordinal);
        var reviewButtonEnd = view.IndexOf("</button>", reviewButtonStart, StringComparison.Ordinal);
        var reviewButton = view[reviewButtonStart..reviewButtonEnd];
        Assert.Contains("type=\"button\"", reviewButton, StringComparison.Ordinal);
        Assert.DoesNotContain("form=\"seller-order-submit-form\"", reviewButton, StringComparison.Ordinal);
        Assert.Contains("@Html.AntiForgeryToken()", view, StringComparison.Ordinal);
        Assert.Contains("0%", view, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"@formAction\"", view, StringComparison.Ordinal);
        Assert.Contains("name=\"ExpectedVersion\"", view, StringComparison.Ordinal);
        Assert.Contains("name=\"QuoteToken\"", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Product_search_contract_exposes_commercial_identification_price_unit_and_informational_stock()
    {
        var script = File.ReadAllText(ProjectFile("Orofoods.Web", "wwwroot", "js", "seller-order-drafts.js"));
        var controller = File.ReadAllText(ProjectFile("Orofoods.Web", "Areas", "Vendedor", "Controllers", "OrderDraftsController.cs"));
        var fields = typeof(Orofoods.Web.Services.Sellers.SellerCatalogProductRow)
            .GetProperties().Select(property => property.Name).ToArray();

        Assert.Contains("Brand", fields);
        Assert.Contains("WmcCode", fields);
        Assert.Contains("CommercialUnitCode", fields);
        Assert.Contains("PriceUnitLabel", fields);
        Assert.Contains("AvailableQuantity", fields);
        Assert.Contains("product.Brand", controller, StringComparison.Ordinal);
        Assert.Contains("product.WmcCode", controller, StringComparison.Ordinal);
        Assert.Contains("product.CommercialUnitCode", controller, StringComparison.Ordinal);
        Assert.Contains("product.PriceUnitLabel", controller, StringComparison.Ordinal);
        Assert.Contains("product.AvailableQuantity", controller, StringComparison.Ordinal);
        Assert.Contains("Saldo disponível", script, StringComparison.Ordinal);
        Assert.Contains("Estoque informativo", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Product_search_debounces_for_300ms_and_duplicate_wmc_codes_do_not_create_a_second_line()
    {
        var script = File.ReadAllText(ProjectFile("Orofoods.Web", "wwwroot", "js", "seller-order-drafts.js"));
        var controller = File.ReadAllText(ProjectFile("Orofoods.Web", "Areas", "Vendedor", "Controllers", "OrderDraftsController.cs"));
        var existingStart = script.IndexOf("if (existing)", StringComparison.Ordinal);
        var existingEnd = script.IndexOf("return;", existingStart, StringComparison.Ordinal);
        var repeatedProductBlock = script[existingStart..existingEnd];

        Assert.Contains("window.setTimeout(() => searchProducts(1), 300)", script, StringComparison.Ordinal);
        Assert.Contains("Este item já existe no pedido!", repeatedProductBlock, StringComparison.Ordinal);
        Assert.Contains("is-duplicate", repeatedProductBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("quantity.value = String(Math.max(minimum, currentQuantity) + 1)", repeatedProductBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void Saved_order_requires_a_review_with_customer_address_payment_items_prices_and_total()
    {
        var view = File.ReadAllText(ProjectFile("Orofoods.Web", "Areas", "Vendedor", "Views", "OrderDrafts", "Editor.cshtml"));
        var controller = File.ReadAllText(ProjectFile("Orofoods.Web", "Areas", "Vendedor", "Controllers", "OrderDraftsController.cs"));
        var script = File.ReadAllText(ProjectFile("Orofoods.Web", "wwwroot", "js", "seller-order-drafts.js"));

        Assert.Contains("Gravar pedido", view, StringComparison.Ordinal);
        Assert.Contains("Revisão do pedido", view, StringComparison.Ordinal);
        Assert.Contains("Cliente", view, StringComparison.Ordinal);
        Assert.Contains("Endereço", view, StringComparison.Ordinal);
        Assert.Contains("Pagamento", view, StringComparison.Ordinal);
        Assert.Contains("@foreach (var item in Model.Items.Where(item => !item.RemovalPending))", view, StringComparison.Ordinal);
        Assert.Contains("@item.UnitPrice", view, StringComparison.Ordinal);
        Assert.Contains("@Model.Total", view, StringComparison.Ordinal);
        Assert.Contains("form=\"seller-order-submit-form\"", view, StringComparison.Ordinal);
        Assert.Contains("reviewDialog.showModal()", script, StringComparison.Ordinal);
        Assert.Contains("isSubmitting", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Mobile_action_bar_exposes_the_total_and_a_44px_product_search_shortcut()
    {
        var view = File.ReadAllText(ProjectFile("Orofoods.Web", "Areas", "Vendedor", "Views", "OrderDrafts", "Editor.cshtml"));
        var controller = File.ReadAllText(ProjectFile("Orofoods.Web", "Areas", "Vendedor", "Controllers", "OrderDraftsController.cs"));
        var script = File.ReadAllText(ProjectFile("Orofoods.Web", "wwwroot", "js", "seller-order-drafts.js"));
        var css = File.ReadAllText(ProjectFile("Orofoods.Web", "wwwroot", "css", "seller-order-drafts.css"));

        Assert.Contains("data-draft-total", view, StringComparison.Ordinal);
        Assert.Contains("data-focus-product-search", view, StringComparison.Ordinal);
        Assert.Contains("querySelectorAll('[data-draft-total]')", script, StringComparison.Ordinal);
        Assert.Contains("focusProductSearch", script, StringComparison.Ordinal);
        Assert.Contains(".seller-order-drafts__mobile-search", css, StringComparison.Ordinal);
        Assert.Contains("min-height:44px", css, StringComparison.Ordinal);
        Assert.DoesNotContain("tabela vigente", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tabela vigente", controller, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("revalida preÃƒÂ§o e estoque", view, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Entry_styles_include_the_approved_palette_and_mobile_safe_action_bar()
    {
        var css = File.ReadAllText(ProjectFile("Orofoods.Web", "wwwroot", "css", "seller-order-drafts.css"));

        Assert.Contains("#000066", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#F4F7FC", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#FFFFFF", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#E2E8F0", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#0B132B", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#111827", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#374151", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#E5E7EB", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#94A3B8", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#38BDF8", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#0E7490", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#DC2626", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@media", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("env(safe-area-inset-bottom)", css, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Mobile_editor_header_wraps_long_customer_names_inside_the_viewport()
    {
        var css = File.ReadAllText(ProjectFile("Orofoods.Web", "wwwroot", "css", "seller-order-drafts.css"));
        var mobileStylesStart = css.IndexOf("@media (max-width: 767.98px)", StringComparison.Ordinal);
        var mobileStyles = mobileStylesStart >= 0 ? css[mobileStylesStart..] : "";

        Assert.Contains(".seller-order-drafts__header--editor > div", mobileStyles, StringComparison.Ordinal);
        Assert.Contains("min-width:0", mobileStyles, StringComparison.Ordinal);
        Assert.Contains("overflow-wrap:anywhere", mobileStyles, StringComparison.Ordinal);
    }

    [Fact]
    public void Entry_script_hides_sticky_actions_while_the_mobile_keyboard_is_open()
    {
        var script = File.ReadAllText(ProjectFile("Orofoods.Web", "wwwroot", "js", "seller-order-drafts.js"));
        var controller = File.ReadAllText(ProjectFile("Orofoods.Web", "Areas", "Vendedor", "Controllers", "OrderDraftsController.cs"));

        Assert.Contains("visualViewport", script, StringComparison.Ordinal);
        Assert.Contains("is-keyboard-open", script, StringComparison.Ordinal);
        Assert.Contains("RemovedProductIds", script, StringComparison.Ordinal);
        var view = File.ReadAllText(ProjectFile("Orofoods.Web", "Areas", "Vendedor", "Views", "OrderDrafts", "Editor.cshtml"));
        Assert.Contains("name=\"ExpectedVersion\"", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Entry_script_enables_send_only_after_initialization_and_disables_it_for_dirty_drafts()
    {
        var script = File.ReadAllText(ProjectFile("Orofoods.Web", "wwwroot", "js", "seller-order-drafts.js"));
        var controller = File.ReadAllText(ProjectFile("Orofoods.Web", "Areas", "Vendedor", "Controllers", "OrderDraftsController.cs"));

        Assert.Contains("if (persisted && submitButton && hasQuote)", script, StringComparison.Ordinal);
        Assert.Contains("submitButton.disabled = false", script, StringComparison.Ordinal);
        Assert.Contains("submitButton.disabled = true", script, StringComparison.Ordinal);
        Assert.Contains("submitButton.title = 'Salve as altera", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_product_search_results_clear_previous_pagination()
    {
        var script = File.ReadAllText(ProjectFile("Orofoods.Web", "wwwroot", "js", "seller-order-drafts.js"));
        var controller = File.ReadAllText(ProjectFile("Orofoods.Web", "Areas", "Vendedor", "Controllers", "OrderDraftsController.cs"));
        var renderProducts = script.IndexOf("function renderProducts(payload)", StringComparison.Ordinal);
        var emptyResults = script.IndexOf("if (products.length === 0)", renderProducts, StringComparison.Ordinal);
        var clearPagination = script.IndexOf("paginationHost.replaceChildren()", renderProducts, StringComparison.Ordinal);
        var hidePagination = script.IndexOf("paginationHost.hidden = true", renderProducts, StringComparison.Ordinal);

        Assert.True(renderProducts >= 0);
        Assert.True(emptyResults > renderProducts);
        Assert.True(clearPagination > renderProducts && clearPagination < emptyResults);
        Assert.True(hidePagination > renderProducts && hidePagination < emptyResults);
    }

    [Fact]
    public void Entry_view_displays_field_validation_errors()
    {
        var view = File.ReadAllText(ProjectFile("Orofoods.Web", "Areas", "Vendedor", "Views", "OrderDrafts", "Editor.cshtml"));

        Assert.Contains("asp-validation-summary=\"All\"", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Entry_view_and_script_support_wmc_code_entry_enter_visual_next_line_and_exact_duplicate_message()
    {
        var view = File.ReadAllText(ProjectFile("Orofoods.Web", "Areas", "Vendedor", "Views", "OrderDrafts", "Editor.cshtml"));
        var script = File.ReadAllText(ProjectFile("Orofoods.Web", "wwwroot", "js", "seller-order-drafts.js"));
        var controller = File.ReadAllText(ProjectFile("Orofoods.Web", "Areas", "Vendedor", "Controllers", "OrderDraftsController.cs"));

        Assert.Contains("código WMC", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-code", view, StringComparison.Ordinal);
        Assert.Contains("id=\"draft-entry-line\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"next-wmc-code\"", view, StringComparison.Ordinal);
        Assert.Contains("product.wmcCode", script, StringComparison.Ordinal);
        Assert.Contains("event.key === 'Enter'", script, StringComparison.Ordinal);
        Assert.Contains("Este item já existe no pedido!", script, StringComparison.Ordinal);
        Assert.Contains("data-wmc-code", script, StringComparison.Ordinal);
        Assert.Contains("entryLine.hidden = rows.length === 0", script, StringComparison.Ordinal);
        Assert.Contains("nextCodeInput.focus()", script, StringComparison.Ordinal);
        Assert.Contains("PaymentTerms = paymentTerms", controller, StringComparison.Ordinal);
        Assert.Contains("usePostedInput: true", controller, StringComparison.Ordinal);
        Assert.Contains("ModelState.AddModelError", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void Seller_entry_hides_delivery_date_but_keeps_the_legacy_field_in_its_form_contract()
    {
        var view = File.ReadAllText(ProjectFile("Orofoods.Web", "Areas", "Vendedor", "Views", "OrderDrafts", "Editor.cshtml"));

        Assert.DoesNotContain("name=\"RequestedDeliveryDate\"", view, StringComparison.Ordinal);
        Assert.Contains(nameof(SellerOrderDraftSaveForm.RequestedDeliveryDate), typeof(SellerOrderDraftSaveForm).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public void Entry_form_does_not_accept_prices_or_discount_values_and_does_not_use_cart_or_checkout_services()
    {
        var controllerType = typeof(CustomersController).Assembly
            .GetType("Orofoods.Web.Areas.Vendedor.Controllers.OrderDraftsController")!;
        var constructor = Assert.Single(controllerType.GetConstructors());
        Assert.DoesNotContain(constructor.GetParameters(), parameter =>
            parameter.ParameterType.Name.Contains("CartService", StringComparison.Ordinal)
            || parameter.ParameterType.Name.Contains("CheckoutService", StringComparison.Ordinal));

        var formFields = typeof(SellerOrderDraftFormItem).GetProperties().Select(property => property.Name).ToArray();
        Assert.Contains("ProductId", formFields);
        Assert.Contains("Quantity", formFields);
        Assert.DoesNotContain("UnitPrice", formFields);
        Assert.DoesNotContain("ResolvedUnitPrice", formFields);
        Assert.DoesNotContain("RequestedDiscountAmount", formFields);
    }

    private static string ProjectFile(params string[] segments)
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../", Path.Combine(segments)));
        return path;
    }
}
