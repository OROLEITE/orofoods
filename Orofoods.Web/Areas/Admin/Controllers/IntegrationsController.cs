using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
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
    WmcProductImportService wmcProductImportService,
    ILogger<IntegrationsController> logger,
    IHostEnvironment hostEnvironment,
    WmcBimboStagingImportService wmcBimboStagingImportService,
    WmcInitialLoadReadinessReleaseService initialLoadReadinessReleaseService,
    IOptions<WmcInitialLoadReleaseOptions> initialLoadReleaseOptions,
    WmcExportAuditService? wmcExportAuditService = null,
    WmcJsonOrderErpIntegration? wmcJsonOrderErpIntegration = null,
    WmcCustomerImportService? wmcCustomerImportService = null) : Controller
{

    public async Task<IActionResult> Index(string? q, IntegrationStatus? status, string? wmcStatus)
    {
        _ = wmcExportAuditService;
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
    public async Task<IActionResult> ReconcileWmcOrderNumber(int id, CancellationToken cancellationToken)
    {
        if (wmcJsonOrderErpIntegration is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var result = await wmcJsonOrderErpIntegration.ReconcileNroPedidoAsync(id, cancellationToken);
        if (result.Recovered)
        {
            TempData["WmcReconciliationMessage"] = $"Número WMC recuperado: {result.ErpOrderNumber}.";
        }
        else
        {
            TempData["WmcReconciliationError"] = result.Error ?? "O WMC ainda não retornou um número de pedido positivo.";
        }

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

    public async Task<IActionResult> Wmc(CancellationToken cancellationToken)
    {
        ViewBag.WmcCustomerSellerCodes = wmcCustomerImportService is null
            ? []
            : await wmcCustomerImportService.GetSellerCodesAsync(cancellationToken);
        ViewBag.WmcCustomerPreview = null;
        ViewBag.WmcCustomerRegularizationPreview = null;
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
        return View(new WmcPreviewPageModel(brands, [], null, [], LastProductSyncAt: await wmcProductImportService.GetLastProductImportAsync(cancellationToken)));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WmcProducts(short brandCode, CancellationToken cancellationToken)
    {
        var brands = await wmcBrandReader.GetAllAsync(cancellationToken);
        var brand = brands.SingleOrDefault(item => item.Code == brandCode);
        if (brand is null) return BadRequest("Marca WMC inválida.");
        var candidates = await wmcProductImportService.GetCandidatesAsync(brandCode, cancellationToken);
        SetWmcViewState();
        return View(nameof(Wmc), new WmcPreviewPageModel(brands, [], null, [], "Products", brandCode, brand.Description, candidates,
            LastProductSyncAt: await wmcProductImportService.GetLastProductImportAsync(cancellationToken)));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WmcCustomerPreview(string sellerCode, CancellationToken cancellationToken)
    {
        if (wmcCustomerImportService is null) return Problem("Importação manual de clientes WMC indisponível.");
        ViewBag.WmcCustomerSellerCodes = await wmcCustomerImportService.GetSellerCodesAsync(cancellationToken);
        ViewBag.WmcCustomerPreview = await wmcCustomerImportService.PreviewAsync(sellerCode, cancellationToken);
        ViewBag.IsRunning = wmcSyncCoordinator.IsRunning;
        ViewBag.LastRun = wmcSyncCoordinator.LastRun;
        IReadOnlyList<WmcBrandRecord> brands = [];
        try
        {
            brands = await wmcBrandReader.GetAllAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "WMC brand loading failed while rendering customer preview. Operation={Operation}", "CustomerPreview");
            TempData["WmcPreviewError"] = "NÃ£o foi possÃ­vel consultar o espelho WMC agora. Tente novamente mais tarde.";
        }

        DateTime? lastProductSyncAt = null;
        try
        {
            lastProductSyncAt = await wmcProductImportService.GetLastProductImportAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "WMC product sync timestamp unavailable while rendering customer preview.");
        }

        return View(nameof(Wmc), new WmcPreviewPageModel(brands, [], null, [], LastProductSyncAt: lastProductSyncAt));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WmcCustomerRegularizationPreview(CancellationToken cancellationToken)
    {
        if (wmcCustomerImportService is null) return Problem("Importação manual de clientes WMC indisponível.");
        ViewBag.WmcCustomerSellerCodes = await wmcCustomerImportService.GetSellerCodesAsync(cancellationToken);
        ViewBag.WmcCustomerPreview = null;
        ViewBag.WmcCustomerRegularizationPreview = await wmcCustomerImportService.PreviewPendingAsync(cancellationToken);
        SetWmcViewState();
        return View(nameof(Wmc), new WmcPreviewPageModel([], [], null, []));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WmcCustomerRegularization(bool confirm, CancellationToken cancellationToken)
    {
        if (wmcCustomerImportService is null) return Problem("Importação manual de clientes WMC indisponível.");
        var result = await wmcSyncCoordinator.RunExclusivelyAsync(
            () => wmcCustomerImportService.RegularizePendingAsync(confirm, cancellationToken), cancellationToken);
        TempData["WmcCustomerImportMessage"] = result is null
            ? "Já existe uma sincronização WMC em andamento."
            : result.ErrorMessage ?? $"Regularização concluída: {result.Updated} cliente(s) liberado(s).";
        return RedirectToAction(nameof(Wmc));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WmcConfirmSelection(short brandCode, string[]? selectedProductCodes, CancellationToken cancellationToken)
    {
        var brands = await wmcBrandReader.GetAllAsync(cancellationToken);
        var brand = brands.SingleOrDefault(item => item.Code == brandCode);
        if (brand is null) return BadRequest("Marca WMC inválida.");
        var candidates = await wmcProductImportService.GetCandidatesAsync(brandCode, cancellationToken);
        var requested = (selectedProductCodes ?? []).Select(WmcProductRules.NormalizeCode).Distinct(StringComparer.Ordinal).ToArray();
        var selected = candidates.Where(candidate => requested.Contains(candidate.WmcCode, StringComparer.Ordinal) && candidate.Selectable).ToArray();
        if (selected.Length == 0 || selected.Length != requested.Length)
        {
            ModelState.AddModelError("", "Seleção vazia ou desatualizada. Revise os produtos selecionados.");
            SetWmcViewState();
            return View(nameof(Wmc), new WmcPreviewPageModel(brands, [], null, [], "Products", brandCode, brand.Description, candidates, requested));
        }
        var summary = new WmcProductImportSummary(selected.Length, selected.Count(item => item.ProductId is null), selected.Count(item => item.ProductId is not null),
            selected.Count(item => item.Changes.Count > 0), selected.Count(item => item.Changes.Count == 0));
        SetWmcViewState();
        return View(nameof(Wmc), new WmcPreviewPageModel(brands, [], null, [], "Confirmation", brandCode, brand.Description, candidates, requested, summary));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WmcImportProducts(short brandCode, string[]? selectedProductCodes, string reimportExisting, bool simulationValidated, CancellationToken cancellationToken)
    {
        if (string.Equals(reimportExisting, "cancel", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction(nameof(Wmc));
        if (!simulationValidated)
            return BadRequest("A conferência das alterações é obrigatória antes da confirmação.");
        var brands = await wmcBrandReader.GetAllAsync(cancellationToken);
        var brand = brands.SingleOrDefault(item => item.Code == brandCode);
        if (brand is null) return BadRequest("Marca WMC inválida.");
        if (reimportExisting is not ("true" or "false")) return BadRequest("Decisão inválida.");
        var codes = (selectedProductCodes ?? []).Select(WmcProductRules.NormalizeCode).Distinct(StringComparer.Ordinal).ToArray();
        var candidates = await wmcProductImportService.GetCandidatesAsync(brandCode, cancellationToken);
        var validSelection = candidates
            .Where(candidate => codes.Contains(candidate.WmcCode, StringComparer.Ordinal))
            .ToArray();
        if (codes.Length == 0 || validSelection.Length != codes.Length || validSelection.Any(candidate => !candidate.Selectable))
        {
            ModelState.AddModelError("", "A conferência ficou desatualizada. Revise os produtos antes de confirmar.");
            SetWmcViewState();
            return View(nameof(Wmc), new WmcPreviewPageModel(brands, [], null, [], "Products", brandCode, brand.Description, candidates, codes));
        }
        var result = await wmcProductImportService.ImportAsync(brandCode, codes, reimportExisting == "true", cancellationToken);
        var refreshedCandidates = await wmcProductImportService.GetCandidatesAsync(brandCode, cancellationToken);
        SetWmcViewState();
        return View(nameof(Wmc), new WmcPreviewPageModel(brands, [], null, [], "Result", brandCode, brand.Description,
            Candidates: refreshedCandidates, SelectedProductCodes: codes, ImportResult: result,
            LastProductSyncAt: await wmcProductImportService.GetLastProductImportAsync(cancellationToken)));
    }

    private void SetWmcViewState()
    {
        ViewBag.IsRunning = wmcSyncCoordinator.IsRunning;
        ViewBag.LastRun = wmcSyncCoordinator.LastRun;
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
        return View(nameof(Wmc), new WmcPreviewPageModel(brands, selectedCodes, batch.Summary, previewsByBrand,
            LastProductSyncAt: await wmcProductImportService.GetLastProductImportAsync(cancellationToken)));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WmcBimboSimulate(CancellationToken cancellationToken)
    {
        if (!hostEnvironment.IsStaging()) return Forbid();
        var result = await wmcBimboStagingImportService.SimulateAsync(cancellationToken);
        return Ok(new WmcBimboSimulationResponse(result.Succeeded, result.Products, result.ErrorCode));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WmcBimboImport(string[]? selectedWmcCodes, CancellationToken cancellationToken)
    {
        if (!hostEnvironment.IsStaging())
        {
            return Forbid();
        }

        var actorUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            return Forbid();
        }

        var result = await wmcBimboStagingImportService.ImportSelectedAsync(actorUserId, selectedWmcCodes, cancellationToken);
        var response = new WmcBimboImportResponse(
            result.Succeeded,
            result.ImportedWmcCodes.Count,
            result.SkippedWmcCodes.Count,
            result.ErrorSummary,
            GetWmcBimboErrorMessage(result.ErrorSummary),
            result.Products);
        return result.Succeeded ? Ok(response) : Conflict(response);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WmcBimboActivate(int productId, string? reason, CancellationToken cancellationToken)
    {
        if (!hostEnvironment.IsStaging()) return Forbid();
        var result = await initialLoadReadinessReleaseService.ActivateBimboAsync(
            productId,
            reason,
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            cancellationToken);
        var succeeded = result.Status is WmcInitialLoadReleaseStatus.Released or WmcInitialLoadReleaseStatus.AlreadyReady;
        return StatusCode(succeeded ? StatusCodes.Status200OK : StatusCodes.Status409Conflict,
            new WmcBimboActivationResponse(succeeded, result.Status.ToString(), result.Message));
    }

    private async Task LoadBimboManagementRowsAsync(CancellationToken cancellationToken)
    {
        var canActivateBimbo = hostEnvironment.IsStaging() && initialLoadReleaseOptions.Value.Enabled;
        ViewBag.BimboProducts = await wmcBimboStagingImportService.GetAdminProductsAsync(canActivateBimbo, cancellationToken);
        ViewBag.CanManageBimbo = hostEnvironment.IsStaging();
        ViewBag.CanActivateBimbo = canActivateBimbo;
    }

    private static string? GetWmcBimboErrorMessage(string? errorCode) => errorCode switch
    {
        "NO_PRODUCTS_SELECTED" => "Selecione ao menos um produto validado na simulação.",
        "PRODUCT_NOT_APPROVED" => "A seleção contém códigos fora do lote BIMBO homologado.",
        "WMC_READ_FAILED" => "Não foi possível consultar o espelho WMC.",
        "PENDING_CATEGORY_ACTIVE" => "A categoria pendente está ativa e precisa ser revisada.",
        "LOCAL_WMC_CODE_CONFLICT" => "Há códigos WMC duplicados no cadastro Orofoods.",
        "SOURCE_WMC_CODE_CONFLICT" => "Há códigos WMC duplicados no espelho.",
        "APPROVED_PRODUCT_MISSING" => "Um ou mais produtos selecionados não foram encontrados no WMC.",
        "SOURCE_BRAND_MISMATCH" => "Um produto selecionado não pertence à marca BIMBO 104.",
        "SOURCE_PRODUCT_NOT_ACTIVE" => "Um produto selecionado está inativo no WMC.",
        "SOURCE_STOCK_UNIT_MISMATCH" => "A unidade de estoque do WMC não é PC.",
        "SOURCE_PRODUCT_NAME_INVALID" => "A descrição de um produto selecionado é inválida.",
        "SOURCE_EAN13_INVALID" => "O EAN-13 de um produto está ausente ou inválido.",
        "SOURCE_EAN13_MISMATCH" => "Um EAN-13 não corresponde à referência homologada.",
        "SOURCE_STOCK_INVALID" => "O estoque de um produto selecionado é inválido.",
        "LOCAL_SKU_CONFLICT" => "O SKU WMC reservado já está sendo utilizado por outro produto.",
        "IMPORT_FAILED" => "A importação falhou e a transação foi revertida.",
        null => null,
        _ => "A importação foi recusada após a validação. Revise os dados e tente novamente."
    };

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WmcCustomerImport(string sellerCode, bool confirm, CancellationToken cancellationToken)
    {
        if (wmcCustomerImportService is null) return Problem("Importação manual de clientes WMC indisponível.");
        if (!confirm)
        {
            TempData["WmcCustomerImportError"] = "Confirme explicitamente a importação após revisar a prévia.";
            return RedirectToAction(nameof(Wmc));
        }

        var result = await wmcSyncCoordinator.RunExclusivelyAsync(
            () => wmcCustomerImportService.ImportAsync(sellerCode, cancellationToken),
            cancellationToken);
        if (result is null)
        {
            TempData["WmcCustomerImportError"] = "Já existe uma sincronização WMC em andamento.";
            return RedirectToAction(nameof(Wmc));
        }
        TempData["WmcCustomerImportMessage"] = result.ErrorMessage is null
            ? $"Importação concluída: {result.Created} criado(s), {result.Updated} atualizado(s), {result.Ignored} ignorado(s), {result.Conflicts} conflito(s)."
            : result.ErrorMessage;
        return RedirectToAction(nameof(Wmc));
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

public sealed record WmcBimboImportResponse(
    bool Succeeded,
    int ImportedProducts,
    int SkippedProducts,
    string? ErrorCode,
    string? ErrorMessage,
    IReadOnlyList<WmcBimboProductOutcome> Products);

public sealed record WmcBimboSimulationResponse(
    bool Succeeded,
    IReadOnlyList<WmcBimboProductSimulation> Products,
    string? ErrorCode);

public sealed record WmcBimboActivationResponse(bool Succeeded, string Status, string Message);
