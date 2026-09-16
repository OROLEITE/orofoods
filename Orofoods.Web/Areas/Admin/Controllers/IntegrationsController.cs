using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Services.Integrations;
using Orofoods.Web.Services.Orders;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Administrador")]
public class IntegrationsController(
    ApplicationDbContext db,
    OrderIntegrationService integrationService,
    WmcOrderFileGenerator wmcOrderFileGenerator,
    WmcExportAuditService wmcExportAuditService) : Controller
{
    public async Task<IActionResult> Index(string? q, IntegrationStatus? status, string? wmcStatus)
    {
        var query = db.Orders
            .AsNoTracking()
            .Include(order => order.Customer)
            .Include(order => order.WmcExportAudits)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(order => order.Number.Contains(q) || order.Customer!.TradeName.Contains(q));
        }

        if (status is not null)
        {
            query = query.Where(order => order.IntegrationStatus == status);
        }

        switch (wmcStatus?.Trim().ToLowerInvariant())
        {
            case "failed":
                query = query.Where(order => order.WmcExportAudits.Any() && !order.WmcExportAudits.OrderByDescending(audit => audit.ExportedAt).Select(audit => audit.Succeeded).First());
                break;
            case "succeeded":
                query = query.Where(order => order.WmcExportAudits.Any() && order.WmcExportAudits.OrderByDescending(audit => audit.ExportedAt).Select(audit => audit.Succeeded).First());
                break;
            case "none":
                query = query.Where(order => !order.WmcExportAudits.Any());
                break;
        }

        ViewBag.Query = q;
        ViewBag.Status = status;
        ViewBag.WmcStatus = wmcStatus;
        return View(await query.OrderByDescending(order => order.LastIntegrationAttempt ?? order.CreatedAt).ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reprocess(int id)
    {
        await integrationService.SendAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportWmc(int id, CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Items)
            .ThenInclude(item => item.Product)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (order is null)
        {
            return NotFound();
        }

        var result = wmcOrderFileGenerator.Build(order);
        if (!result.Succeeded)
        {
            var error = string.Join(" ", result.Errors);
            await wmcExportAuditService.RecordAsync(order.Id, CurrentUserId, CurrentUserEmail, null, false, error, cancellationToken);
            TempData["WmcError"] = error;
            return RedirectToAction(nameof(Index));
        }

        var fileName = $"WMC_{order.Number.Replace("-", string.Empty)}.txt";
        await wmcExportAuditService.RecordAsync(order.Id, CurrentUserId, CurrentUserEmail, fileName, true, null, cancellationToken);
        return File(System.Text.Encoding.UTF8.GetBytes(result.Content!), "text/plain; charset=utf-8", fileName);
    }

    private string? CurrentUserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    private string? CurrentUserEmail => User.Identity?.Name;
}
