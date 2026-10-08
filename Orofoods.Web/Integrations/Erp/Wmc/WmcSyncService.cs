using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Inventory;

namespace Orofoods.Web.Integrations.Erp.Wmc;

/// <summary>
/// Reads customers/products/sellers/stock from the WMC Firebird mirror and reflects them into Orofoods'
/// own PostgreSQL entities, matched by WmcCode. Never writes back to Firebird. A read failure leaves all
/// existing PostgreSQL data untouched (nothing is saved for the entity type that failed).
/// </summary>
public sealed class WmcSyncService(
    ApplicationDbContext db,
    IWmcCustomerReader customerReader,
    IWmcProductReader productReader,
    IWmcSellerReader sellerReader,
    ILogger<WmcSyncService> logger,
    TimeProvider timeProvider)
{
    private const string PendingCategorySlug = "wmc-pendente-categorizacao";

    public async Task<WmcSyncEntityResult> SyncCustomersAsync(CancellationToken cancellationToken = default)
    {
        int read = 0, created = 0, updated = 0, skipped = 0;
        try
        {
            var rows = await customerReader.GetAllAsync(cancellationToken);
            read = rows.Count;
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.CodCliente))
                {
                    skipped++;
                    continue;
                }

                var existing = await db.Customers.FirstOrDefaultAsync(c => c.WmcCode == row.CodCliente, cancellationToken);
                if (existing is null)
                {
                    // WMC only confirms CODCLIENTE/NOME; Cnpj is required and unique in Orofoods but has no
                    // WMC source yet, so new customers are created Pending with a clearly non-real
                    // placeholder Cnpj until someone completes the real registration data.
                    db.Customers.Add(new Customer
                    {
                        WmcCode = row.CodCliente,
                        LegalName = row.Nome,
                        TradeName = row.Nome,
                        Cnpj = BuildPlaceholderCnpj(row.CodCliente),
                        Status = CustomerStatus.Pending,
                        IsActive = true
                    });
                    created++;
                }
                else if (!string.Equals(existing.TradeName, row.Nome, StringComparison.Ordinal))
                {
                    existing.TradeName = row.Nome;
                    updated++;
                }
                else
                {
                    skipped++;
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            return new WmcSyncEntityResult(read, created, updated, skipped, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao sincronizar clientes do WMC.");
            return new WmcSyncEntityResult(read, 0, 0, 0, ex.Message);
        }
    }

    public async Task<WmcSyncEntityResult> SyncProductsAsync(CancellationToken cancellationToken = default)
    {
        int read = 0, created = 0, updated = 0, skipped = 0;
        try
        {
            var rows = await productReader.GetAllAsync(cancellationToken);
            read = rows.Count;
            var duplicateCodes = FindDuplicateCodes(rows);
            if (duplicateCodes.Count > 0)
            {
                return new WmcSyncEntityResult(read, 0, 0, 0, $"CODPRODUTO duplicado na réplica: {string.Join(", ", duplicateCodes)}");
            }

            var distinctRows = rows.ToList();
            var products = await db.Products.ToListAsync(cancellationToken);
            var duplicateLocalCodes = products
                .Where(product => !string.IsNullOrWhiteSpace(product.WmcCode))
                .GroupBy(product => NormalizeWmcCode(product.WmcCode), StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();
            if (duplicateLocalCodes.Count > 0)
            {
                return new WmcSyncEntityResult(read, 0, 0, 0, $"WmcCode duplicado no PostgreSQL: {string.Join(", ", duplicateLocalCodes)}");
            }

            var pendingCategoryId = await GetOrCreatePendingCategoryIdAsync(cancellationToken);
            var byWmcCode = products
                .Where(product => !string.IsNullOrWhiteSpace(product.WmcCode))
                .GroupBy(product => NormalizeWmcCode(product.WmcCode), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            foreach (var row in distinctRows)
            {
                var code = row.CodProduto.Trim();
                if (string.IsNullOrWhiteSpace(code))
                {
                    skipped++;
                    continue;
                }

                byWmcCode.TryGetValue(NormalizeWmcCode(code), out var existing);
                if (existing is null)
                {
                    existing = new Product
                    {
                        WmcCode = code,
                        Ean = Clean(row.Ean),
                        WmcBrandCode = Clean(row.CodMarca),
                        WmcAlternateUnit = Clean(row.UnAltern),
                        WmcPackageQuantity = row.QtdeEmbalagem,
                        WmcConversionQuantity = row.QtdeConversao,
                        Sku = BuildPlaceholderSku(row.CodProduto),
                        Name = row.Produto,
                        Unit = string.IsNullOrWhiteSpace(row.Un) ? "caixa" : row.Un,
                        ProductCategoryId = pendingCategoryId,
                        IsActive = false,
                        IsAvailable = false,
                        IsWmcActive = ReadWmcActive(row.Situacao, code),
                        WmcStockAvailable = false
                    };
                    db.Products.Add(existing);
                    byWmcCode[NormalizeWmcCode(code)] = existing;
                    created++;
                }
                else
                {
                    var changed = false;
                    if (!string.Equals(existing.Name, row.Produto, StringComparison.Ordinal))
                    {
                        existing.Name = row.Produto;
                        changed = true;
                    }
                    var ean = Clean(row.Ean);
                    if (!string.Equals(existing.Ean, ean, StringComparison.Ordinal))
                    {
                        existing.Ean = ean;
                        changed = true;
                    }
                    var brandCode = Clean(row.CodMarca);
                    if (!string.Equals(existing.WmcBrandCode, brandCode, StringComparison.Ordinal))
                    {
                        existing.WmcBrandCode = brandCode;
                        changed = true;
                    }
                    var alternateUnit = Clean(row.UnAltern);
                    if (!string.Equals(existing.WmcAlternateUnit, alternateUnit, StringComparison.Ordinal))
                    {
                        existing.WmcAlternateUnit = alternateUnit;
                        changed = true;
                    }
                    if (existing.WmcPackageQuantity != row.QtdeEmbalagem)
                    {
                        existing.WmcPackageQuantity = row.QtdeEmbalagem;
                        changed = true;
                    }
                    if (existing.WmcConversionQuantity != row.QtdeConversao)
                    {
                        existing.WmcConversionQuantity = row.QtdeConversao;
                        changed = true;
                    }
                    var wmcActive = ReadWmcActive(row.Situacao, code);
                    if (existing.IsWmcActive != wmcActive)
                    {
                        existing.IsWmcActive = wmcActive;
                        changed = true;
                    }
                    if (!string.IsNullOrWhiteSpace(row.Un) && !string.Equals(existing.Unit, row.Un.Trim(), StringComparison.Ordinal))
                    {
                        existing.Unit = row.Un.Trim();
                        changed = true;
                    }
                    if (changed) updated++; else skipped++;
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            return new WmcSyncEntityResult(read, created, updated, skipped, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao sincronizar produtos do WMC.");
            return new WmcSyncEntityResult(read, 0, 0, 0, ex.Message);
        }
    }

    public async Task<WmcSyncEntityResult> SyncStockAsync(CancellationToken cancellationToken = default)
    {
        int read = 0, created = 0, updated = 0, skipped = 0;
        try
        {
            var rows = await productReader.GetAllAsync(cancellationToken);
            read = rows.Count;
            var duplicateCodes = FindDuplicateCodes(rows);
            if (duplicateCodes.Count > 0)
            {
                return new WmcSyncEntityResult(read, 0, 0, 0, $"CODPRODUTO duplicado na réplica: {string.Join(", ", duplicateCodes)}");
            }

            var distinctRows = rows.ToList();
            var products = await db.Products
                .Where(product => product.WmcCode != null)
                .ToDictionaryAsync(product => NormalizeWmcCode(product.WmcCode), StringComparer.Ordinal, cancellationToken);
            var productIds = products.Values.Select(product => product.Id).ToArray();
            var inventories = await db.ProductInventories
                .Where(inventory => productIds.Contains(inventory.ProductId))
                .ToDictionaryAsync(inventory => inventory.ProductId, cancellationToken);
            foreach (var row in distinctRows)
            {
                if (!products.TryGetValue(NormalizeWmcCode(row.CodProduto), out var product))
                {
                    skipped++; // Product not yet linked locally; stock has nothing to attach to.
                    continue;
                }

                if (!TryNormalizeStock(row.EstoqueAtual, out var quantity, out var warning))
                {
                    product.WmcStockAvailable = false;
                    logger.LogWarning("Estoque WMC inválido para {WmcCode}: {Reason}", product.WmcCode, warning);
                    skipped++;
                    continue;
                }

                if (!string.IsNullOrEmpty(warning))
                {
                    logger.LogWarning("Estoque WMC ajustado para {WmcCode}: {Reason}", product.WmcCode, warning);
                }

                var inventory = inventories.GetValueOrDefault(product.Id);
                if (inventory is null)
                {
                    inventory = new ProductInventory { ProductId = product.Id, QuantityOnHand = quantity };
                    db.ProductInventories.Add(inventory);
                    inventories[product.Id] = inventory;
                    created++;
                }
                else if (inventory.QuantityOnHand != quantity)
                {
                    inventory.QuantityOnHand = quantity;
                    updated++;
                }
                product.WmcStockAvailable = quantity > 0;
                if (inventory.QuantityOnHand == quantity && quantity == 0)
                {
                    skipped++;
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            return new WmcSyncEntityResult(read, created, updated, skipped, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao sincronizar estoque do WMC.");
            return new WmcSyncEntityResult(read, 0, 0, 0, ex.Message);
        }
    }

    public async Task<WmcSyncEntityResult> SyncSellersAsync(CancellationToken cancellationToken = default)
    {
        var rows = await sellerReader.GetAllAsync(cancellationToken);
        return new WmcSyncEntityResult(rows.Count, 0, 0, rows.Count, null);
    }

    public async Task<WmcSyncRunResult> SyncAllAsync(CancellationToken cancellationToken = default)
    {
        var startedAt = timeProvider.GetUtcNow().UtcDateTime;
        var initialLoad = !await db.WmcSyncRuns.AnyAsync(run => run.Status == "Succeeded", cancellationToken);
        var run = new Models.Integrations.WmcSyncRun { StartedAt = startedAt, InitialLoad = initialLoad, Status = "Running" };
        db.WmcSyncRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);

        var customers = await SyncCustomersAsync(cancellationToken);
        run.CustomersRead = customers.RecordsRead;
        if (customers.ErrorMessage is not null) return await FailRunAsync(run, "Customers", customers.ErrorMessage, startedAt, customers, WmcSyncEntityResult.Empty, WmcSyncEntityResult.Empty, WmcSyncEntityResult.Empty, cancellationToken);
        var products = await SyncProductsAsync(cancellationToken);
        run.ProductsRead = products.RecordsRead;
        run.ProductsCreated = products.RecordsCreated;
        run.ProductsUpdated = products.RecordsUpdated;
        if (products.ErrorMessage is not null) return await FailRunAsync(run, "Products", products.ErrorMessage, startedAt, customers, products, WmcSyncEntityResult.Empty, WmcSyncEntityResult.Empty, cancellationToken);
        var sellers = await SyncSellersAsync(cancellationToken);
        run.SellersRead = sellers.RecordsRead;
        if (sellers.ErrorMessage is not null) return await FailRunAsync(run, "Sellers", sellers.ErrorMessage, startedAt, customers, products, sellers, WmcSyncEntityResult.Empty, cancellationToken);
        var stock = await SyncStockAsync(cancellationToken);
        run.StockRead = stock.RecordsRead;
        run.StockCreated = stock.RecordsCreated;
        run.StockUpdated = stock.RecordsUpdated;
        if (stock.ErrorMessage is not null) return await FailRunAsync(run, "Stock", stock.ErrorMessage, startedAt, customers, products, sellers, stock, cancellationToken);

        var wmcProducts = await db.Products.Where(product => product.WmcCode != null).ToListAsync(cancellationToken);
        foreach (var product in wmcProducts) product.WmcInitialLoadReady = true;
        run.Status = "Succeeded";
        run.FinishedAt = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        return new WmcSyncRunResult(startedAt, run.FinishedAt.Value, customers, products, sellers, stock, run.Status);
    }

    private async Task<WmcSyncRunResult> FailRunAsync(WmcSyncRun run, string stage, string error, DateTime startedAt, WmcSyncEntityResult customers, WmcSyncEntityResult products, WmcSyncEntityResult sellers, WmcSyncEntityResult stock, CancellationToken cancellationToken)
    {
        run.Status = "Failed";
        run.FailedStage = stage;
        run.ErrorMessage = error;
        run.FinishedAt = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        return new WmcSyncRunResult(startedAt, run.FinishedAt.Value, customers, products, sellers, stock, run.Status);
    }

    private async Task<int> GetOrCreatePendingCategoryIdAsync(CancellationToken cancellationToken)
    {
        var category = await db.ProductCategories.FirstOrDefaultAsync(c => c.Slug == PendingCategorySlug, cancellationToken);
        if (category is not null)
        {
            return category.Id;
        }

        category = new ProductCategory { Name = "A categorizar (WMC)", Slug = PendingCategorySlug, IsActive = false };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return category.Id;
    }

    private static string BuildPlaceholderCnpj(string codCliente)
    {
        var value = $"WMC-{codCliente}";
        return value.Length > 18 ? value[..18] : value;
    }

    private static string BuildPlaceholderSku(string codProduto)
    {
        var value = $"WMC-{codProduto}";
        return value.Length > 30 ? value[..30] : value;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private bool ReadWmcActive(string? situation, string code) => situation?.Trim().ToUpperInvariant() switch
    {
        "A" => true,
        "I" => false,
        _ => LogUnknownSituation(code)
    };

    private bool LogUnknownSituation(string code)
    {
        logger.LogWarning("Situação WMC desconhecida para {WmcCode}; produto bloqueado.", code);
        return false;
    }

    private static List<string> FindDuplicateCodes(IEnumerable<WmcProductRecord> rows) => rows
        .Where(row => !string.IsNullOrWhiteSpace(row.CodProduto))
        .GroupBy(row => NormalizeWmcCode(row.CodProduto), StringComparer.Ordinal)
        .Where(group => group.Count() > 1)
        .Select(group => group.Key)
        .ToList();

    private static string NormalizeWmcCode(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    private static bool TryNormalizeStock(decimal? value, out int quantity, out string reason)
    {
        quantity = 0;
        reason = "valor ausente";
        if (!value.HasValue)
        {
            return false;
        }

        if (value.Value < 0)
        {
            reason = "valor negativo; disponibilidade definida como zero";
            return true;
        }

        if (decimal.Truncate(value.Value) != value.Value || value.Value > int.MaxValue)
        {
            reason = "valor fracionário ou fora do intervalo suportado";
            return false;
        }

        quantity = (int)value.Value;
        reason = "";
        return true;
    }
}
