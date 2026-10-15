using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Areas.Vendedor.Controllers;
using Orofoods.Web.Authorization;
using Orofoods.Web.Controllers;
using Orofoods.Web.Models.Identity;

namespace Orofoods.Web.Tests.Controllers;

public sealed class CustomerSuggestionEndpointTests
{
    [Fact]
    public void Admin_customer_suggestions_remain_administrator_only()
    {
        var authorize = Assert.Single(typeof(AdminPortalAccessController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        var route = Assert.Single(typeof(AdminPortalAccessController).GetMethod(nameof(AdminPortalAccessController.SearchCustomers))!
            .GetCustomAttributes(typeof(HttpGetAttribute), true).Cast<HttpGetAttribute>());

        Assert.Equal(ApplicationRoles.Administrator, authorize.Roles);
        Assert.Equal("Clientes", route.Template);
    }

    [Theory]
    [InlineData(typeof(CustomersController), nameof(CustomersController.Search), "/Vendedor/Clientes/Buscar")]
    [InlineData(typeof(OrderDraftsController), nameof(OrderDraftsController.SearchCustomers), "/Vendedor/Rascunhos/Clientes")]
    public void Seller_customer_suggestions_keep_the_linked_representative_policy(Type controllerType, string actionName, string routeTemplate)
    {
        var authorize = Assert.Single(controllerType
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        var route = Assert.Single(controllerType.GetMethod(actionName)!
            .GetCustomAttributes(typeof(HttpGetAttribute), true).Cast<HttpGetAttribute>());

        Assert.Equal(OrofoodsPolicies.LinkedSalesRepresentative, authorize.Policy);
        Assert.Equal(routeTemplate, route.Template);
    }
}
