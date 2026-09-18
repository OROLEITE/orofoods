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
            var pendingCategoryId = await GetOrCreatePendingCategoryIdAsync(cancellationToken);
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.CodProduto))
                {
                    skipped++;
                    continue;
                }

                var existing = await db.Products.FirstOrDefaultAsync(p => p.WmcCode == row.CodProduto, cancellationToken);
                if (existing is null)
                {
                    // ProductCategoryId is required in Orofoods but WMC has no confirmed category mapping,
                    // and P.PRECOCOMPRA must not be assumed as sale price (see spec section 23). New
                    // products land inactive/unavailable in a pending category until a human categorizes
                    // and prices them.
                    db.Products.Add(new Product
                    {
                        WmcCode = row.CodProduto,
                        Sku = BuildPlaceholderSku(row.CodProduto),
                        Name = row.Produto,
                        Unit = string.IsNullOrWhiteSpace(row.Un) ? "caixa" : row.Un,
                        ProductCategoryId = pendingCategoryId,
                        IsActive = false,
                        IsAvailable = false
                    });
                    created++;
                }
                else if (!string.Equals(existing.Name, row.Produto, StringComparison.Ordinal))
                {
                    existing.Name = row.Produto;
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
            foreach (var row in rows)
            {
                if (row.EstoqueAtual is null)
                {
                    skipped++;
                    continue;
                }

                var product = await db.Products.FirstOrDefaultAsync(p => p.WmcCode == row.CodProduto, cancellationToken);
                if (product is null)
                {
                    skipped++; // Product not yet linked locally; stock has nothing to attach to.
                    continue;
                }

                var inventory = await db.ProductInventories.FirstOrDefaultAsync(i => i.ProductId == product.Id, cancellationToken);
                if (inventory is null)
                {
                    db.ProductInventories.Add(new ProductInventory { ProductId = product.Id, QuantityOnHand = row.EstoqueAtual.Value });
                    created++;
                }
                else if (inventory.QuantityOnHand != row.EstoqueAtual.Value)
                {
                    inventory.QuantityOnHand = row.EstoqueAtual.Value;
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
        var customers = await SyncCustomersAsync(cancellationToken);
        var products = await SyncProductsAsync(cancellationToken);
        var sellers = await SyncSellersAsync(cancellationToken);
        var stock = await SyncStockAsync(cancellationToken);
        return new WmcSyncRunResult(startedAt, timeProvider.GetUtcNow().UtcDateTime, customers, products, sellers, stock);
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
}
