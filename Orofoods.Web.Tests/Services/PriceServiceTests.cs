using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class PriceServiceTests
{
    [Fact]
    public async Task Base_price_is_used_when_customer_has_a_price_table_with_prices_and_promotions()
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
            PromotionalPrice = 79.90m,
            IsAvailable = true,
            IsActive = true
        };

        db.AddRange(category, priceTable, customer, product);
        await db.SaveChangesAsync();

        db.PriceTableItems.Add(new PriceTableItem
        {
            PriceTableId = priceTable.Id,
            ProductId = product.Id,
            Price = 84.90m,
            PromotionalPrice = 74.90m
        });
        await db.SaveChangesAsync();

        var sut = new PriceService(db);
        var result = await sut.GetPriceAsync(customer.Id, product.Id);

        Assert.NotNull(result);
        Assert.Equal(92.90m, result!.EffectivePrice);
    }

    [Fact]
    public async Task Product_promotional_price_is_ignored_when_customer_has_no_price_table_item()
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
        Assert.Equal(110m, result!.EffectivePrice);
    }

    [Fact]
    public async Task Inactive_customer_price_table_and_product_promotion_are_ignored()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Paes", Slug = "inactive-table-promo", IsActive = true };
        var table = new PriceTable { Name = "Tabela inativa", IsActive = false };
        var customer = new Customer
        {
            LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99",
            Status = CustomerStatus.Approved, IsActive = true, PriceTable = table
        };
        var product = new Product
        {
            Sku = "WMC-503813", WmcCode = "503813", Name = "Produto", ProductCategory = category,
            Brand = "BIMBO", Unit = "PC", BasePrice = 20.39m, PromotionalPrice = 18.99m,
            IsAvailable = true, IsActive = true
        };
        db.AddRange(category, table, customer, product);
        await db.SaveChangesAsync();
        db.PriceTableItems.Add(new PriceTableItem
        {
            PriceTableId = table.Id, ProductId = product.Id, Price = 24.50m, PromotionalPrice = 17.50m
        });
        await db.SaveChangesAsync();

        var result = await new PriceService(db).GetPriceAsync(customer.Id, product.Id);

        Assert.NotNull(result);
        Assert.Equal(20.39m, result!.EffectivePrice);
    }

    [Fact]
    public async Task Inactive_customer_price_table_is_ignored_in_favor_of_base_price_when_product_has_no_promotion()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Paes", Slug = "inactive-table-base", IsActive = true };
        var table = new PriceTable { Name = "Tabela inativa", IsActive = false };
        var customer = new Customer
        {
            LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99",
            Status = CustomerStatus.Approved, IsActive = true, PriceTable = table
        };
        var product = new Product
        {
            Sku = "WMC-503813", WmcCode = "503813", Name = "Produto", ProductCategory = category,
            Brand = "BIMBO", Unit = "PC", BasePrice = 20.39m, IsAvailable = true, IsActive = true
        };
        db.AddRange(category, table, customer, product);
        await db.SaveChangesAsync();
        db.PriceTableItems.Add(new PriceTableItem
        {
            PriceTableId = table.Id, ProductId = product.Id, Price = 24.50m, PromotionalPrice = 17.50m
        });
        await db.SaveChangesAsync();

        var result = await new PriceService(db).GetPriceAsync(customer.Id, product.Id);

        Assert.NotNull(result);
        Assert.Equal(20.39m, result!.EffectivePrice);
    }

    [Fact]
    public async Task Active_table_prices_and_promotions_are_ignored_in_favor_of_base_price()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Paes", Slug = "active-table-promo", IsActive = true };
        var table = new PriceTable { Name = "Tabela ativa", IsActive = true };
        var customer = new Customer
        {
            LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99",
            Status = CustomerStatus.Approved, IsActive = true, PriceTable = table
        };
        var product = new Product
        {
            Sku = "WMC-503813", WmcCode = "503813", Name = "Produto", ProductCategory = category,
            Brand = "BIMBO", Unit = "PC", BasePrice = 20.39m, PromotionalPrice = 18.99m,
            IsAvailable = true, IsActive = true
        };
        db.AddRange(category, table, customer, product);
        await db.SaveChangesAsync();
        db.PriceTableItems.Add(new PriceTableItem
        {
            PriceTableId = table.Id, ProductId = product.Id, Price = 24.50m, PromotionalPrice = 17.50m
        });
        await db.SaveChangesAsync();

        var result = await new PriceService(db).GetPriceAsync(customer.Id, product.Id);

        Assert.NotNull(result);
        Assert.Equal(20.39m, result!.EffectivePrice);
    }

    [Fact]
    public async Task Positive_bimbo_base_price_of_one_real_is_commercially_valid()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Paes", Slug = "paes", IsActive = true };
        var customer = new Customer
        {
            LegalName = "Burger House Ltda", TradeName = "Burger House", Cnpj = "12.345.678/0001-99",
            Status = CustomerStatus.Approved, IsActive = true
        };
        var product = new Product
        {
            Sku = "WMC-503768", WmcCode = "503768", WmcBrandCode = "113", Name = "Pao BIMBO",
            ProductCategory = category, Brand = "BIMBO", Unit = "PCT", CommercialUnitCode = "PCT",
            BasePrice = 1m, IsAvailable = true, IsActive = true
        };
        db.AddRange(category, customer, product);
        await db.SaveChangesAsync();

        var result = await new PriceService(db).GetPriceAsync(customer.Id, product.Id);

        Assert.NotNull(result);
        Assert.True(result!.IsCommerciallyValid);
        Assert.Equal(1m, result.EffectivePrice);
    }
}
