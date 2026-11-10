using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Authorization;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Areas.Vendedor.Controllers;

[Area("Vendedor")]
[Authorize(Policy = OrofoodsPolicies.LinkedSalesRepresentative)]
public sealed class CheckoutController : Controller
{
    [HttpGet("/Vendedor/Clientes/{customerId:int}/Checkout")]
    public Task<IActionResult> Index(int customerId, CancellationToken cancellationToken) => RedirectToOrderEntry(customerId);

    [HttpPost("/Vendedor/Clientes/{customerId:int}/Checkout")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Index(int customerId, SellerCheckoutViewModel input, CancellationToken cancellationToken) =>
        RedirectToOrderEntry(customerId);

    [HttpGet("/Vendedor/Clientes/{customerId:int}/Checkout/Sucesso/{id:int}")]
    public Task<IActionResult> Success(int customerId, int id, int? paymentId, CancellationToken cancellationToken) =>
        Task.FromResult<IActionResult>(RedirectToAction(
            "Details", "Orders", new { area = "Vendedor", customerId, orderId = id })!);

    private Task<IActionResult> RedirectToOrderEntry(int customerId) =>
        Task.FromResult<IActionResult>(RedirectToAction(
            "New", "OrderDrafts", new { area = "Vendedor", customerId })!);
}
