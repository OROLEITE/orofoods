using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Controllers;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Tests.Infrastructure;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Tests.Controllers;

public class HomeControllerProductTests
{
    [Fact]
    public async Task Index_returns_only_public_products_and_categories_with_visible_products()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var visibleCategory = new ProductCategory { Name = "Pães", Slug = "paes", SortOrder = 1, IsActive = true };
        var unavailableCategory = new ProductCategory { Name = "Molhos", Slug = "molhos", SortOrder = 2, IsActive = true };
        var inactiveCategory = new ProductCategory { Name = "Inativa", Slug = "inativa", SortOrder = 3, IsActive = false };
        db.ProductCategories.AddRange(visibleCategory, unavailableCategory, inactiveCategory);
        await db.SaveChangesAsync();

        var regular = new Product { Sku = "REG-01", Name = "Regular", Brand = "Orofoods", ProductCategoryId = visibleCategory.Id, IsAvailable = true };
        var featured = new Product { Sku = "DST-01", Name = "Destaque", Brand = "BIMBO", ProductCategoryId = visibleCategory.Id, IsAvailable = true, IsFeatured = true };
        db.Products.AddRange(
            regular,
            featured,
            new Product { Sku = "OFF-01", Name = "Indisponível", Brand = "Orofoods", ProductCategoryId = unavailableCategory.Id, IsAvailable = false, IsFeatured = true },
            new Product { Sku = "WMC-01", Name = "WMC pendente", Brand = "Orofoods", ProductCategoryId = unavailableCategory.Id, IsAvailable = true, WmcCode = "10401", WmcInitialLoadReady = false },
            new Product { Sku = "CAT-01", Name = "Categoria inativa", Brand = "Orofoods", ProductCategoryId = inactiveCategory.Id, IsAvailable = true });
        await db.SaveChangesAsync();
        db.ProductImages.Add(new ProductImage { ProductId = featured.Id, Url = "/images/featured.png", AltText = "Produto destaque", IsPrimary = true });
        await db.SaveChangesAsync();

        var result = await new HomeController(db).Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PublicHomeViewModel>(view.Model);
        var products = model.Products;
        var categories = model.Categories;
        Assert.Equal("Destaque", products[0].Name);
        Assert.Single(products[0].Images);
        Assert.Contains(products, product => product.Name == "Regular");
        Assert.DoesNotContain(products, product => product.Name is "Indisponível" or "WMC pendente");
        Assert.Equal(["Pães"], categories.Select(category => category.Name));
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
