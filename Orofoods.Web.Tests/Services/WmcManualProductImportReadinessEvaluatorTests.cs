using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Catalog;

namespace Orofoods.Web.Tests.Services;

public sealed class WmcManualProductImportReadinessEvaluatorTests
{
    [Fact]
    public void Accepts_active_avicao_data_without_pc_or_pct_cx_configuration()
    {
        var product = Product();
        product.Unit = "UN";
        product.CommercialUnitCode = null;
        product.LogisticsUnitCode = null;
        product.PackagesPerCase = null;
        var source = new WmcProductRecord(
            "AV-001", "Biscoito", "A", "UN", 5, 5,
            Ean: "7899679701348", UnAltern: "CX", PrecoVenda: 10,
            QtdeEmbalagem: 24, QtdeConversao: 24);

        var result = WmcManualProductImportReadinessEvaluator.Evaluate(
            source,
            product,
            [
                new("UN", "UNIDADE", 1m, "7899679701348", false),
                new("CX", "CAIXA", 24m, "17899679701345", true)
            ],
            unitsSnapshotComplete: true,
            availableQuantity: 5,
            inventoryManuallyAdjusted: false);

        Assert.True(result.IsReady);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void Keeps_inactive_products_blocked_in_manual_mode()
    {
        var result = WmcManualProductImportReadinessEvaluator.Evaluate(
            new WmcProductRecord("P", "Produto", "I", "UN", 5, 5, PrecoVenda: 10),
            Product(),
            [new("UN", "UNIDADE", 1m, "7899679701348", false)],
            unitsSnapshotComplete: true,
            availableQuantity: 5,
            inventoryManuallyAdjusted: false);

        Assert.False(result.IsReady);
        Assert.Equal("wmc-inactive", result.Reason);
    }

    private static Product Product() => new()
    {
        WmcCode = "P",
        Name = "Produto",
        Unit = "UN",
        BasePrice = 10,
        IsActive = true,
        IsAvailable = true,
        IsWmcActive = true,
        WmcStockAvailable = true,
        MinimumCases = 1
    };
}
