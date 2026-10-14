using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Integrations;
using Orofoods.Web.Services.Orders;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Administrador")]
public class IntegrationsController(
    ApplicationDbContext db,
    WmcManualExportService wmcManualExportService,
    WmcSyncCoordinator wmcSyncCoordinator,
    WmcSyncService wmcSyncService,
    IOptions<WmcSyncOptions> wmcSyncOptions,
    IWmcBrandReader wmcBrandReader,
    WmcProductPreviewService wmcProductPreviewService,
    ILogger<IntegrationsController> logger) : Controller
{
    public async Task<IActionResult> Index(string? q, IntegrationStatus? status, string? wmcStatus)
    {
        var query = db.Orders
            .AsNoTracking()
            .Include(order => order.Customer)
            .Include(order => order.WmcExportAudits)
            .Include(order => order.WmcOrderExportState)
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
                query = query.Where(order => order.WmcExportAudits.Any() && !order.WmcExportAudits.OrderByDescending(audit => audit.ExportedAt).ThenByDescending(audit => audit.Id).Select(audit => audit.Succeeded).First());
                break;
            case "succeeded":
                query = query.Where(order => order.WmcExportAudits.Any() && order.WmcExportAudits.OrderByDescending(audit => audit.ExportedAt).ThenByDescending(audit => audit.Id).Select(audit => audit.Succeeded).First());
                break;
            case "none":
                query = query.Where(order => !order.WmcExportAudits.Any());
                break;
        }

        ViewBag.Query = q;
        ViewBag.Status = status;
        ViewBag.WmcStatus = wmcStatus;
        var orders = await query.OrderByDescending(order => order.LastIntegrationAttempt ?? order.CreatedAt).ToListAsync();
        ViewData["WmcManualExportEligibleOrderIds"] = orders
            .Where(wmcManualExportService.CanManuallyExport)
            .Select(order => order.Id)
            .ToHashSet();
        return View(orders);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Reprocess(int id, CancellationToken cancellationToken = default)
    {
        return ExportWmc(id, cancellationToken);
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

    public async Task<IActionResult> Wmc(CancellationToken cancellationToken)
    {
        ViewBag.IsRunning = wmcSyncCoordinator.IsRunning;
        ViewBag.LastRun = wmcSyncCoordinator.LastRun;
        IReadOnlyList<WmcBrandRecord> brands = [];
        try
        {
            brands = await wmcBrandReader.GetAllAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "WMC brand loading failed. Operation={Operation}", "LoadBrands");
            TempData["WmcPreviewError"] = "N\u00E3o foi poss\u00EDvel carregar as marcas WMC.";
        }
        return View(new WmcPreviewPageModel(brands, [], null, []));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WmcPreview(short[]? brandCodes, CancellationToken cancellationToken)
    {
        IReadOnlyList<WmcBrandRecord> brands = [];
        try
        {
            brands = await wmcBrandReader.GetAllAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "WMC brand loading failed. Operation={Operation}", "LoadBrands");
            TempData["WmcPreviewError"] = "N\u00E3o foi poss\u00EDvel carregar as marcas WMC.";
        }
        var availableCodes = brands.Select(brand => (short)brand.Code).ToHashSet();
        var selectedCodes = (brandCodes ?? [])
            .Where(availableCodes.Contains)
            .Distinct()
            .ToArray();
        if (selectedCodes.Length == 0)
        {
            selectedCodes = availableCodes.OrderBy(code => code).ToArray();
        }

        var batch = await wmcProductPreviewService.PreviewByBrandsAsync(selectedCodes, cancellationToken);
        var previewsByBrand = batch.ByBrand
            .Select(item => item with
            {
                Description = brands.FirstOrDefault(brand => brand.Code == item.Code)?.Description ?? item.Description
            })
            .ToList();
        ViewBag.IsRunning = wmcSyncCoordinator.IsRunning;
        ViewBag.LastRun = wmcSyncCoordinator.LastRun;
        return View(nameof(Wmc), new WmcPreviewPageModel(brands, selectedCodes, batch.Summary, previewsByBrand));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WmcSyncNow(CancellationToken cancellationToken)
    {
        if (!wmcSyncOptions.Value.Enabled)
        {
            return Forbid();
        }

        var result = await wmcSyncCoordinator.RunExclusivelyAsync(() => wmcSyncService.SyncAllAsync(cancellationToken), cancellationToken);
        TempData["WmcSyncMessage"] = result is null
            ? "Sincroniza\u00e7\u00e3o j\u00e1 em andamento."
            : "Sincroniza\u00e7\u00e3o conclu\u00edda.";
        return RedirectToAction(nameof(Wmc));
    }
}
