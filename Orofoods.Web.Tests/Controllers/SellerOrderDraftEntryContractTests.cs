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
        Assert.Contains("SellerCatalogService", catalogController, StringComparison.Ordinal);
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

        Assert.Contains("Enviar pedido", view, StringComparison.Ordinal);
        Assert.Contains("id=\"seller-order-submit-form\"", view, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"Submit\"", view, StringComparison.Ordinal);
        Assert.Contains("form=\"seller-order-submit-form\"", view, StringComparison.Ordinal);
        Assert.Contains("form=\"seller-order-submit-form\" disabled aria-disabled=\"true\"", view, StringComparison.Ordinal);
        Assert.Contains("@Html.AntiForgeryToken()", view, StringComparison.Ordinal);
        Assert.Contains("0%", view, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"@formAction\"", view, StringComparison.Ordinal);
        Assert.Contains("name=\"ExpectedVersion\"", view, StringComparison.Ordinal);
        Assert.Contains("name=\"QuoteToken\"", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Entry_styles_include_the_approved_palette_and_mobile_safe_action_bar()
    {
        var css = File.ReadAllText(ProjectFile("Orofoods.Web", "wwwroot", "css", "seller-order-drafts.css"));

        Assert.Contains("#000066", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#F4F7FC", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#FFFFFF", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#E2E8F0", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#2563EB", css, StringComparison.OrdinalIgnoreCase);
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

        Assert.Contains("if (persisted && submitButton && hasQuote)", script, StringComparison.Ordinal);
        Assert.Contains("submitButton.disabled = false", script, StringComparison.Ordinal);
        Assert.Contains("submitButton.disabled = true", script, StringComparison.Ordinal);
        Assert.Contains("submitButton.title = 'Salve as altera", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_product_search_results_clear_previous_pagination()
    {
        var script = File.ReadAllText(ProjectFile("Orofoods.Web", "wwwroot", "js", "seller-order-drafts.js"));
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
