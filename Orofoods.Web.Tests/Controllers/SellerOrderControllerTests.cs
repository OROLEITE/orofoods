using Microsoft.AspNetCore.Authorization;
using Orofoods.Web.Areas.Vendedor.Controllers;
using Orofoods.Web.Authorization;
using System.Reflection;

namespace Orofoods.Web.Tests.Controllers;

public class SellerOrderControllerTests
{
    [Fact]
    public void Order_controllers_require_linked_seller_policy()
    {
        Assert.All(new[] { typeof(OrdersController) }, controller =>
            Assert.Contains(controller.GetCustomAttributes<AuthorizeAttribute>(), x => x.Policy == OrofoodsPolicies.LinkedSalesRepresentative));
    }
}