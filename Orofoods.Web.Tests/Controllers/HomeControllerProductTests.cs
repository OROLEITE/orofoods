using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Orofoods.Web.Controllers;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Tests.Infrastructure;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Tests.Controllers;

public class HomeControllerProductTests
{
    [Fact]
    public async Task Index_returns_active_catalog_products_without_granting_purchase_availability()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var visibleCategory = new ProductCategory { Name = "Pães", Slug = "paes", SortOrder = 1, IsActive = true };
        var unavailableCategory = new ProductCategory { Name = "Molhos", Slug = "molhos", SortOrder = 2, IsActive = true };
        var inactiveCategory = new ProductCategory { Name = "Inativa", Slug = "inativa", SortOrder = 3, IsActive = false };
        db.ProductCategories.AddRange(visibleCategory, unavailableCategory, inactiveCategory);
        await db.SaveChangesAsync();

        var regular = new Product { Sku = "REG-01", Name = "Regular", Brand = "Orofoods", ProductCategoryId = visibleCategory.Id, IsActive = true, IsAvailable = true };
        var featured = new Product { Sku = "DST-01", Name = "Destaque", Brand = "BIMBO", ProductCategoryId = visibleCategory.Id, IsActive = true, IsAvailable = true, IsFeatured = true };
        db.Products.AddRange(
            regular,
            featured,
            new Product { Sku = "OFF-01", Name = "Indisponível", Brand = "Orofoods", ProductCategoryId = unavailableCategory.Id, IsActive = true, IsAvailable = false, IsFeatured = true },
            new Product { Sku = "WMC-01", Name = "WMC pendente", Brand = "Orofoods", ProductCategoryId = unavailableCategory.Id, IsActive = true, IsAvailable = true, WmcCode = "10401", WmcInitialLoadReady = false },
            new Product { Sku = "CAT-01", Name = "Categoria inativa", Brand = "Orofoods", ProductCategoryId = inactiveCategory.Id, IsActive = true, IsAvailable = true },
            new Product { Sku = "OLD-01", Name = "Produto inativo", Brand = "Orofoods", ProductCategoryId = visibleCategory.Id, IsActive = false, IsAvailable = true });
        await db.SaveChangesAsync();
        db.ProductImages.Add(new ProductImage { ProductId = featured.Id, Url = "/images/featured.png", AltText = "Produto destaque", IsPrimary = true });
        await db.SaveChangesAsync();

