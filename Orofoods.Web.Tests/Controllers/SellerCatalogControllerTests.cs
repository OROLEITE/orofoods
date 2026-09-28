using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Areas.Vendedor.Controllers;
using Orofoods.Web.Authorization;
using System.Reflection;

namespace Orofoods.Web.Tests.Controllers;

public class SellerCatalogControllerTests
{
    [Fact]
    public void Catalog_and_cart_controllers_require_the_linked_seller_policy()
    {
        var controllers = new[] { typeof(CatalogController), typeof(CartController) };
        Assert.All(controllers, controller =>
            Assert.Contains(controller.GetCustomAttributes<AuthorizeAttribute>(),
                attribute => attribute.Policy == OrofoodsPolicies.LinkedSalesRepresentative));
    }

    [Fact]
    public void Seller_mutations_use_antiforgery()
    {
        var methods = typeof(CartController).GetMethods()
            .Where(method => method.GetCustomAttributes<HttpPostAttribute>().Any())
            .ToArray();

        Assert.NotEmpty(methods);
        Assert.All(methods, method => Assert.NotEmpty(method.GetCustomAttributes<ValidateAntiForgeryTokenAttribute>()));
    }
}