using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class AdminCommercialServiceTests
{
    [Fact]
    public async Task Upserts_one_price_per_table_and_product()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Paes", Slug = "paes", IsActive = true };
        var product = new Product { Sku = "PAO-001", Name = "Brioche", ProductCategory = category, Brand = "Orofoods", Unit = "caixa", BasePrice = 90m, IsActive = true };
        var table = new PriceTable { Name = "Hamburgueria", IsActive = true };
        db.AddRange(category, product, table);
        await db.SaveChangesAsync();
        var sut = new AdminCommercialService(db);

        await sut.SavePriceAsync(table.Id, product.Id, 84m, null);
        await sut.SavePriceAsync(table.Id, product.Id, 82m, 79m);

        var item = Assert.Single(db.PriceTableItems);
        Assert.Equal(82m, item.Price);
        Assert.Equal(79m, item.PromotionalPrice);
    }
}