        var result = await new HomeController(db) { ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() } }.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PublicHomeViewModel>(view.Model);
        var products = model.Products;
        var categories = model.Categories;
        Assert.Equal("Destaque", products[0].Name);
        Assert.Single(products[0].Images);
        Assert.Contains(products, product => product.Name == "Regular");
        Assert.Contains(products, product => product.Name == "Indisponível" && !product.IsCommerciallyAvailable);
        Assert.Contains(products, product => product.Name == "WMC pendente" && !product.IsCommerciallyAvailable);
        Assert.Contains(products, product => product.Name == "Categoria inativa" && product.IsActive);
        Assert.DoesNotContain(products, product => product.Name == "Produto inativo");
        Assert.Equal(["Pães", "Molhos"], categories.Select(category => category.Name));
    }

    [Fact]
    public async Task Products_search_matches_catalog_storage_information_for_the_frozen_quick_filter()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Pães", Slug = "paes", IsActive = true };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();
        var frozen = new Product
        {
            Sku = "HOT-DOG-01", Name = "Pão Hot Dog", Brand = "BIMBO", ProductCategoryId = category.Id,
            IsActive = true, IsAvailable = false, StorageInformation = "Manter congelado"
        };
        db.Products.Add(frozen);
        await db.SaveChangesAsync();

        var controller = new HomeController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() }
        };

        var result = Assert.IsType<ViewResult>(await controller.Products("congel", null));
        var model = Assert.IsType<PublicProductCatalogViewModel>(result.Model);

        Assert.Equal(frozen.Id, Assert.Single(model.Products).Id);
        Assert.False(model.Products[0].IsCommerciallyAvailable);
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

        var result = await new HomeController(db) { ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() } }.Index();

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

        var controller = new HomeController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() }
        };
        var result = await controller.Products("brioche", null);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PublicProductCatalogViewModel>(view.Model);
        Assert.Single(model.Products);
        Assert.False((bool)view.ViewData["CustomerPurchaseAuthorized"]!);
        Assert.Empty(Assert.IsType<Dictionary<int, decimal>>(view.ViewData["Prices"]));
    }

    [Fact]
    public async Task Products_search_matches_WMC_EAN_and_EAN13_without_matching_only_names()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "PÃ£es", Slug = "paes", IsActive = true };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();
        var wmcProduct = new Product
        {
            Sku = "SKU-A", Name = "Produto Alfa", Brand = "Marca A", WmcCode = "503768",
            ProductCategoryId = category.Id, IsActive = true, IsAvailable = true
        };
        var eanProduct = new Product
        {
            Sku = "SKU-B", Name = "Produto Beta", Brand = "Marca B", Ean = "7899679700365",
            ProductCategoryId = category.Id, IsActive = true, IsAvailable = true
        };
        var ean13Product = new Product
        {
            Sku = "SKU-C", Name = "Produto Gama", Brand = "Marca C", Ean13 = "0123456789012",
            ProductCategoryId = category.Id, IsActive = true, IsAvailable = true
        };
        db.Products.AddRange(wmcProduct, eanProduct, ean13Product);
        await db.SaveChangesAsync();
        var controller = new HomeController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() }
        };

        foreach (var (query, expectedId) in new[]
        {
            ("503768", wmcProduct.Id),
            ("7899679700365", eanProduct.Id),
            ("0123456789012", ean13Product.Id)
        })
        {
            var result = Assert.IsType<ViewResult>(await controller.Products(query, null));
            var model = Assert.IsType<PublicProductCatalogViewModel>(result.Model);
            Assert.Equal(expectedId, Assert.Single(model.Products).Id);
        }
    }

    [Fact]
    public async Task Products_exposes_only_the_approved_customers_authorized_table_price()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Pães", Slug = "paes", IsActive = true };
        var table = new PriceTable { Name = "Tabela aprovada", IsActive = true };
        var customer = new Customer
        {
            LegalName = "Cliente A Ltda", TradeName = "Cliente A", Cnpj = "12.345.678/0001-99",
            Status = CustomerStatus.Approved, IsActive = true, PriceTable = table
        };
        db.AddRange(category, table, customer);
        await db.SaveChangesAsync();
        var product = new Product
        {
            Sku = "BIMBO-503768", WmcCode = "503768", Name = "Pão", Brand = "BIMBO",
            ProductCategoryId = category.Id, IsActive = true, IsAvailable = true, CommercialUnitCode = "PCT",
            BasePrice = 1m, IsWmcActive = true, WmcStockAvailable = true, WmcInitialLoadReady = true
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        db.ProductInventories.Add(new Orofoods.Web.Models.Inventory.ProductInventory { ProductId = product.Id, QuantityOnHand = 10, QuantityReserved = 0 });
        db.PriceTableItems.Add(new PriceTableItem { PriceTableId = table.Id, ProductId = product.Id, Price = 18.50m });
        db.Users.Add(new ApplicationUser { Id = "approved-client", UserName = "approved-client@test.local", NormalizedUserName = "APPROVED-CLIENT@TEST.LOCAL", Email = "approved-client@test.local", NormalizedEmail = "APPROVED-CLIENT@TEST.LOCAL", IsActive = true, CustomerId = customer.Id });
        await db.SaveChangesAsync();

        var controller = new HomeController(db, customerAccessService: new CustomerAccessService(db), priceService: new PriceService(db));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, "approved-client"),
                    new Claim(ClaimTypes.Role, "Cliente")
                ], "test"))
            }
        };

        var result = await controller.Products(null, null);

        var view = Assert.IsType<ViewResult>(result);
        Assert.True((bool)view.ViewData["CustomerPurchaseAuthorized"]!);
        var prices = Assert.IsType<Dictionary<int, decimal>>(view.ViewData["Prices"]);
        Assert.Equal(18.50m, prices[product.Id]);
        Assert.True(Assert.IsType<PublicProductCatalogViewModel>(view.Model).Products.Single().IsCommerciallyAvailable);
    }

    [Fact]
    public async Task Index_gives_visitors_no_prices_and_approved_customers_only_their_table_price()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Pães", Slug = "paes", IsActive = true };
        var table = new PriceTable { Name = "Tabela aprovada", IsActive = true };
        var customer = new Customer
        {
            LegalName = "Cliente A Ltda", TradeName = "Cliente A", Cnpj = "12.345.678/0001-99",
            Status = CustomerStatus.Approved, IsActive = true, PriceTable = table
        };
        db.AddRange(category, table, customer);
        await db.SaveChangesAsync();
        var product = new Product
        {
            Sku = "BIMBO-503768", WmcCode = "503768", Name = "Pão", Brand = "BIMBO",
            ProductCategoryId = category.Id, IsActive = true, IsAvailable = true, CommercialUnitCode = "PCT",
            BasePrice = 1m, IsWmcActive = true, WmcStockAvailable = true, WmcInitialLoadReady = true
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        db.ProductInventories.Add(new Orofoods.Web.Models.Inventory.ProductInventory { ProductId = product.Id, QuantityOnHand = 10, QuantityReserved = 0 });
        db.PriceTableItems.Add(new PriceTableItem { PriceTableId = table.Id, ProductId = product.Id, Price = 18.50m });
        db.Users.Add(new ApplicationUser { Id = "approved-client", UserName = "approved-client@test.local", NormalizedUserName = "APPROVED-CLIENT@TEST.LOCAL", Email = "approved-client@test.local", NormalizedEmail = "APPROVED-CLIENT@TEST.LOCAL", IsActive = true, CustomerId = customer.Id });
        await db.SaveChangesAsync();

        var visitor = new HomeController(db, customerAccessService: new CustomerAccessService(db), priceService: new PriceService(db))
        {
            ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() }
        };
        var visitorView = Assert.IsType<ViewResult>(await visitor.Index());
        Assert.False((bool)visitorView.ViewData["CustomerPurchaseAuthorized"]!);
        Assert.Empty(Assert.IsType<Dictionary<int, decimal>>(visitorView.ViewData["Prices"]));

        var client = new HomeController(db, customerAccessService: new CustomerAccessService(db), priceService: new PriceService(db))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([
                        new Claim(ClaimTypes.NameIdentifier, "approved-client"),
                        new Claim(ClaimTypes.Role, "Cliente")
                    ], "test"))
                }
            }
        };
        var clientView = Assert.IsType<ViewResult>(await client.Index());
        Assert.True((bool)clientView.ViewData["CustomerPurchaseAuthorized"]!);
        Assert.Equal(18.50m, Assert.IsType<Dictionary<int, decimal>>(clientView.ViewData["Prices"])[product.Id]);
        Assert.True(Assert.IsType<PublicHomeViewModel>(clientView.Model).Products.Single().IsCommerciallyAvailable);
    }

    [Fact]
    public async Task Index_shows_ten_active_BIMBO_products_even_when_unavailable_without_duplicates()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Pães", Slug = "paes", IsActive = true };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();

        var bimboProducts = Enumerable.Range(1, 10)
            .Select(index => new Product
            {
                Sku = $"BIMBO-{index:00}",
                WmcCode = $"5038{index:02}",
                Name = $"BIMBO pão {index:00}",
                Brand = "BIMBO",
                ProductCategoryId = category.Id,
                IsActive = true,
                IsAvailable = index % 2 == 0,
                IsWmcActive = true,
                WmcStockAvailable = true,
                WmcInitialLoadReady = index % 2 == 0
            })
            .ToArray();
        db.Products.AddRange(bimboProducts);
        db.Products.AddRange(
            new Product { Sku = "OTHER-01", Name = "Outro destaque A", Brand = "Orofoods", ProductCategoryId = category.Id, IsActive = true, IsAvailable = true, IsFeatured = true },
            new Product { Sku = "OTHER-02", Name = "Outro destaque B", Brand = "Orofoods", ProductCategoryId = category.Id, IsActive = true, IsAvailable = true, IsFeatured = true },
            new Product { Sku = "BIMBO-OLD", Name = "BIMBO inativo", Brand = "BIMBO", ProductCategoryId = category.Id, IsActive = false, IsFeatured = true });
        await db.SaveChangesAsync();

        var controller = new HomeController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() }
        };

        var result = Assert.IsType<ViewResult>(await controller.Index());
        var products = Assert.IsType<PublicHomeViewModel>(result.Model).Products;

        Assert.Equal(10, products.Count);
        Assert.Equal(10, products.Count(product => product.Brand == "BIMBO"));
        Assert.Equal(products.Count, products.Select(product => product.Id).Distinct().Count());
        Assert.Equal(bimboProducts.OrderBy(product => product.Name).Select(product => product.Name), products.Select(product => product.Name));
        Assert.All(products, product => Assert.True(product.IsActive));
        Assert.All(products.Where(product => product.Brand == "BIMBO"), product => Assert.False(product.IsCommerciallyAvailable));
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

    [Fact]
    public async Task Product_keeps_public_details_route_for_an_authenticated_customer()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Paes", Slug = "paes", IsActive = true };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();
        db.Products.Add(new Product { Id = 31, Sku = "BIMBO-31", WmcCode = "503768", Name = "Pao BIMBO", Brand = "BIMBO", ProductCategoryId = category.Id, IsActive = true, IsAvailable = true });
        await db.SaveChangesAsync();
        var controller = new HomeController(db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "customer"), new Claim(ClaimTypes.Role, "Cliente")], "test"))
            }
        };

        var result = await controller.Product(31);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(31, Assert.IsType<Product>(view.Model).Id);
    }
}
