using Orofoods.Web.Models.Catalog;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public static class WmcManualProductImportReadinessEvaluator
{
    public static WmcInitialLoadReadinessResult Evaluate(
        WmcProductRecord source,
        Product product,
        IReadOnlyCollection<WmcProductUnitRecord> units,
        bool unitsSnapshotComplete,
        int availableQuantity,
        bool inventoryManuallyAdjusted = false)
    {
        if (WmcProductRules.ClassifySituation(source.Situacao) != WmcProductSituation.Active || !product.IsWmcActive)
            return WmcInitialLoadReadinessResult.Blocked("wmc-inactive");

        if (!product.IsActive || !product.IsAvailable)
            return WmcInitialLoadReadinessResult.Blocked("local-product-disabled");

        if (source.PrecoVenda is not > 0)
            return WmcInitialLoadReadinessResult.Blocked("price-invalid");

        var unitValidation = WmcManualProductUnitValidation.Validate(source, units, unitsSnapshotComplete);
        if (!unitValidation.IsValid)
            return WmcInitialLoadReadinessResult.Blocked($"unit-invalid: {unitValidation.BlockReason}");

        if (inventoryManuallyAdjusted)
            return WmcInitialLoadReadinessResult.Blocked("inventory-manually-adjusted");

        if (!product.WmcStockAvailable || availableQuantity < product.MinimumSalesQuantity)
            return WmcInitialLoadReadinessResult.Blocked("stock-invalid");

        return WmcInitialLoadReadinessResult.Ready();
    }
}
