using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Services.Catalog;
using Orofoods.Web.Services.Storage;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Controllers;

public sealed class AdminProductsControllerTests
{
    [Fact]
    public async Task Edit_returns_validation_error_when_wmc_code_is_already_assigned()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Teste", Slug = "teste", IsActive = true };
        db.ProductCategories.Add(category);
        db.Products.Add(new Product
        {
            Sku = "WMC-243680",
            WmcCode = " 243680 ",
            Name = "Produto já mapeado",
            ProductCategory = category,
            Brand = "Teste",
            Unit = "UN"
        });
        await db.SaveChangesAsync();

        var controller = new ProductsController(db, new AdminCatalogService(db), Mock.Of<IProductImageStorage>());
        var result = await controller.Edit(new Product
        {
            Sku = "STG-TEST-0001",
            WmcCode = "243680",
            Name = "Produto de homologação",
            ProductCategoryId = category.Id,
            Brand = "Teste",
            Unit = "UN"
        });

        Assert.IsType<ViewResult>(result);
        Assert.Contains(
            controller.ModelState.Values.SelectMany(state => state.Errors),
            error => error.ErrorMessage.Contains("codigo WMC", StringComparison.OrdinalIgnoreCase));
        Assert.Single(await db.Products.ToListAsync());
    }

    [Fact]
    public async Task Index_exposes_stock_separately_and_keeps_inactive_bimbo_visible_when_requested()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Teste", Slug = "teste", IsActive = true };
        var product = new Product
        {
            Sku = "BIMBO-503838",
            WmcCode = "503838",
            WmcBrandCode = "104",
            Name = "Mini HB Brioche",
            Brand = "BIMBO",
            Unit = "PC",
            CommercialUnitCode = "PCT",
            LogisticsUnitCode = "CX",
            PackagesPerCase = 8,
            ProductCategory = category,
            IsActive = false,
            IsAvailable = false
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        db.ProductInventories.Add(new ProductInventory { ProductId = product.Id, QuantityOnHand = 70, QuantityReserved = 4 });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = Assert.IsType<ViewResult>(await controller.Index("BIMBO", includeInactive: true));
        var rows = Assert.IsAssignableFrom<IReadOnlyList<Product>>(result.Model);
        var row = Assert.Single(rows);
        var stockByProductId = Assert.IsAssignableFrom<IReadOnlyDictionary<int, int>>(controller.ViewData["StockByProductId"]);

        Assert.False(row.IsActive);
        Assert.False(row.IsAvailable);
        Assert.Equal("PCT", row.PriceUnitLabel);
        Assert.Equal(70, stockByProductId[row.Id]);

        var activeOnlyController = CreateController(db);
        var activeOnlyResult = Assert.IsType<ViewResult>(await activeOnlyController.Index("BIMBO"));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<Product>>(activeOnlyResult.Model));
    }

    [Fact]
    public async Task Deactivate_sets_success_feedback_after_service_completes()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Teste", Slug = "teste", IsActive = true };
        var product = new Product { Sku = "SKU-1", Name = "Produto", Brand = "Teste", ProductCategory = category, Unit = "PCT" };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        SetTempData(controller);

        var result = Assert.IsType<RedirectToActionResult>(await controller.Deactivate(product.Id, "BIMBO", includeInactive: true, page: 4));

        Assert.Equal(nameof(ProductsController.Index), result.ActionName);
        Assert.Equal("BIMBO", result.RouteValues!["q"]);
        Assert.Equal(true, result.RouteValues["includeInactive"]);
        Assert.Equal(4, result.RouteValues["page"]);
        Assert.Equal("Produto desativado com sucesso.", controller.TempData["ProductSuccess"]);
        Assert.False((await db.Products.SingleAsync(item => item.Id == product.Id)).IsActive);
        Assert.False((await db.Products.SingleAsync(item => item.Id == product.Id)).IsAvailable);
    }

    [Fact]
    public async Task Deactivate_missing_product_returns_error_feedback_without_success()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var controller = CreateController(db);
        SetTempData(controller);

        var result = Assert.IsType<RedirectToActionResult>(await controller.Deactivate(404));

        Assert.Equal(nameof(ProductsController.Index), result.ActionName);
        Assert.NotNull(controller.TempData["ProductError"]);
        Assert.Null(controller.TempData["ProductSuccess"]);
    }

    [Fact]
    public async Task Edit_reports_activation_only_after_a_successful_save()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Teste", Slug = "teste", IsActive = true };
        db.ProductCategories.Add(category);
        var product = new Product
        {
            Sku = "SKU-1",
            Name = "Produto",
            Brand = "Teste",
            Unit = "PCT",
            ProductCategory = category,
            IsActive = false,
            IsAvailable = false
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        SetTempData(controller);
        var input = new Product
        {
            Id = product.Id,
            Sku = product.Sku,
            Name = product.Name,
            Brand = product.Brand,
            Unit = product.Unit,
            ProductCategoryId = category.Id,
            IsActive = true,
            IsAvailable = false,
            Accent = product.Accent
        };

        var result = Assert.IsType<RedirectToActionResult>(await controller.Edit(input, "BIMBO", includeInactive: true, page: 5));

        Assert.Equal(nameof(ProductsController.Index), result.ActionName);
        Assert.Equal("BIMBO", result.RouteValues!["q"]);
        Assert.Equal(true, result.RouteValues["includeInactive"]);
        Assert.Equal(5, result.RouteValues["page"]);
        Assert.Equal("Produto ativado com sucesso.", controller.TempData["ProductSuccess"]);
        var saved = await db.Products.SingleAsync(item => item.Id == product.Id);
        Assert.True(saved.IsActive);
        Assert.False(saved.IsAvailable);
    }

    [Fact]
    public async Task Edit_keeps_bimbo_inactive_and_preserves_its_stock_when_active_remains_unchecked()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Teste", Slug = "teste", IsActive = true };
        var product = new Product
        {
            Sku = "BIMBO-503838",
            WmcCode = "503838",
            WmcBrandCode = "104",
            Name = "Mini HB Brioche",
            Brand = "BIMBO",
            Unit = "PC",
            CommercialUnitCode = "PCT",
            LogisticsUnitCode = "CX",
            PackagesPerCase = 8,
            ProductCategory = category,
            BasePrice = 1m,
            IsActive = false,
            IsAvailable = false
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        db.ProductInventories.Add(new ProductInventory { ProductId = product.Id, QuantityOnHand = 70 });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        SetTempData(controller);
        var result = await controller.Edit(new Product
        {
            Id = product.Id,
            Sku = product.Sku,
            WmcCode = product.WmcCode,
            Name = product.Name,
            Brand = product.Brand,
            Unit = product.Unit,
            CommercialUnitCode = product.CommercialUnitCode,
            LogisticsUnitCode = product.LogisticsUnitCode,
            PackagesPerCase = product.PackagesPerCase,
            ProductCategoryId = category.Id,
            BasePrice = product.BasePrice,
            IsActive = false,
            IsAvailable = false,
            Accent = product.Accent
        });

        Assert.IsType<RedirectToActionResult>(result);
        var saved = await db.Products.SingleAsync(item => item.Id == product.Id);
        var inventory = await db.ProductInventories.SingleAsync(item => item.ProductId == product.Id);
        Assert.False(saved.IsActive);
        Assert.False(saved.IsAvailable);
        Assert.Equal(1m, saved.BasePrice);
        Assert.Equal(70, inventory.QuantityOnHand);
    }

    private static ProductsController CreateController(ApplicationDbContext db) =>
        new(db, new AdminCatalogService(db), Mock.Of<IProductImageStorage>());

    private static void SetTempData(Controller controller) =>
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
}
