using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Orders;

namespace Orofoods.Web.ViewComponents;

public sealed class CatalogCartViewComponent(
    CustomerAccessService customerAccessService,
    AdminCustomerContextService adminCustomerContextService,
    CartService cartService) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (User.Identity?.IsAuthenticated != true || User.IsInRole("Vendedor"))
        {
            return Content(string.Empty);
        }

        var userId = (User as ClaimsPrincipal)?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var customer = User.IsInRole("Cliente")
            ? await customerAccessService.GetApprovedCustomerAsync(userId)
            : null;

        if (customer is null && User.IsInRole("Administrador"))
        {
            var selectedCustomerId = adminCustomerContextService.GetSelectedCustomerId(HttpContext.Session, userId);
            if (selectedCustomerId is not null)
            {
                customer = await customerAccessService.GetApprovedCustomerByIdAsync(selectedCustomerId.Value);
            }
        }

        if (customer is null)
        {
            return Content(string.Empty);
        }

        var cart = await cartService.GetAsync(customer.Id, HttpContext.Session);
        return View(cart.Items.Sum(item => item.Quantity));
    }
}
