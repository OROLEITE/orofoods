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
    WmcManualExportService wmcManualExportService,
    WmcSyncCoordinator wmcSyncCoordinator,
    WmcSyncService wmcSyncService) : Controller
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
        var result = await wmcManualExportService.ExportAsync(
            id,
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            User.Identity?.Name,
            cancellationToken);
        if (result.Disposition == WmcManualExportDisposition.Exported)
        {
            return File(System.Text.Encoding.UTF8.GetBytes(result.Content!), "text/plain; charset=utf-8", result.FileName!);
        }
        if (result.Disposition == WmcManualExportDisposition.NotFound) return NotFound();
        TempData["WmcError"] = result.Error ?? "Não foi possível exportar o pedido para o WMC.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Wmc()
    {
        ViewBag.IsRunning = wmcSyncCoordinator.IsRunning;
        ViewBag.LastRun = wmcSyncCoordinator.LastRun;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WmcSyncNow(CancellationToken cancellationToken)
    {
        var result = await wmcSyncCoordinator.RunExclusivelyAsync(() => wmcSyncService.SyncAllAsync(cancellationToken), cancellationToken);
        TempData["WmcSyncMessage"] = result is null
            ? "Sincroniza\u00e7\u00e3o j\u00e1 em andamento."
            : "Sincroniza\u00e7\u00e3o conclu\u00edda.";
        return RedirectToAction(nameof(Wmc));
    }
}
