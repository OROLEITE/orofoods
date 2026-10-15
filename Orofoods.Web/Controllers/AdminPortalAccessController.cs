using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Identity;

namespace Orofoods.Web.Controllers;

[Authorize(Roles = ApplicationRoles.Administrator)]
[Route("Admin/Portais")]
public sealed class AdminPortalAccessController(
    ApplicationDbContext db,
    AdminSellerContextService sellerContextService,
    AdminPortalAccessAuditService auditService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await auditService.GetRecentAsync(cancellationToken));

    [HttpGet("SelecionarVendedor")]
    public async Task<IActionResult> SelectSeller(string? returnUrl)
    {
        ViewBag.ReturnUrl = SafeReturnUrl(returnUrl, "/Vendedor");
        ViewBag.SelectedRepresentativeId = sellerContextService.GetSelectedRepresentativeId(
            HttpContext.Session,
            User.FindFirstValue(ClaimTypes.NameIdentifier));
        return View(await GetActiveRepresentativesAsync());
    }

    [HttpPost("SelecionarVendedor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectSeller(int representativeId, string? returnUrl, CancellationToken cancellationToken)
    {
        var representative = await db.SalesRepresentatives.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == representativeId && x.IsActive, cancellationToken);
        if (representative is null)
        {
            ModelState.AddModelError(string.Empty, "Selecione um vendedor ativo.");
            ViewBag.ReturnUrl = SafeReturnUrl(returnUrl, "/Vendedor");
            ViewBag.SelectedRepresentativeId = representativeId;
            return View(await GetActiveRepresentativesAsync(cancellationToken));
        }

        var administratorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(administratorUserId))
        {
            return Forbid();
        }

        await auditService.RecordSelectionAsync(
            administratorUserId,
            User.FindFirstValue(ClaimTypes.Email),
            AdminPortalAccessType.SalesRepresentative,
            representative.Id,
            representative.Name,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);
        sellerContextService.SetSelectedRepresentativeId(HttpContext.Session, administratorUserId, representative.Id);

        return LocalRedirect(SafeReturnUrl(returnUrl, "/Vendedor"));
    }

    private static string SafeReturnUrl(string? returnUrl, string fallback) =>
        !string.IsNullOrWhiteSpace(returnUrl)
        && (returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            || returnUrl.StartsWith("~/", StringComparison.Ordinal))
        && !returnUrl.Contains('\\')
            ? returnUrl
            : fallback;

    private Task<List<Orofoods.Web.Models.Customers.SalesRepresentative>> GetActiveRepresentativesAsync(
        CancellationToken cancellationToken = default) => db.SalesRepresentatives.AsNoTracking()
        .Where(x => x.IsActive)
        .OrderBy(x => x.Name)
        .ToListAsync(cancellationToken);
}
