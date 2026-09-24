using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using Orofoods.Web.Areas.Vendedor.Controllers;
using Orofoods.Web.Authorization;

namespace Orofoods.Web.Tests.Controllers;

public class SellerControllerTests
{
    [Fact]
    public void Seller_controllers_require_the_linked_seller_policy()
    {
        var controllers = new[]
        {
            typeof(DashboardController),
            typeof(CustomersController)
        };

        Assert.All(controllers, controller =>
            Assert.Contains(controller.GetCustomAttributes<AuthorizeAttribute>(),
                attribute => attribute.Policy == OrofoodsPolicies.LinkedSalesRepresentative));
    }

    [Fact]
    public void Seller_controllers_are_in_the_Vendedor_area()
    {
        Assert.All(
            new[] { typeof(DashboardController), typeof(CustomersController) },
            controller => Assert.NotNull(controller.GetCustomAttributes<AreaAttribute>().SingleOrDefault(attribute => attribute.RouteValue == "Vendedor")));
    }
}