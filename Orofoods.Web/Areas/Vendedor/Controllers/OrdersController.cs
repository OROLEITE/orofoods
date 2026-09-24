using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Authorization;
using Orofoods.Web.Services.Sellers;

namespace Orofoods.Web.Areas.Vendedor.Controllers;

[Area("Vendedor")]
[Authorize(Policy = OrofoodsPolicies.LinkedSalesRepresentative)]
public sealed class OrdersController(SellerOrderHistoryService orderHistoryService) : Controller
{
    [HttpGet("/Vendedor/Clientes/{customerId:int}/Pedidos")]
    public async Task<IActionResult> Index(int customerId, string? q, string? status, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var history = await orderHistoryService.GetHistoryAsync(User, customerId, q, status, page, pageSize, cancellationToken);
        return history is null ? Forbid() : View(history);
    }

    [HttpGet("/Vendedor/Clientes/{customerId:int}/Pedidos/{orderId:int}")]
    public async Task<IActionResult> Details(int customerId, int orderId, CancellationToken cancellationToken)
    {
        var detail = await orderHistoryService.GetDetailsAsync(User, customerId, orderId, cancellationToken);
        return detail is null ? Forbid() : View(detail);
    }
}