using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Integrations;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcProductPreviewService(ApplicationDbContext db, IWmcProductReader productReader)
{
    public async Task<WmcProductPreviewResult> PreviewAsync(short? brandCode, CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await productReader.GetAllByBrandAsync(brandCode, cancellationToken);
            var products = await db.Products.AsNoTracking().ToListAsync(cancellationToken);
            var localCodes = products
                .Where(product => !string.IsNullOrWhiteSpace(product.WmcCode))
                .Select(product => WmcProductRules.NormalizeCode(product.WmcCode))
                .ToHashSet(StringComparer.Ordinal);
            var duplicateCodes = WmcProductRules.FindDuplicateCodes(rows).ToHashSet(StringComparer.Ordinal);
            var localDuplicateCodes = products
                .Where(product => !string.IsNullOrWhiteSpace(product.WmcCode))
                .GroupBy(product => WmcProductRules.NormalizeCode(product.WmcCode), StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);
            duplicateCodes.UnionWith(localDuplicateCodes);

            var active = 0;
            var inactive = 0;
            var unknown = 0;
            var invalidCodes = 0;
            var stockProblems = 0;
            var projectedAvailable = 0;

            var activeRows = rows.Where(row => WmcProductRules.ClassifySituation(row.Situacao) == WmcProductSituation.Active).ToList();
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.CodProduto))
                {
                    invalidCodes++;
                }

                switch (WmcProductRules.ClassifySituation(row.Situacao))
                {
                    case WmcProductSituation.Active:
                        active++;
                        break;
                    case WmcProductSituation.Inactive:
                        inactive++;
                        break;
                    default:
                        unknown++;
                        break;
                }

                if (WmcProductRules.ClassifySituation(row.Situacao) == WmcProductSituation.Active
                    && (!WmcProductRules.TryNormalizeStock(row.EstoqueAtual, out _, out _)
                    || row.EstoqueAtual is null
                    || row.EstoqueAtual < 0))
                {
                    stockProblems++;
                }
                else if (WmcProductRules.ClassifySituation(row.Situacao) == WmcProductSituation.Active
                    && WmcProductRules.TryNormalizeStock(row.EstoqueAtual, out var availableQuantity, out _)
                    && availableQuantity > 0
                    && !duplicateCodes.Contains(WmcProductRules.NormalizeCode(row.CodProduto))
                    && !string.IsNullOrWhiteSpace(row.CodProduto))
                {
                    projectedAvailable++;
                }
            }

            var activeValidRows = activeRows.Where(row => !string.IsNullOrWhiteSpace(row.CodProduto)).ToList();
            var newProducts = activeValidRows.Count(row => !localCodes.Contains(WmcProductRules.NormalizeCode(row.CodProduto)));
            var existingProducts = activeValidRows.Count - newProducts;
            var blocked = rows.Count - projectedAvailable;

            return new WmcProductPreviewResult(
                rows.Count,
                newProducts,
                existingProducts,
                active,
                inactive,
                unknown,
                invalidCodes,
                duplicateCodes.Count,
                stockProblems,
                projectedAvailable,
                blocked,
                activeValidRows.Count,
                inactive,
                unknown,
                newProducts,
                existingProducts);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new WmcProductPreviewResult(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "Não foi possível consultar a prévia WMC.");
        }
    }
}
