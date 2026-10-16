using System.Data;
using System.ComponentModel.DataAnnotations;
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
    public const short BrandCode = 104;
    // Homologation guard only. Imported values are always persisted from row.Ean (CODBARRASFAB).
    private static readonly IReadOnlyDictionary<string, string> Ean13HomologationReferences = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["503768"] = "7899679700365",
        ["503775"] = "7899679701478",
        ["503777"] = "7899679701751",
        ["503805"] = "7899679700327",
        ["503811"] = "7899679701072",
        ["503813"] = "7899679701348",
        ["503833"] = "7899679702130",
        ["503838"] = "7899679702659",
        ["504338"] = "7899679702307",
        ["504342"] = "7899679704936"
    };
    private const string PendingCategorySlug = "wmc-pendente-categorizacao";
    private static readonly string[] ProductCodes =
    ["503768", "503775", "503777", "503805", "503811", "503813", "503833", "503838", "504338", "504342"];
    private static readonly IReadOnlyList<string> ApprovedCodeList = Array.AsReadOnly(ProductCodes);
    private static readonly HashSet<string> ProductCodeSet = ProductCodes.ToHashSet(StringComparer.Ordinal);

    public static IReadOnlyList<string> ApprovedProductCodes => ApprovedCodeList;

    public async Task<WmcBimboImportResult> ImportAsync(string actorUserId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorUserId);
        var startedAt = timeProvider.GetUtcNow();
        IReadOnlyList<WmcProductRecord> rows;
        try
        {
            rows = await productReader.GetAllByBrandAsync(BrandCode, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError("WMC BIMBO product read failed. ErrorType={ErrorType}", exception.GetType().Name);
            return await RecordRejectedAsync(actorUserId, startedAt, "WMC_READ_FAILED", cancellationToken, outcome: "Failed");
        }

        var validation = ValidateRows(rows);
        if (validation.ErrorCode is not null)
        {
            return await RecordRejectedAsync(actorUserId, startedAt, validation.ErrorCode, cancellationToken);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var imported = new List<string>();
            var skipped = new List<string>();
            var targetRows = validation.Rows!;
            var localProducts = await db.Products
                .Where(product => product.WmcCode != null)
                .ToListAsync(cancellationToken);
            var localByCode = localProducts
                .Where(product => ProductCodeSet.Contains(WmcProductRules.NormalizeCode(product.WmcCode)))
                .GroupBy(product => WmcProductRules.NormalizeCode(product.WmcCode), StringComparer.Ordinal)
                .ToList();
            if (localByCode.Any(group => group.Count() > 1))
            {
                await transaction.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();
                return await RecordRejectedAsync(actorUserId, startedAt, "LOCAL_WMC_CODE_CONFLICT", cancellationToken);
            }

            var localByNormalizedCode = localByCode.ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
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

            foreach (var row in targetRows)
            {
                var code = WmcProductRules.NormalizeCode(row.CodProduto);
                if (localByNormalizedCode.ContainsKey(code))
                {
                    skipped.Add(code);
                    continue;
                }

                var stock = decimal.ToInt32(row.EstoqueAtual!.Value);
                var product = new Product
                {
                    WmcCode = code,
                    WmcBrandCode = BrandCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
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
                localByNormalizedCode.Add(code, product);
            }

            var completedAt = timeProvider.GetUtcNow();
            db.WmcProductImportRuns.Add(CreateAudit(actorUserId, startedAt, completedAt, "Succeeded", imported, skipped, null));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new WmcBimboImportResult(true, imported, skipped, null);
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

    private async Task<WmcBimboImportResult> RecordRejectedAsync(
        string actorUserId,
        DateTimeOffset startedAt,
        string errorCode,
        CancellationToken cancellationToken,
        string outcome = "Rejected")
    {
        try
        {
            db.WmcProductImportRuns.Add(CreateAudit(actorUserId, startedAt, timeProvider.GetUtcNow(), outcome, [], [], errorCode));
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError("WMC BIMBO import audit write failed. ErrorType={ErrorType}", exception.GetType().Name);
            db.ChangeTracker.Clear();
        }

        return new WmcBimboImportResult(false, [], [], errorCode);
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
        BrandCode = BrandCode,
        ActorUserId = actorUserId,
        StartedAtUtc = startedAt,
        CompletedAtUtc = completedAt,
        Outcome = outcome,
        ImportedProductCodes = string.Join(",", imported.Order(StringComparer.Ordinal)),
        SkippedProductCodes = string.Join(",", skipped.Order(StringComparer.Ordinal)),
        ErrorSummary = errorCode
    };

    private static RowValidation ValidateRows(IReadOnlyList<WmcProductRecord> rows)
    {
        var targetRows = rows
            .Where(row => ProductCodeSet.Contains(WmcProductRules.NormalizeCode(row.CodProduto)))
            .ToList();
        if (WmcProductRules.FindDuplicateCodes(targetRows).Count > 0)
        {
            return RowValidation.Invalid("SOURCE_WMC_CODE_CONFLICT");
        }

        var byCode = targetRows.ToDictionary(row => WmcProductRules.NormalizeCode(row.CodProduto), StringComparer.Ordinal);
        if (ProductCodes.Any(code => !byCode.ContainsKey(code)))
        {
            return RowValidation.Invalid("APPROVED_PRODUCT_MISSING");
        }

        foreach (var code in ProductCodes)
        {
            var row = byCode[code];
            if (!string.Equals(row.CodMarca?.Trim(), BrandCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal))
            {
                return RowValidation.Invalid("SOURCE_BRAND_MISMATCH");
            }

            if (WmcProductRules.ClassifySituation(row.Situacao) != WmcProductSituation.Active)
            {
                return RowValidation.Invalid("SOURCE_PRODUCT_NOT_ACTIVE");
            }

            if (!string.Equals(row.Un?.Trim(), "PC", StringComparison.OrdinalIgnoreCase))
            {
                return RowValidation.Invalid("SOURCE_STOCK_UNIT_MISMATCH");
            }

            if (string.IsNullOrWhiteSpace(row.Produto) || row.Produto.Trim().Length > 140)
            {
                return RowValidation.Invalid("SOURCE_PRODUCT_NAME_INVALID");
            }

            var ean13 = row.Ean?.Trim();
            var barcodeValidationResults = new List<ValidationResult>();
            if (string.IsNullOrWhiteSpace(ean13)
                || !Validator.TryValidateValue(
                    ean13,
                    new ValidationContext(new object()),
                    barcodeValidationResults,
                    [new Gs1BarcodeAttribute(13)]))
            {
                return RowValidation.Invalid("SOURCE_EAN13_INVALID");
            }

            if (!string.Equals(ean13, Ean13HomologationReferences[code], StringComparison.Ordinal))
            {
                return RowValidation.Invalid("SOURCE_EAN13_MISMATCH");
            }

            if (row.EstoqueAtual is not decimal stock || stock < 0 || decimal.Truncate(stock) != stock || stock > int.MaxValue)
            {
                return RowValidation.Invalid("SOURCE_STOCK_INVALID");
            }
        }

        return RowValidation.Valid(ProductCodes.Select(code => byCode[code]).ToArray());
    }

    private sealed record RowValidation(IReadOnlyList<WmcProductRecord>? Rows, string? ErrorCode)
    {
        public static RowValidation Valid(IReadOnlyList<WmcProductRecord> rows) => new(rows, null);
        public static RowValidation Invalid(string errorCode) => new(null, errorCode);
    }
}

public sealed record WmcBimboImportResult(
    bool Succeeded,
    IReadOnlyList<string> ImportedWmcCodes,
    IReadOnlyList<string> SkippedWmcCodes,
    string? ErrorSummary);
