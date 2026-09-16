using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Models;

public class FavoriteProductTests
{
    [Fact]
    public async Task Favorite_product_is_unique_per_customer_and_product()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Burger House Ltda", Cnpj = "12.345.678/0001-99" };
        var category = new ProductCategory { Name = "Pães", Slug = "paes", IsActive = true };
        db.AddRange(customer, category);
        await db.SaveChangesAsync();
        var product = new Product { Sku = "BRI-01", Name = "Pão Brioche", Brand = "Orofoods", ProductCategoryId = category.Id };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        db.FavoriteProducts.AddRange(
            new FavoriteProduct { CustomerId = customer.Id, ProductId = product.Id },
            new FavoriteProduct { CustomerId = customer.Id, ProductId = product.Id });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
