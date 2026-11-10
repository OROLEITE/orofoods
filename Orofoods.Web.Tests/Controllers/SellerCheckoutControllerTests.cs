using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Areas.Vendedor.Controllers;
using Orofoods.Web.Authorization;
using Orofoods.Web.ViewModels;
using System.Reflection;

namespace Orofoods.Web.Tests.Controllers;

public sealed class SellerCheckoutControllerTests
{
    [Fact]
    public void Checkout_controller_requires_linked_seller_policy()
    {
        Assert.Contains(typeof(CheckoutController).GetCustomAttributes<AuthorizeAttribute>(),
            attribute => attribute.Policy == OrofoodsPolicies.LinkedSalesRepresentative);
    }

    [Fact]
    public void Checkout_post_uses_antiforgery()
    {
        var method = Assert.Single(typeof(CheckoutController).GetMethods(),
            candidate => candidate.Name == "Index" && candidate.GetCustomAttributes<HttpPostAttribute>().Any());
        Assert.NotEmpty(method.GetCustomAttributes<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public async Task Legacy_checkout_get_and_post_redirect_to_draft_entry_without_placing_an_order()
    {
        const int customerId = 21;
        var controller = new CheckoutController();

        var getResult = Assert.IsType<RedirectToActionResult>(await controller.Index(customerId, CancellationToken.None));
        var postResult = Assert.IsType<RedirectToActionResult>(await controller.Index(
            customerId, new SellerCheckoutViewModel { CustomerId = customerId }, CancellationToken.None));

        foreach (var redirect in new[] { getResult, postResult })
        {
            Assert.Equal("New", redirect.ActionName);
            Assert.Equal("OrderDrafts", redirect.ControllerName);
            Assert.Equal(customerId, redirect.RouteValues!["customerId"]);
        }
    }

    [Fact]
    public void Seller_checkout_view_has_no_address_field_or_AddressId_binding()
    {
        var view = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Orofoods.Web", "Areas", "Vendedor", "Views", "Checkout", "Index.cshtml"));

        Assert.DoesNotContain("asp-for=\"AddressId\"", view);
        Assert.DoesNotContain("name=\"AddressId\"", view);
        Assert.DoesNotContain("Endereço de entrega", view);
        Assert.DoesNotContain("Model.Addresses", view);
    }

    [Fact]
    public async Task Seller_customer_and_assisted_checkout_models_do_not_expose_or_require_AddressId()
    {
        var sellerInput = new SellerCheckoutViewModel { CustomerId = 21, PaymentTermId = 1 };
        var sellerErrors = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var sellerValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            sellerInput, new System.ComponentModel.DataAnnotations.ValidationContext(sellerInput), sellerErrors, validateAllProperties: true);

        var controller = new CheckoutController();
        var postResult = Assert.IsType<RedirectToActionResult>(
            await controller.Index(21, sellerInput, CancellationToken.None));

        Assert.True(sellerValid, string.Join("; ", sellerErrors.Select(error => error.ErrorMessage)));
        Assert.Null(typeof(SellerCheckoutViewModel).GetProperty("AddressId"));
        Assert.True(controller.ModelState.IsValid);
        Assert.Equal("OrderDrafts", postResult.ControllerName);

        var customerInput = new CheckoutViewModel { PaymentTermId = 1 };
        var customerErrors = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        Assert.True(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            customerInput, new System.ComponentModel.DataAnnotations.ValidationContext(customerInput), customerErrors, validateAllProperties: true));
        Assert.Null(typeof(CheckoutViewModel).GetProperty("AddressId"));

        var assistedInput = new AssistedOrderViewModel { PaymentTermId = 1 };
        var assistedErrors = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        Assert.True(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            assistedInput, new System.ComponentModel.DataAnnotations.ValidationContext(assistedInput), assistedErrors, validateAllProperties: true));
        Assert.Null(typeof(AssistedOrderViewModel).GetProperty("AddressId"));
    }

    [Fact]
    public async Task Legacy_checkout_success_redirects_to_the_seller_order_history()
    {
        var controller = new CheckoutController();

        var redirect = Assert.IsType<RedirectToActionResult>(
            await controller.Success(customerId: 21, id: 400, paymentId: null, CancellationToken.None));

        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal("Orders", redirect.ControllerName);
        Assert.Equal(21, redirect.RouteValues!["customerId"]);
        Assert.Equal(400, redirect.RouteValues["orderId"]);
    }

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "Orofoods.slnx"))) return current.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate Orofoods.slnx from the test directory.");
    }
}
