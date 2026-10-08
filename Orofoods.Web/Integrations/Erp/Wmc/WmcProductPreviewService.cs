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
            return (await BuildPreviewDetailsAsync(rows, cancellationToken)).Preview;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ErrorResult();
        }
    }

    public async Task<WmcProductPreviewBatchResult> PreviewByBrandsAsync(
        IReadOnlyCollection<short> brandCodes,
        CancellationToken cancellationToken = default)
    {
        if (brandCodes.Count == 0)
        {
            return new WmcProductPreviewBatchResult(await PreviewAsync(null, cancellationToken), []);
        }

        var selectedCodes = brandCodes.Distinct().ToArray();
        var rowsByBrand = new Dictionary<short, IReadOnlyList<WmcProductRecord>>();
        try
        {
            foreach (var brandCode in selectedCodes)
            {
                rowsByBrand[brandCode] = await productReader.GetAllByBrandAsync(brandCode, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new WmcProductPreviewBatchResult(ErrorResult(), []);
        }

        var allRows = rowsByBrand.Values.SelectMany(rows => rows).ToList();
        var summary = (await BuildPreviewDetailsAsync(allRows, cancellationToken)).Preview;
        var previews = new List<WmcBrandPreviewResult>(selectedCodes.Length);
        foreach (var brandCode in selectedCodes)
        {
            var details = await BuildPreviewDetailsAsync(rowsByBrand[brandCode], cancellationToken);
            previews.Add(new WmcBrandPreviewResult(
                brandCode,
                string.Empty,
                details.Preview,
                details.Products));
        }

        return new WmcProductPreviewBatchResult(summary, previews);
    }

    private async Task<PreviewDetails> BuildPreviewDetailsAsync(
        IReadOnlyList<WmcProductRecord> rows,
        CancellationToken cancellationToken)
    {
        try
        {
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

            var preview = new WmcProductPreviewResult(
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
            var productItems = activeValidRows
                .Select(row => new WmcProductPreviewItem(
                    WmcProductRules.NormalizeCode(row.CodProduto),
                    row.Produto,
                    row.EstoqueAtual,
                    duplicateCodes.Contains(WmcProductRules.NormalizeCode(row.CodProduto))
                        ? "Conflito"
                        : localCodes.Contains(WmcProductRules.NormalizeCode(row.CodProduto))
                            ? "Existente"
                            : "Novo"))
                .ToList();

            return new PreviewDetails(preview, productItems);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new PreviewDetails(ErrorResult(), []);
        }
    }

    private sealed record PreviewDetails(
        WmcProductPreviewResult Preview,
        IReadOnlyList<WmcProductPreviewItem> Products);

    private static WmcProductPreviewResult Combine(IEnumerable<WmcProductPreviewResult> previews)
    {
        var values = previews.ToList();
        if (values.Count == 0)
        {
            return new WmcProductPreviewResult(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        return new WmcProductPreviewResult(
            values.Sum(item => item.ProductsRead),
            values.Sum(item => item.NewProducts),
            values.Sum(item => item.ExistingProducts),
            values.Sum(item => item.ActiveProducts),
            values.Sum(item => item.InactiveProducts),
            values.Sum(item => item.UnknownSituations),
            values.Sum(item => item.InvalidCodes),
            values.Sum(item => item.DuplicateCodes),
            values.Sum(item => item.StockProblems),
            values.Sum(item => item.ProjectedAvailableProducts),
            values.Sum(item => item.ProjectedBlockedProducts),
            values.Sum(item => item.ActiveEligibleProducts),
            values.Sum(item => item.InactiveIgnoredProducts),
            values.Sum(item => item.InvalidSituations),
            values.Sum(item => item.NewActiveProducts),
            values.Sum(item => item.ExistingActiveProducts),
            values.FirstOrDefault(item => item.ErrorMessage is not null)?.ErrorMessage);
    }

    private static WmcProductPreviewResult ErrorResult() =>
        new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "Não foi possível consultar a prévia WMC.");
}
