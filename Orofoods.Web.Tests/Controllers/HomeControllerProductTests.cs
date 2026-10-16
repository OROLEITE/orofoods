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
    public async Task Index_prioritizes_BIMBO_products_with_associated_photos_without_exposing_inactive_products()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Paes", Slug = "paes", IsActive = true };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();

        var featuredOtherBrand = new Product { Sku = "OTHER-01", Name = "Outro destaque", Brand = "Orofoods", ProductCategoryId = category.Id, IsActive = true, IsAvailable = true, IsFeatured = true };
        var bimboWithPhoto = new Product { Sku = "BIMBO-01", Name = "BIMBO com foto", Brand = "BIMBO", ProductCategoryId = category.Id, IsActive = true, IsAvailable = true };
        var bimboWithoutPhoto = new Product { Sku = "BIMBO-02", Name = "BIMBO sem foto", Brand = "BIMBO", ProductCategoryId = category.Id, IsActive = true, IsAvailable = true };
        var inactiveBimboWithPhoto = new Product { Sku = "BIMBO-03", Name = "BIMBO inativo", Brand = "BIMBO", ProductCategoryId = category.Id, IsActive = false, IsAvailable = true, IsFeatured = true };
        db.Products.AddRange(featuredOtherBrand, bimboWithPhoto, bimboWithoutPhoto, inactiveBimboWithPhoto);
        await db.SaveChangesAsync();
        db.ProductImages.AddRange(
            new ProductImage { ProductId = bimboWithPhoto.Id, Url = "/uploads/products/bimbo-confirmed.png", AltText = "Embalagem BIMBO confirmada", IsPrimary = true },
            new ProductImage { ProductId = inactiveBimboWithPhoto.Id, Url = "/uploads/products/bimbo-inactive.png", AltText = "Embalagem BIMBO", IsPrimary = true });
        await db.SaveChangesAsync();

        var result = await new HomeController(db).Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PublicHomeViewModel>(view.Model);
        Assert.Equal("BIMBO com foto", model.Products[0].Name);
        Assert.Single(model.Products[0].Images);
        Assert.DoesNotContain(model.Products, product => product.Name == "BIMBO inativo");
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
