using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Authorization;

namespace Orofoods.Web.Areas.Vendedor.Controllers;

[Area("Vendedor")]
[Authorize(Policy = OrofoodsPolicies.LinkedSalesRepresentative)]
public sealed class CartController : Controller
{
    [HttpGet("/Vendedor/Clientes/{customerId:int}/Carrinho")]
    public Task<IActionResult> Index(int customerId, CancellationToken cancellationToken) => RedirectToOrderEntry(customerId);

    [HttpPost("/Vendedor/Clientes/{customerId:int}/Carrinho/Adicionar")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Add(int customerId, int productId, int quantity, CancellationToken cancellationToken) => RedirectToOrderEntry(customerId);

    [HttpPost("/Vendedor/Clientes/{customerId:int}/Carrinho/Atualizar")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Update(int customerId, int productId, int quantity, CancellationToken cancellationToken) => RedirectToOrderEntry(customerId);

    [HttpPost("/Vendedor/Clientes/{customerId:int}/Carrinho/Remover")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Remove(int customerId, int productId, CancellationToken cancellationToken) => RedirectToOrderEntry(customerId);

    private Task<IActionResult> RedirectToOrderEntry(int customerId) =>
        Task.FromResult<IActionResult>(RedirectToAction("New", "OrderDrafts", new { area = "Vendedor", customerId })!);
}
