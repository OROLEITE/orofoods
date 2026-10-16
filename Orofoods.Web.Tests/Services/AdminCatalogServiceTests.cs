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

    [Fact]
    public async Task Saves_valid_ean_and_dun_as_strings_without_removing_leading_zeroes()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Paes", Slug = "paes", IsActive = true };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();
        var sut = new AdminCatalogService(db);

        var product = await sut.SaveProductAsync(new Product
        {
            Sku = "EAN-ZERO-001", Name = "Produto com códigos", ProductCategoryId = category.Id,
            Brand = "Teste", Unit = "UN", Ean13 = "0123456789012", Dun14 = "00012345678905"
        });

        Assert.Equal("0123456789012", product.Ean13);
        Assert.Equal("00012345678905", product.Dun14);
    }

    [Fact]
    public async Task Rejects_incomplete_package_unit_configuration()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Paes", Slug = "paes", IsActive = true };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();
        var sut = new AdminCatalogService(db);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SaveProductAsync(new Product
        {
            Sku = "PCT-INVALIDO", Name = "Produto", ProductCategoryId = category.Id,
            Brand = "Teste", Unit = "UN", CommercialUnitCode = "PCT", LogisticsUnitCode = "CX"
        }));

        Assert.Contains("fator", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Does_not_reclassify_a_product_referenced_by_a_legacy_saved_order()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Orofoods.Web.Models.Customers.Customer
        {
            LegalName = "Cliente", TradeName = "Cliente", Cnpj = "55.555.555/0001-55",
            Status = Orofoods.Web.Models.Customers.CustomerStatus.Approved, IsActive = true
        };
        var category = new ProductCategory { Name = "Paes", Slug = "paes", IsActive = true };
        var product = new Product
        {
            Sku = "LEGACY-ORDER-001", Name = "Produto legado", ProductCategory = category,
            Brand = "Teste", Unit = "caixa", BasePrice = 20m
        };
        var savedOrder = new Orofoods.Web.Models.Orders.SavedOrder
        {
            Customer = customer, Name = "Modelo legado",
            Items = [new Orofoods.Web.Models.Orders.SavedOrderItem { Product = product, Quantity = 3 }]
        };
        db.AddRange(customer, category, product, savedOrder);
        await db.SaveChangesAsync();
        var sut = new AdminCatalogService(db);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SaveProductAsync(new Product
        {
            Id = product.Id, Sku = product.Sku, Name = product.Name, ProductCategoryId = category.Id,
            Brand = product.Brand, Unit = product.Unit, BasePrice = product.BasePrice,
            CommercialUnitCode = "PCT", LogisticsUnitCode = "CX", PackagesPerCase = 12
        }));

        Assert.Contains("unidade atual", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null((await db.Products.FindAsync(product.Id))!.CommercialUnitCode);
    }

    [Fact]
    public async Task Allows_product_to_keep_its_own_wmc_code_when_editing()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Paes", Slug = "paes", IsActive = true };
        var existing = new Product
        {
            Sku = "STG-TEST-0001",
            WmcCode = "243680",
            Name = "Produto de homologacao",
            ProductCategory = category,
            Brand = "Teste",
            Unit = "UN"
        };
        db.AddRange(category, existing);
        await db.SaveChangesAsync();
        var sut = new AdminCatalogService(db);

        var updated = await sut.SaveProductAsync(new Product
        {
            Id = existing.Id,
            Sku = existing.Sku,
            WmcCode = "243680",
            Name = "Produto de homologacao atualizado",
            ProductCategoryId = category.Id,
            Brand = "Teste",
            Unit = "UN"
        });

        Assert.Equal(existing.Id, updated.Id);
        Assert.Equal("243680", updated.WmcCode);
        Assert.Equal("Produto de homologacao atualizado", updated.Name);
        Assert.Single(await db.Products.ToListAsync());
    }
}
