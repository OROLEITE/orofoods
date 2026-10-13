using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Controllers.Api.V1;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Controllers;

public class AdminInventoryControllerTests
{
    [Fact]
    public async Task Manual_adjustment_is_saved_and_audited_and_not_overwritten_by_wmc_stock_sync()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Pães", Slug = "paes", IsActive = true };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();

        var product = new Product
        {
            Sku = "WMC-243680",
            WmcCode = "243680",
            Name = "BAUDUCCO PAO GRAOS CLASSICOS 390G",
            ProductCategoryId = category.Id,
            Unit = "UN",
            UnitsPerCase = 1,
            MinimumCases = 2,
            IsActive = true,
            IsAvailable = true,
            IsWmcActive = true,
            WmcStockAvailable = true,
            WmcInitialLoadReady = true
        };
        var wmcManagedProduct = new Product
        {
            Sku = "WMC-243681",
            WmcCode = "243681",
            Name = "Produto sem ajuste manual",
            ProductCategoryId = category.Id,
            Unit = "UN",
            UnitsPerCase = 1,
            MinimumCases = 1,
            IsActive = true,
            IsAvailable = true,
            IsWmcActive = true,
            WmcStockAvailable = true,
            WmcInitialLoadReady = true
        };
        db.Products.Add(product);
        db.Products.Add(wmcManagedProduct);
        db.ProductInventories.Add(new ProductInventory
        {
            Product = product,
            QuantityOnHand = 4,
            QuantityReserved = 1
        });
        db.ProductInventories.Add(new ProductInventory
        {
            Product = wmcManagedProduct,
            QuantityOnHand = 3,
            QuantityReserved = 1
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var action = await controller.Adjust(product.Id, 6, "Conferência física");

        Assert.IsType<RedirectToActionResult>(action);
        var inventory = await db.ProductInventories.SingleAsync(x => x.ProductId == product.Id);
        Assert.Equal(6, inventory.QuantityOnHand);
        Assert.Equal(1, inventory.QuantityReserved);
        Assert.True(inventory.IsManuallyAdjusted);
        var audit = Assert.Single(await db.InventoryAdjustments.ToListAsync());
        Assert.Equal(4, audit.PreviousQuantityOnHand);
        Assert.Equal(6, audit.NewQuantityOnHand);
        Assert.Equal("Conferência física", audit.Reason);

        var sync = CreateWmcSyncService(db,
        [
            new WmcProductRecord("243680", product.Name, "A", "UN", 0, 0),
            new WmcProductRecord("243681", wmcManagedProduct.Name, "A", "UN", 5, 5)
        ]);
        await sync.SyncStockAsync();

        Assert.Equal(6, inventory.QuantityOnHand);
        Assert.False(product.WmcStockAvailable);
        Assert.Equal(5, (await db.ProductInventories.SingleAsync(x => x.ProductId == wmcManagedProduct.Id)).QuantityOnHand);
        Assert.False((await db.ProductInventories.SingleAsync(x => x.ProductId == wmcManagedProduct.Id)).IsManuallyAdjusted);

        var catalogResult = await new CatalogController(db).Products();
        var catalogPage = Assert.IsType<Orofoods.Web.Api.PagedResult<ProductResponse>>(
            Assert.IsType<OkObjectResult>(catalogResult.Result).Value);
        Assert.False(catalogPage.Items.Single(item => item.Id == product.Id).IsAvailable);
    }

    [Fact]
    public async Task Catalog_availability_accounts_for_reserved_quantity_and_minimum_cases()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Pães", Slug = "paes", IsActive = true };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();
        var product = new Product
        {
            Sku = "WMC-243680",
            WmcCode = "243680",
            Name = "BAUDUCCO PAO GRAOS CLASSICOS 390G",
            ProductCategoryId = category.Id,
            MinimumCases = 3,
            IsActive = true,
            IsAvailable = true,
            IsWmcActive = true,
            WmcStockAvailable = true,
            WmcInitialLoadReady = true
        };
        db.Products.Add(product);
        db.ProductInventories.Add(new ProductInventory
        {
            Product = product,
            QuantityOnHand = 5,
            QuantityReserved = 3
        });
        await db.SaveChangesAsync();

        var result = await new CatalogController(db).Products();
        var page = Assert.IsType<Orofoods.Web.Api.PagedResult<ProductResponse>>(
            Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.False(Assert.Single(page.Items).IsAvailable);
    }

    [Fact]
    public async Task Adjustment_creates_missing_inventory_row_and_audit()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Pães", Slug = "paes", IsActive = true };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();
        var product = new Product
        {
            Sku = "SKU-NEW", Name = "Produto novo", ProductCategoryId = category.Id,
            IsActive = true, IsAvailable = true
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var action = await controller.Adjust(product.Id, 4, "Contagem inicial");

        Assert.IsType<RedirectToActionResult>(action);
        var inventory = Assert.Single(await db.ProductInventories.ToListAsync());
        Assert.Equal(4, inventory.QuantityOnHand);
        Assert.Equal(0, inventory.QuantityReserved);
        Assert.True(inventory.IsManuallyAdjusted);
        var audit = Assert.Single(await db.InventoryAdjustments.ToListAsync());
        Assert.Equal(0, audit.PreviousQuantityOnHand);
        Assert.Equal(4, audit.NewQuantityOnHand);
    }

    [Fact]
    public async Task Catalog_keeps_wmc_availability_gate_after_manual_stock_adjustment()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Pães", Slug = "paes", IsActive = true };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();
        var product = new Product
        {
            Sku = "WMC-243680",
            WmcCode = "243680",
            Name = "BAUDUCCO PAO GRAOS CLASSICOS 390G",
            ProductCategoryId = category.Id,
            MinimumCases = 1,
            IsActive = true,
            IsAvailable = true,
            IsWmcActive = true,
            WmcStockAvailable = false,
            WmcInitialLoadReady = true
        };
        db.Products.Add(product);
        db.ProductInventories.Add(new ProductInventory
        {
            Product = product,
            QuantityOnHand = 8,
            QuantityReserved = 0,
            IsManuallyAdjusted = true
        });
        await db.SaveChangesAsync();

        var result = await new CatalogController(db).Products();
        var page = Assert.IsType<Orofoods.Web.Api.PagedResult<ProductResponse>>(
            Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.False(Assert.Single(page.Items).IsAvailable);
    }

    [Fact]
    public async Task Adjustment_below_reserved_quantity_is_rejected_without_writes()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Pães", Slug = "paes", IsActive = true };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();
        var product = new Product
        {
            Sku = "SKU-1", Name = "Produto", ProductCategoryId = category.Id,
            IsActive = true, IsAvailable = true
        };
        db.Products.Add(product);
        db.ProductInventories.Add(new ProductInventory
        {
            Product = product, QuantityOnHand = 5, QuantityReserved = 3
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var action = await controller.Adjust(product.Id, 2, "Ajuste inválido");

        Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(5, (await db.ProductInventories.SingleAsync()).QuantityOnHand);
        Assert.Empty(await db.InventoryAdjustments.ToListAsync());
        Assert.Equal("O saldo físico não pode ser menor que o saldo reservado.", controller.TempData["InventoryError"]);
    }

    [Fact]
    public async Task Admin_can_release_manual_override_for_next_wmc_sync()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Pães", Slug = "paes", IsActive = true };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();
        var product = new Product
        {
            Sku = "SKU-1", WmcCode = "SKU-1", Name = "Produto", ProductCategoryId = category.Id,
            IsActive = true, IsAvailable = true
        };
        db.Products.Add(product);
        db.ProductInventories.Add(new ProductInventory
        {
            Product = product, QuantityOnHand = 5, IsManuallyAdjusted = true
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var action = await controller.ResumeWmcStockSync(product.Id);

        Assert.IsType<RedirectToActionResult>(action);
        Assert.False((await db.ProductInventories.SingleAsync()).IsManuallyAdjusted);

        var sync = CreateWmcSyncService(db, [new WmcProductRecord("SKU-1", product.Name, "A", "CX", 9, 9)]);
        var syncResult = await sync.SyncStockAsync();

        Assert.Null(syncResult.ErrorMessage);
        Assert.Equal(9, (await db.ProductInventories.SingleAsync()).QuantityOnHand);
    }

    private static InventoryController CreateController(Orofoods.Web.Data.ApplicationDbContext db)
    {
        var controller = new InventoryController(
            db,
            TestIdentityFactory.CreateUserManager(db),
            NullLogger<InventoryController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "stock-admin")], "test"))
            }
        };
        controller.TempData = new TempDataDictionary(controller.HttpContext, new TestTempDataProvider());
        return controller;
    }

    private static WmcSyncService CreateWmcSyncService(
        Orofoods.Web.Data.ApplicationDbContext db,
        IReadOnlyList<WmcProductRecord> products) => new(
            db,
            new EmptyWmcCustomerReader(),
            new TestWmcProductReader(products),
            new UndiscoveredWmcSellerReader(NullLogger<UndiscoveredWmcSellerReader>.Instance),
            NullLogger<WmcSyncService>.Instance,
            TimeProvider.System);

    private sealed class EmptyWmcCustomerReader : IWmcCustomerReader
    {
        public Task<IReadOnlyList<Orofoods.Web.Integrations.Erp.Wmc.WmcCustomerRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Orofoods.Web.Integrations.Erp.Wmc.WmcCustomerRecord>>([]);
    }

    private sealed class TestWmcProductReader(IReadOnlyList<WmcProductRecord> products) : IWmcProductReader
    {
        public Task<IReadOnlyList<WmcProductRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(products);
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
