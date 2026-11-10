using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Inventory;

namespace Orofoods.Web.Tests.Integrations;

public sealed class WmcBimboProductRulesTests
{
    [Theory]
    [InlineData("PC")]
    [InlineData("UN")]
    [InlineData("PCT")]
    public void Staging_activation_accepts_the_504342_unit_contract_without_relaxing_package_configuration(string unit)
    {
        var issues = WmcBimboProductRules.ValidateStagingActivationData(CreateProduct("504342", unit), CreateInventory());

        Assert.Empty(issues);
    }

    [Fact]
    public void Staging_activation_keeps_pct_requirement_for_other_bimbo_products()
    {
        var issues = WmcBimboProductRules.ValidateStagingActivationData(CreateProduct("503838", "PC"), CreateInventory());

        Assert.Contains(issues, issue => issue.Code == "COMMERCIAL_PACKAGE_CONFIGURATION_INCOMPLETE");
    }

    [Fact]
    public void Staging_activation_keeps_ean_protection_for_504342()
    {
        var product = CreateProduct("504342", "PC");
        product.Ean = "0000000000000";
        product.Ean13 = "0000000000000";

        var issues = WmcBimboProductRules.ValidateStagingActivationData(product, CreateInventory());

        Assert.Contains(issues, issue => issue.Code == "COMMERCIAL_EAN_INVALID");
    }

    private static Product CreateProduct(string wmcCode, string unit) => new()
    {
        Sku = $"SKU-{wmcCode}",
        WmcCode = wmcCode,
        WmcBrandCode = "104",
        Name = "BIMBO produto homologado",
        Unit = unit,
        CommercialUnitCode = "PCT",
        LogisticsUnitCode = "CX",
        PackagesPerCase = 15,
        BasePrice = 33.13m,
        Ean = "7899679704936",
        Ean13 = "7899679704936",
        IsWmcActive = true,
        WmcStockAvailable = true
    };

    private static ProductInventory CreateInventory() => new()
    {
        QuantityOnHand = 1,
        QuantityReserved = 0
    };
}
