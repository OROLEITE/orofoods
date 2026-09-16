using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Controllers;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Tests.Infrastructure;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Tests.Controllers;

public class HomeControllerProductTests
{
    [Fact]
    public async Task Index_returns_featured_available_products_with_images_first()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Paes", Slug = "paes", IsActive = true };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();

        var regular = new Product { Sku = "REG-01", Name = "Regular", Brand = "Orofoods", ProductCategoryId = category.Id, IsAvailable = true };
        var featured = new Product { Sku = "DST-01", Name = "Destaque", Brand = "BIMBO", ProductCategoryId = category.Id, IsAvailable = true, IsFeatured = true };
        db.Products.AddRange(regular, featured, new Product { Sku = "OFF-01", Name = "Indisponivel", Brand = "Orofoods", ProductCategoryId = category.Id, IsAvailable = false, IsFeatured = true });
        await db.SaveChangesAsync();
        db.ProductImages.Add(new ProductImage { ProductId = featured.Id, Url = "/images/featured.png", AltText = "Produto destaque", IsPrimary = true });
        await db.SaveChangesAsync();

        var result = await new HomeController(db).Index();

        var view = Assert.IsType<ViewResult>(result);
        var products = Assert.IsAssignableFrom<IReadOnlyList<Product>>(view.Model);
        Assert.Equal("Destaque", products[0].Name);
        Assert.Single(products[0].Images);
        Assert.DoesNotContain(products, product => product.Name == "Indisponivel");
    }

    [Fact]
    public async Task Products_returns_only_active_products_matching_search()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Pães", Slug = "paes", IsActive = true };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();
        db.Products.AddRange(
            new Product { Sku = "BRI-01", Name = "Pão Brioche", Brand = "Orofoods", ProductCategoryId = category.Id, IsActive = true, IsAvailable = true },
            new Product { Sku = "OLD-01", Name = "Produto Antigo", Brand = "Orofoods", ProductCategoryId = category.Id, IsActive = false });
        await db.SaveChangesAsync();

        var result = await new HomeController(db).Products("brioche", null);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PublicProductCatalogViewModel>(view.Model);
        Assert.Single(model.Products);
    }

    [Fact]
    public async Task Product_returns_not_found_when_product_is_inactive()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Pães", Slug = "paes", IsActive = true };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();
        db.Products.Add(new Product { Sku = "OLD-01", Name = "Produto Antigo", Brand = "Orofoods", ProductCategoryId = category.Id, IsActive = false });
        await db.SaveChangesAsync();

        var result = await new HomeController(db).Product(1);

        Assert.IsType<NotFoundResult>(result);
    }
}
