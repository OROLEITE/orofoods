using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Catalog;

namespace Orofoods.Web.Tests.Services;

public sealed class WmcInitialLoadReadinessEvaluatorTests
{
    [Fact]
    public void Valid_active_product_with_price_units_and_stock_is_ready()
    {
        var result = Evaluate();

        Assert.True(result.IsReady);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void Valid_source_unit_is_enough_when_optional_local_commercial_codes_are_absent()
    {
        var product = Product();
        product.CommercialUnitCode = null;
        product.LogisticsUnitCode = null;
        product.PackagesPerCase = null;

        var result = Evaluate(product: product);

        Assert.True(result.IsReady);
    }

    [Fact]
    public void Inactive_wmc_product_is_blocked()
    {
        var result = Evaluate(new WmcProductRecord("P", "Produto", "I", "PC", 5, 5, PrecoVenda: 10));

        Assert.False(result.IsReady);
        Assert.Equal("wmc-inactive", result.Reason);
    }

    [Fact]
    public void Invalid_price_is_blocked()
    {
        var result = Evaluate(new WmcProductRecord("P", "Produto", "A", "PC", 5, 5, PrecoVenda: 0));

        Assert.False(result.IsReady);
        Assert.Equal("price-invalid", result.Reason);
    }

    [Fact]
    public void Incomplete_units_are_blocked()
    {
        var result = Evaluate(units: []);

        Assert.False(result.IsReady);
        Assert.Equal("unit-invalid", result.Reason);
    }

    [Fact]
    public void Invalid_packaging_is_blocked()
    {
        var product = Product();
        product.PackagesPerCase = 0;
        var result = Evaluate(product: product);

        Assert.False(result.IsReady);
        Assert.Equal("packaging-invalid", result.Reason);
    }

    [Fact]
    public void Stock_below_minimum_is_blocked()
    {
        var product = Product();
        product.CommercialUnitCode = "CX";
        product.MinimumCases = 2;
        var result = Evaluate(product: product, availableQuantity: 1);

        Assert.False(result.IsReady);
        Assert.Equal("stock-invalid", result.Reason);
    }

    [Fact]
    public void Manual_inventory_adjustment_is_not_released()
    {
        var result = Evaluate(inventoryManuallyAdjusted: true);

        Assert.False(result.IsReady);
        Assert.Equal("inventory-manually-adjusted", result.Reason);
    }

    private static WmcInitialLoadReadinessResult Evaluate(
        WmcProductRecord? source = null,
        Product? product = null,
        IReadOnlyCollection<WmcProductUnitRecord>? units = null,
        int availableQuantity = 5,
        bool inventoryManuallyAdjusted = false)
    {
        source ??= new WmcProductRecord("P", "Produto", "A", "PC", 5, 5, PrecoVenda: 10);
        product ??= Product();
        units ??= ValidUnits();
        return WmcInitialLoadReadinessEvaluator.Evaluate(source, product, units, availableQuantity, inventoryManuallyAdjusted);
    }

    private static Product Product() => new()
    {
        WmcCode = "P",
        Name = "Produto",
        Unit = "PC",
        CommercialUnitCode = "PCT",
        LogisticsUnitCode = "CX",
        PackagesPerCase = 18,
        MinimumCases = 1,
        BasePrice = 10,
        IsActive = true,
        IsAvailable = true,
        IsWmcActive = true,
        WmcStockAvailable = true
    };

    private static IReadOnlyCollection<WmcProductUnitRecord> ValidUnits() =>
    [
        new("PC", "PACOTE", 1, "7899679701348", false),
        new("CX", "CAIXA", 18, "17899679701345", true)
    ];
}
