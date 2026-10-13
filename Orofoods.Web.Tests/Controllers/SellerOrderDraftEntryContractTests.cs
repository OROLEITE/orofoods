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
        Assert.DoesNotContain(postActions, method => method.Name.Contains("Submit", StringComparison.OrdinalIgnoreCase)
            || method.Name.Contains("Send", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Entry_view_keeps_discount_locked_and_stage_three_send_disabled()
    {
        var view = File.ReadAllText(ProjectFile("Orofoods.Web", "Areas", "Vendedor", "Views", "OrderDrafts", "Editor.cshtml"));

        Assert.Contains("Enviar pedido", view, StringComparison.Ordinal);
        Assert.Contains("disabled", view, StringComparison.Ordinal);
        Assert.Contains("0%", view, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"@formAction\"", view, StringComparison.Ordinal);
        Assert.Contains("name=\"ExpectedVersion\"", view, StringComparison.Ordinal);
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
        Assert.Contains("#059669", css, StringComparison.OrdinalIgnoreCase);
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
