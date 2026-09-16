using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Services.Catalog;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class AdminCatalogServiceTests
{
    [Fact]
    public async Task Adds_first_product_image_as_primary_and_keeps_a_single_primary_image()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Congelados", Slug = "congelados", IsActive = true };
        var product = new Product { Sku = "BIM-001", Name = "Pao", ProductCategory = category, Brand = "BIMBO", Unit = "caixa", BasePrice = 90m, IsActive = true };
        db.AddRange(category, product);
        await db.SaveChangesAsync();
        var sut = new AdminCatalogService(db);

        var first = await sut.AddProductImageAsync(product.Id, "/uploads/products/first.png", "Primeira foto");
        Assert.True(first.IsPrimary);
        var second = await sut.AddProductImageAsync(product.Id, "/uploads/products/second.png", "Segunda foto");
        await sut.SetPrimaryImageAsync(product.Id, second.Id);

        var images = await db.ProductImages.Where(image => image.ProductId == product.Id).OrderBy(image => image.SortOrder).ToListAsync();
        var primary = Assert.Single(images, image => image.IsPrimary);
        Assert.Equal(second.Id, primary.Id);
    }

    [Fact]
    public async Task Rejects_duplicate_sku_and_deactivates_product_without_deleting_it()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Paes", Slug = "paes", IsActive = true };
        var existing = new Product { Sku = "PAO-001", Name = "Brioche", ProductCategory = category, Brand = "Orofoods", Unit = "caixa", BasePrice = 90m, IsActive = true, IsAvailable = true };
        db.AddRange(category, existing);
        await db.SaveChangesAsync();
        var sut = new AdminCatalogService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SaveProductAsync(new Product { Sku = "pao-001", Name = "Duplicado", ProductCategoryId = category.Id }));
        await sut.DeactivateProductAsync(existing.Id);

        var persisted = await db.Products.FindAsync(existing.Id);
        Assert.NotNull(persisted);
        Assert.False(persisted!.IsActive);
        Assert.False(persisted.IsAvailable);
    }

    [Fact]
    public async Task Saves_the_wmc_code_for_a_product()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Paes", Slug = "paes", IsActive = true };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();
        var sut = new AdminCatalogService(db);

        var product = await sut.SaveProductAsync(new Product
        {
            Sku = "BIM-009",
            WmcCode = "610601552",
            Name = "Pao Brioche",
            ProductCategoryId = category.Id,
            Brand = "BIMBO",
            Description = "Pao congelado",
            Unit = "caixa",
            BasePrice = 100m,
            IsActive = true,
            IsAvailable = true
        });

        Assert.Equal("610601552", product.WmcCode);
    }
}
