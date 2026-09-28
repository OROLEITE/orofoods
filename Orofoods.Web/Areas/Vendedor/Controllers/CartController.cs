using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Authorization;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Orders;

namespace Orofoods.Web.Areas.Vendedor.Controllers;

[Area("Vendedor")]
[Authorize(Policy = OrofoodsPolicies.LinkedSalesRepresentative)]
public sealed class CartController(
    CartService cartService,
    SalesRepresentativeAccessService accessService) : Controller
{
    [HttpGet("/Vendedor/Clientes/{customerId:int}/Carrinho")]
    public async Task<IActionResult> Index(int customerId, CancellationToken cancellationToken)
    {
        var scope = await accessService.GetSellerCartScopeAsync(User, customerId, cancellationToken);
        if (scope is null) return Forbid();
        ViewBag.CustomerId = customerId;
        return View(await cartService.GetAsync(customerId, HttpContext.Session, scope));
    }

    [HttpPost("/Vendedor/Clientes/{customerId:int}/Carrinho/Adicionar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int customerId, int productId, int quantity, CancellationToken cancellationToken)
    {
        var scope = await accessService.GetSellerCartScopeAsync(User, customerId, cancellationToken);
        if (scope is null) return Forbid();
        if (quantity <= 0)
        {
            TempData["SellerCartError"] = "Informe uma quantidade válida.";
            return RedirectToAction("Index", "Catalog", new { area = "Vendedor", customerId });
        }

        try
        {
            await cartService.AddAsync(customerId, productId, quantity, HttpContext.Session, scope);
        }
        catch (InvalidOperationException exception)
        {
            TempData["SellerCartError"] = exception.Message;
        }

        return RedirectToAction(nameof(Index), new { customerId });
    }

    [HttpPost("/Vendedor/Clientes/{customerId:int}/Carrinho/Atualizar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int customerId, int productId, int quantity, CancellationToken cancellationToken)
    {
        var scope = await accessService.GetSellerCartScopeAsync(User, customerId, cancellationToken);
        if (scope is null) return Forbid();
        if (quantity <= 0)
        {
            TempData["SellerCartError"] = "Informe uma quantidade válida.";
            return RedirectToAction(nameof(Index), new { customerId });
        }

        try
        {
            await cartService.UpdateAsync(customerId, productId, quantity, HttpContext.Session, scope);
        }
        catch (InvalidOperationException exception)
        {
            TempData["SellerCartError"] = exception.Message;
        }
        return RedirectToAction(nameof(Index), new { customerId });
    }

    [HttpPost("/Vendedor/Clientes/{customerId:int}/Carrinho/Remover")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int customerId, int productId, CancellationToken cancellationToken)
    {
        var scope = await accessService.GetSellerCartScopeAsync(User, customerId, cancellationToken);
        if (scope is null) return Forbid();
        cartService.Remove(productId, HttpContext.Session, scope);
        return RedirectToAction(nameof(Index), new { customerId });
    }
}