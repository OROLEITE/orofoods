using Orofoods.Web.Models.Catalog;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed record WmcInitialLoadReadinessResult(bool IsReady, string? Reason)
{
    public static WmcInitialLoadReadinessResult Ready() => new(true, null);
    public static WmcInitialLoadReadinessResult Blocked(string reason) => new(false, reason);
}

public static class WmcInitialLoadReadinessEvaluator
{
    public static WmcInitialLoadReadinessResult Evaluate(
        WmcProductRecord source,
        Product product,
        IReadOnlyCollection<WmcProductUnitRecord> units,
        int availableQuantity,
        bool inventoryManuallyAdjusted = false)
    {
        if (WmcProductRules.ClassifySituation(source.Situacao) != WmcProductSituation.Active || !product.IsWmcActive)
            return WmcInitialLoadReadinessResult.Blocked("wmc-inactive");

        if (!product.IsActive || !product.IsAvailable)
            return WmcInitialLoadReadinessResult.Blocked("local-product-disabled");

        if (source.PrecoVenda is not > 0)
            return WmcInitialLoadReadinessResult.Blocked("price-invalid");

        if (!IsValidUnitConfiguration(source, product, units))
            return WmcInitialLoadReadinessResult.Blocked("unit-invalid");

        if (!IsValidPackaging(source, product))
            return WmcInitialLoadReadinessResult.Blocked("packaging-invalid");

        if (inventoryManuallyAdjusted)
            return WmcInitialLoadReadinessResult.Blocked("inventory-manually-adjusted");

        if (!product.WmcStockAvailable || availableQuantity < product.MinimumSalesQuantity)
            return WmcInitialLoadReadinessResult.Blocked("stock-invalid");

        return WmcInitialLoadReadinessResult.Ready();
    }

    private static bool IsValidUnitConfiguration(
        WmcProductRecord source,
        Product product,
        IReadOnlyCollection<WmcProductUnitRecord> units)
    {
        if (string.IsNullOrWhiteSpace(source.Un) || string.IsNullOrWhiteSpace(product.Unit))
            return false;

        var grouped = units
            .GroupBy(unit => Normalize(unit.UnitCode), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        if (grouped.Count != units.Count || !grouped.TryGetValue("PC", out var pcs) || pcs.Length != 1
            || !grouped.TryGetValue("CX", out var cxs) || cxs.Length != 1 || !cxs[0].IsMaster)
            return false;

        var pc = pcs[0];
        var cx = cxs[0];
        return pc.Factor is > 0
            && cx.Factor is > 0
            && IsValidBarcode(pc.Barcode, 13)
            && IsValidBarcode(cx.Barcode, 14);
    }

    private static bool IsValidPackaging(WmcProductRecord source, Product product)
    {
        var commercialUnit = Normalize(product.CommercialUnitCode ?? source.CommercialUnitCode ?? product.Unit);
        if (commercialUnit.Length == 0)
            return false;
        var logisticsUnit = Normalize(product.LogisticsUnitCode ?? source.LogisticsUnitCode);
        if (logisticsUnit.Length == 0)
            return true;
        var packagesPerCase = product.PackagesPerCase ?? source.PackagesPerCase;
        return string.Equals(logisticsUnit, "CX", StringComparison.OrdinalIgnoreCase)
            && packagesPerCase is > 0;
    }

    private static bool IsValidBarcode(string? value, int length)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var digits = value.Trim();
        if (digits.Length != length || digits.Any(character => character is < '0' or > '9')) return false;
        var sum = 0;
        for (var index = digits.Length - 2; index >= 0; index--)
        {
            var positionFromRight = digits.Length - 1 - index;
            sum += (digits[index] - '0') * (positionFromRight % 2 == 1 ? 3 : 1);
        }
        return (10 - (sum % 10)) % 10 == digits[^1] - '0';
    }

    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();
}
