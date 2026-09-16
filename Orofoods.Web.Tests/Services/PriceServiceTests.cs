using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class PriceServiceTests
{
    [Fact]
    public async Task Customer_price_table_item_overrides_product_base_price()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Paes", Slug = "paes", SortOrder = 1, IsActive = true };
        var priceTable = new PriceTable { Name = "Hamburgueria", IsActive = true };
        var customer = new Customer
        {
            LegalName = "Burger House Ltda",
            TradeName = "Burger House",
            Cnpj = "12.345.678/0001-99",
            Status = CustomerStatus.Approved,
            IsActive = true,
            PriceTable = priceTable
        };
        var product = new Product
        {
            Sku = "PAO-001",
            Name = "Pao Brioche",
            ProductCategory = category,
            Brand = "Orofoods",
            Unit = "caixa",
            UnitsPerCase = 30,
            BasePrice = 92.90m,
            IsAvailable = true,
            IsActive = true
        };

        db.AddRange(category, priceTable, customer, product);
        await db.SaveChangesAsync();

        db.PriceTableItems.Add(new PriceTableItem
        {
            PriceTableId = priceTable.Id,
            ProductId = product.Id,
            Price = 84.90m
        });
        await db.SaveChangesAsync();

        var sut = new PriceService(db);
        var result = await sut.GetPriceAsync(customer.Id, product.Id);

        Assert.NotNull(result);
        Assert.Equal(84.90m, result!.EffectivePrice);
    }

    [Fact]
    public async Task Product_promotional_price_is_used_when_customer_has_no_price_table_item()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Molhos", Slug = "molhos", SortOrder = 1, IsActive = true };
        var customer = new Customer
        {
            LegalName = "Burger House Ltda",
            TradeName = "Burger House",
            Cnpj = "12.345.678/0001-99",
            Status = CustomerStatus.Approved,
            IsActive = true
        };
        var product = new Product
        {
            Sku = "MOL-001",
            Name = "Cheddar",
            ProductCategory = category,
            Brand = "Orofoods",
            Unit = "caixa",
            UnitsPerCase = 6,
            BasePrice = 110m,
            PromotionalPrice = 99.90m,
            IsAvailable = true,
            IsActive = true
        };

        db.AddRange(category, customer, product);
        await db.SaveChangesAsync();

        var sut = new PriceService(db);
        var result = await sut.GetPriceAsync(customer.Id, product.Id);

        Assert.NotNull(result);
        Assert.Equal(99.90m, result!.EffectivePrice);
    }
}
