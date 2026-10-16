using System.Data;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Inventory;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcBimboStagingImportService(
    ApplicationDbContext db,
    IWmcProductReader productReader,
    ILogger<WmcBimboStagingImportService> logger,
    TimeProvider timeProvider)
{
    private const string PendingCategorySlug = "wmc-pendente-categorizacao";
    private static readonly IReadOnlyList<string> ProductCodes = WmcBimboProductRules.ApprovedProductCodes;
    private static readonly HashSet<string> ProductCodeSet = ProductCodes.ToHashSet(StringComparer.Ordinal);

    public static IReadOnlyList<string> ApprovedProductCodes => ProductCodes;

    public async Task<WmcBimboImportSimulationResult> SimulateAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WmcProductRecord> rows;
        try
        {
            rows = await productReader.GetAllByBrandAsync(WmcBimboProductRules.BrandCode, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError("WMC BIMBO simulation read failed. ErrorType={ErrorType}", exception.GetType().Name);
            return new(false, ProductCodes.Select(code => FailedSimulation(code, "WMC_READ_FAILED", "Não foi possível consultar o espelho WMC.")).ToArray(), "WMC_READ_FAILED");
        }

        var reservedSkus = ProductCodes.Select(code => $"WMC-{code}").ToArray();
        var localProducts = await db.Products.AsNoTracking()
            .Where(product => product.WmcCode != null || reservedSkus.Contains(product.Sku.ToUpper()))
            .ToListAsync(cancellationToken);
        var assessment = await AssessProductsAsync(rows, localProducts, cancellationToken);
        return new(assessment.All(row => row.Issues.Count == 0), assessment, null);
    }

    public async Task<IReadOnlyList<WmcBimboAdminProductRow>> GetAdminProductsAsync(CancellationToken cancellationToken = default)
    {
        var products = await db.Products.AsNoTracking()
            .Include(product => product.ProductCategory)
            .Where(product => product.WmcCode != null)
            .ToListAsync(cancellationToken);
        var targetProducts = products
            .Where(product => ProductCodeSet.Contains(WmcBimboProductRules.NormalizeCode(product.WmcCode)))
            .GroupBy(product => WmcBimboProductRules.NormalizeCode(product.WmcCode), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var productIds = products.Select(product => product.Id).ToArray();
        var inventories = await db.ProductInventories.AsNoTracking()
            .Where(inventory => productIds.Contains(inventory.ProductId))
            .ToDictionaryAsync(inventory => inventory.ProductId, cancellationToken);

        var result = new List<WmcBimboAdminProductRow>(ProductCodes.Count);
        foreach (var code in ProductCodes)
        {
            var matches = targetProducts.GetValueOrDefault(code) ?? [];
            if (matches.Length == 0)
            {
                result.Add(new(code, null, null, null, null, null, null, WmcBimboProductRules.CommercialStatusPending, false, ["Ainda não importado."]));
                continue;
            }

            if (matches.Length > 1)
            {
                result.Add(new(code, null, null, null, null, null, null, WmcBimboProductRules.CommercialStatusPending, false, ["Conflito: há mais de um produto local com este código WMC."]));
                continue;
            }

            var product = matches[0];
            inventories.TryGetValue(product.Id, out var inventory);
            var issues = WmcBimboProductRules.ValidateCommercialData(product, product.ProductCategory, inventory);
            var commercialIssues = issues.Where(issue => issue.Code is not "WMC_PRODUCT_INACTIVE" and not "WMC_STOCK_UNAVAILABLE").ToArray();
            var isActive = product.IsActive && product.IsAvailable && product.WmcInitialLoadReady
                && product.IsWmcActive && product.WmcStockAvailable && inventory is not null
                && inventory.QuantityOnHand - inventory.QuantityReserved > 0;
            var status = commercialIssues.Length > 0
                ? WmcBimboProductRules.CommercialStatusPending
                : isActive ? WmcBimboProductRules.CommercialStatusActive : WmcBimboProductRules.CommercialStatusInactive;
            var reasons = issues.Select(issue => issue.Message).ToList();
            if (status == WmcBimboProductRules.CommercialStatusInactive && reasons.Count == 0)
                reasons.Add("Cadastro completo; aguardando ativação comercial individual.");

            result.Add(new(
                code,
                product.Id,
                product.Name,
                product.Ean13,
                product.Dun14,
                product.BasePrice,
                inventory is null ? null : Math.Max(0, inventory.QuantityOnHand - inventory.QuantityReserved),
                status,
                issues.Count == 0 && !isActive,
                reasons));
        }

        return result;
    }

    public Task<WmcBimboImportResult> ImportAsync(string actorUserId, CancellationToken cancellationToken = default) =>
        ImportSelectedAsync(actorUserId, ProductCodes, cancellationToken);

    public async Task<WmcBimboImportResult> ImportSelectedAsync(
        string actorUserId,
        IReadOnlyCollection<string>? selectedWmcCodes,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorUserId);
        var startedAt = timeProvider.GetUtcNow();
        var selected = (selectedWmcCodes ?? [])
            .Select(WmcBimboProductRules.NormalizeCode)
            .Where(code => code.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (selected.Length == 0)
            return new(false, [], [], "NO_PRODUCTS_SELECTED", []);
        if (selected.Any(code => !ProductCodeSet.Contains(code)))
            return await RecordRejectedAsync(actorUserId, startedAt, "PRODUCT_NOT_APPROVED", cancellationToken);

        IReadOnlyList<WmcProductRecord> rows;
        try
        {
            rows = await productReader.GetAllByBrandAsync(WmcBimboProductRules.BrandCode, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError("WMC BIMBO product read failed. ErrorType={ErrorType}", exception.GetType().Name);
            return await RecordRejectedAsync(actorUserId, startedAt, "WMC_READ_FAILED", cancellationToken, outcome: "Failed");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var selectedSkus = selected.Select(code => $"WMC-{code}").ToArray();
            var localProducts = await db.Products
                .Where(product => product.WmcCode != null || selectedSkus.Contains(product.Sku.ToUpper()))
                .ToListAsync(cancellationToken);
            var selectedRows = (await AssessProductsAsync(rows, localProducts, cancellationToken))
                .Where(product => selected.Contains(product.WmcCode, StringComparer.Ordinal))
                .ToArray();
            var invalid = selectedRows.FirstOrDefault(product => product.Issues.Count > 0);
            if (invalid is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();
                return await RecordRejectedAsync(actorUserId, startedAt, invalid.Issues[0].Code, cancellationToken,
                    issueDetails: selectedRows.Where(product => product.Issues.Count > 0));
            }

            var category = await db.ProductCategories.SingleOrDefaultAsync(item => item.Slug == PendingCategorySlug, cancellationToken);
            if (category is not null && category.IsActive)
            {
                await transaction.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();
                return await RecordRejectedAsync(actorUserId, startedAt, "PENDING_CATEGORY_ACTIVE", cancellationToken);
            }

            if (category is null)
            {
                category = new ProductCategory { Name = "A categorizar (WMC)", Slug = PendingCategorySlug, IsActive = false };
                db.ProductCategories.Add(category);
            }

            var imported = new List<string>();
            var skipped = new List<string>();
            var outcomes = new List<WmcBimboProductOutcome>();
            var localByCode = localProducts
                .Where(product => selected.Contains(WmcBimboProductRules.NormalizeCode(product.WmcCode), StringComparer.Ordinal))
                .ToDictionary(product => WmcBimboProductRules.NormalizeCode(product.WmcCode), StringComparer.Ordinal);
            var sourceByCode = rows
                .Where(row => ProductCodeSet.Contains(WmcBimboProductRules.NormalizeCode(row.CodProduto)))
                .GroupBy(row => WmcBimboProductRules.NormalizeCode(row.CodProduto), StringComparer.Ordinal)
                .Where(group => group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

            foreach (var code in selected)
            {
                if (localByCode.ContainsKey(code))
                {
                    skipped.Add(code);
                    outcomes.Add(new(code, "Ignorado", "O produto já existe; os dados locais foram preservados."));
                    continue;
                }

                var row = sourceByCode[code];
                var stock = decimal.ToInt32(row.EstoqueAtual!.Value);
                var product = new Product
                {
                    WmcCode = code,
                    WmcBrandCode = WmcBimboProductRules.BrandCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Sku = $"WMC-{code}",
                    Ean = row.Ean!.Trim(),
                    Ean13 = row.Ean!.Trim(),
                    Name = row.Produto.Trim(),
                    Brand = "BIMBO",
                    Unit = "PCT",
                    CommercialUnitCode = "PCT",
                    BasePrice = 1.00m,
                    ProductCategory = category,
                    IsActive = false,
                    IsAvailable = false,
                    IsWmcActive = true,
                    WmcStockAvailable = stock > 0,
                    WmcInitialLoadReady = false
                };
                db.Products.Add(product);
                db.ProductInventories.Add(new ProductInventory { Product = product, QuantityOnHand = stock });
                imported.Add(code);
                outcomes.Add(new(code, "Importado", "Importado como PCT e mantido pendente de cadastro comercial."));
            }

            var completedAt = timeProvider.GetUtcNow();
            db.WmcProductImportRuns.Add(CreateAudit(actorUserId, startedAt, completedAt, "Succeeded", imported, skipped, null));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(true, imported, skipped, null, outcomes);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            try
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            catch (Exception rollbackException) when (rollbackException is not OperationCanceledException)
            {
                logger.LogError("WMC BIMBO transaction rollback failed. ErrorType={ErrorType}", rollbackException.GetType().Name);
            }

            db.ChangeTracker.Clear();
            logger.LogError("WMC BIMBO import failed. ErrorType={ErrorType}", exception.GetType().Name);
            return await RecordRejectedAsync(actorUserId, startedAt, "IMPORT_FAILED", cancellationToken, outcome: "Failed");
        }
    }

    private async Task<IReadOnlyList<WmcBimboProductSimulation>> AssessProductsAsync(
        IReadOnlyList<WmcProductRecord> sourceRows,
        IReadOnlyList<Product> localProducts,
        CancellationToken cancellationToken)
    {
        var expectedRows = sourceRows
            .Where(row => ProductCodeSet.Contains(WmcBimboProductRules.NormalizeCode(row.CodProduto)))
            .GroupBy(row => WmcBimboProductRules.NormalizeCode(row.CodProduto), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var localRows = localProducts
            .Where(product => ProductCodeSet.Contains(WmcBimboProductRules.NormalizeCode(product.WmcCode)))
            .GroupBy(product => WmcBimboProductRules.NormalizeCode(product.WmcCode), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var categoryIsInvalid = await db.ProductCategories.AsNoTracking()
            .AnyAsync(category => category.Slug == PendingCategorySlug && category.IsActive, cancellationToken);

        return ProductCodes.Select(code =>
        {
            expectedRows.TryGetValue(code, out var sourceMatches);
            var row = sourceMatches is { Length: 1 } ? sourceMatches[0] : null;
            var issues = WmcBimboProductRules.ValidateSourceRow(code, row).ToList();
            if (sourceMatches is { Length: > 1 })
                issues.Add(new("SOURCE_WMC_CODE_CONFLICT", "Código WMC duplicado no espelho."));

            localRows.TryGetValue(code, out var localMatches);
            if (localMatches is { Length: > 1 })
                issues.Add(new("LOCAL_WMC_CODE_CONFLICT", "Código WMC duplicado no cadastro Orofoods."));

            var existing = localMatches is { Length: 1 } ? localMatches[0] : null;
            if (existing is null && localProducts.Any(product => string.Equals(product.Sku, $"WMC-{code}", StringComparison.OrdinalIgnoreCase)))
                issues.Add(new("LOCAL_SKU_CONFLICT", "O SKU reservado para este código já pertence a outro produto."));
            if (categoryIsInvalid)
                issues.Add(new("PENDING_CATEGORY_ACTIVE", "A categoria pendente de importação está ativa e precisa ser revisada."));

            var skipReasons = existing is null ? Array.Empty<string>() : ["O produto já existe; os dados locais serão preservados."];
            return new WmcBimboProductSimulation(
                code,
                row?.Produto,
                row?.Ean,
                "PCT",
                row?.EstoqueAtual,
                issues.Count == 0 && existing is null,
                existing is not null,
                issues,
                skipReasons);
        }).ToArray();
    }

    private async Task<WmcBimboImportResult> RecordRejectedAsync(
        string actorUserId,
        DateTimeOffset startedAt,
        string errorCode,
        CancellationToken cancellationToken,
        string outcome = "Rejected",
        IEnumerable<WmcBimboProductSimulation>? issueDetails = null)
    {
        try
        {
            var details = issueDetails?.SelectMany(product => product.Issues.Select(issue => $"{product.WmcCode}:{issue.Code}")) ?? [];
            db.WmcProductImportRuns.Add(CreateAudit(actorUserId, startedAt, timeProvider.GetUtcNow(), outcome, [], [],
                string.Join(",", new[] { errorCode }.Concat(details).Take(60))));
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError("WMC BIMBO import audit write failed. ErrorType={ErrorType}", exception.GetType().Name);
            db.ChangeTracker.Clear();
        }

        var outcomes = issueDetails?.SelectMany(product => product.Issues.Select(issue => new WmcBimboProductOutcome(product.WmcCode, issue.Code, issue.Message))).ToArray() ?? [];
        return new(false, [], [], errorCode, outcomes);
    }

    private static WmcProductImportRun CreateAudit(
        string actorUserId,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        string outcome,
        IReadOnlyCollection<string> imported,
        IReadOnlyCollection<string> skipped,
        string? errorCode) => new()
    {
        BrandCode = WmcBimboProductRules.BrandCode,
        ActorUserId = actorUserId,
        StartedAtUtc = startedAt,
        CompletedAtUtc = completedAt,
        Outcome = outcome,
        ImportedProductCodes = string.Join(",", imported.Order(StringComparer.Ordinal)),
        SkippedProductCodes = string.Join(",", skipped.Order(StringComparer.Ordinal)),
        ErrorSummary = errorCode
    };

    private static WmcBimboProductSimulation FailedSimulation(string code, string errorCode, string message) =>
        new(code, null, null, "PCT", null, false, false, [new(errorCode, message)], []);
}

public sealed record WmcBimboProductOutcome(string WmcCode, string Status, string Message);

public sealed record WmcBimboImportResult(
    bool Succeeded,
    IReadOnlyList<string> ImportedWmcCodes,
    IReadOnlyList<string> SkippedWmcCodes,
    string? ErrorSummary,
    IReadOnlyList<WmcBimboProductOutcome> Products);
