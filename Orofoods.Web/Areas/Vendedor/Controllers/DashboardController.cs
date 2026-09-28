using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Authorization;
using Orofoods.Web.Services.Sellers;

namespace Orofoods.Web.Areas.Vendedor.Controllers;

[Area("Vendedor")]
[Authorize(Policy = OrofoodsPolicies.LinkedSalesRepresentative)]
public sealed class DashboardController(SellerWorkspaceService workspaceService) : Controller
{
    [HttpGet("/Vendedor")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var dashboard = await workspaceService.GetDashboardAsync(User, cancellationToken);
        return dashboard is null ? Forbid() : View(dashboard);
    }
}