using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Authorization;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Sellers;

namespace Orofoods.Web.Areas.Vendedor.Controllers;

[Area("Vendedor")]
[Authorize(Policy = OrofoodsPolicies.LinkedSalesRepresentative)]
public sealed class CatalogController(
    SellerCatalogService catalogService,
    SalesRepresentativeAccessService accessService,
    CartService cartService) : Controller
{
    [HttpGet("/Vendedor/Clientes/{customerId:int}/Catalogo")]
    public async Task<IActionResult> Index(int customerId, string? q, CancellationToken cancellationToken)
    {
        var catalog = await catalogService.GetCatalogAsync(User, customerId, q, cancellationToken);
        if (catalog is null) return Forbid();

        var scope = await accessService.GetSellerCartScopeAsync(User, customerId, cancellationToken);
        if (scope is null) return Forbid();
        catalog.CartItemCount = (await cartService.GetAsync(customerId, HttpContext.Session, scope)).Items.Sum(item => item.Quantity);
        return View(catalog);
    }
}