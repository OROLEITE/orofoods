using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Authorization;
using Orofoods.Web.Services.Sellers;

namespace Orofoods.Web.Areas.Vendedor.Controllers;

[Area("Vendedor")]
[Authorize(Policy = OrofoodsPolicies.LinkedSalesRepresentative)]
public sealed class CustomersController(SellerWorkspaceService workspaceService) : Controller
{
    [HttpGet("/Vendedor/Clientes")]
    public async Task<IActionResult> Index(string? q, CancellationToken cancellationToken)
    {
        var customers = await workspaceService.SearchCustomersAsync(User, q, cancellationToken);
        return customers is null ? Forbid() : View(customers);
    }

    [HttpGet("/Vendedor/Clientes/{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var workspace = await workspaceService.GetCustomerWorkspaceAsync(User, id, cancellationToken);
        return workspace is null ? Forbid() : View(workspace);
    }
}