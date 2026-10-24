using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Authorization;

namespace Orofoods.Web.Areas.Vendedor.Controllers;

[Area("Vendedor")]
[Authorize(Policy = OrofoodsPolicies.LinkedSalesRepresentative)]
public sealed class CatalogController : Controller
{
    [HttpGet("/Vendedor/Clientes/{customerId:int}/Catalogo")]
    public Task<IActionResult> Index(int customerId, string? q, CancellationToken cancellationToken) =>
        Task.FromResult<IActionResult>(RedirectToAction("New", "OrderDrafts", new { area = "Vendedor", customerId })!);
}
