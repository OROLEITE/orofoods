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
}
