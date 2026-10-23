using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Collections.Concurrent;
using System.Data.Common;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Moq;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Models.Validation;
using Orofoods.Web.Services.Catalog;
using Orofoods.Web.Services.Storage;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Controllers;

public sealed class AdminProductsControllerTests
{
    [Fact]
    public async Task Edit_saves_product_without_inventing_an_optional_description_and_returns_to_grid()
    {
        var description = typeof(Product).GetProperty(nameof(Product.Description))!;
        var required = description.GetCustomAttribute<RequiredAttribute>();
        var displayFormat = description.GetCustomAttribute<DisplayFormatAttribute>();

        Assert.NotNull(required);
        Assert.True(required.AllowEmptyStrings);
        Assert.NotNull(displayFormat);
        Assert.False(displayFormat.ConvertEmptyStringToNull);

        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Teste", Slug = "teste", IsActive = true };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();
        var controller = CreateController(db);
        SetTempData(controller);
        var input = new Product
        {
            Sku = "BIMBO-EMPTY-DESC",
            Name = "Produto BIMBO sem descrição confirmada",
            Brand = "BIMBO",
            ProductCategoryId = category.Id,
            Description = string.Empty,
            Unit = "PC",
            IsActive = false
        };

        var result = Assert.IsType<RedirectToActionResult>(await controller.Edit(input, "BIMBO", includeInactive: true, page: 2));

        Assert.Equal(nameof(ProductsController.Index), result.ActionName);
        Assert.Equal("BIMBO", result.RouteValues!["q"]);
        Assert.Equal(true, result.RouteValues["includeInactive"]);
        Assert.Equal(2, result.RouteValues["page"]);
        Assert.Equal(string.Empty, (await db.Products.AsNoTracking().SingleAsync()).Description);
        Assert.False((await db.Products.AsNoTracking().SingleAsync()).IsActive);
    }

    [Fact]
    public async Task Edit_does_not_save_a_product_when_numeric_binding_value_is_invalid()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Teste", Slug = "teste", IsActive = true };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();
        var controller = CreateController(db);
        SetTempData(controller);
        var modelBindingOptions = new MvcOptions();
        AdminModelBindingMessages.Configure(modelBindingOptions);
        controller.ModelState.AddModelError(
            nameof(Product.BasePrice),
            modelBindingOptions.ModelBindingMessageProvider.AttemptedValueIsInvalidAccessor("value", nameof(Product.BasePrice)));

        var result = await controller.Edit(new Product
        {
            Sku = "BIMBO-INVALID-PRICE",
            Name = "Produto BIMBO",
            Brand = "BIMBO",
            ProductCategoryId = category.Id,
            Description = string.Empty,
            IsActive = false
        });

        Assert.IsType<ViewResult>(result);
        Assert.Contains(
            controller.ModelState.Values.SelectMany(state => state.Errors),
            error => error.ErrorMessage == "O valor 'value' não é válido para o campo preço base.");
        Assert.Empty(await db.Products.ToListAsync());
    }

    [Fact]
    public async Task Edit_keeps_sku_name_and_category_requirements_with_portuguese_messages()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var controller = CreateController(db);
        var result = await controller.Edit(new Product
        {
            Sku = string.Empty,
            Name = string.Empty,
            ProductCategoryId = 0,
            Description = string.Empty
        });

        Assert.IsType<ViewResult>(result);
        Assert.Equal("Informe o SKU do produto.", Assert.Single(controller.ModelState[nameof(Product.Sku)]!.Errors).ErrorMessage);
        Assert.Equal("Informe o nome do produto.", Assert.Single(controller.ModelState[nameof(Product.Name)]!.Errors).ErrorMessage);
        Assert.Equal("Selecione uma categoria para o produto.", Assert.Single(controller.ModelState[nameof(Product.ProductCategoryId)]!.Errors).ErrorMessage);
        Assert.Empty(await db.Products.ToListAsync());
    }

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
            error => error.ErrorMessage.Contains("código WMC", StringComparison.OrdinalIgnoreCase));
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

    [Theory]
    [InlineData("a")]
    [InlineData("cafe")]
    [InlineData("CAFÉ")]
    [InlineData("sku-77")]
    [InlineData("503813")]
    [InlineData("bIm")]
    public async Task Index_searches_single_character_and_all_product_fields_without_case_or_accent_sensitivity(string search)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Teste", Slug = "teste", IsActive = true };
        db.Products.Add(new Product
        {
            Sku = "SKU-77",
            WmcCode = "503813",
            Name = "Pão de Café",
            Brand = "BIMBO",
            ProductCategory = category,
            Unit = "PCT"
        });
        await db.SaveChangesAsync();

        var result = Assert.IsType<ViewResult>(await CreateController(db).Index(search));
        var products = Assert.IsAssignableFrom<IReadOnlyList<Product>>(result.Model);

        Assert.Single(products);
        Assert.Equal("SKU-77", products[0].Sku);
        Assert.Equal(1, result.ViewData["TotalItems"]);
        Assert.Equal(1, result.ViewData["Page"]);
    }

    [Fact]
    public async Task Index_uses_a_shallow_sqlite_search_expression_for_accent_and_case_insensitive_queries()
    {
        var sqlCapture = new SqlCommandCaptureInterceptor();
        await using var db = await TestDbContextFactory.CreateAsync(sqlCapture);
        var category = new ProductCategory { Name = "Teste", Slug = "teste", IsActive = true };
        db.Products.Add(new Product
        {
            Sku = "SKU-ACCENT",
            WmcCode = "503813",
            Name = "Pão de Café",
            Brand = "BIMBO",
            ProductCategory = category,
            Unit = "PCT"
        });
        await db.SaveChangesAsync();

        var result = await CreateController(db).Index("CAFE");

        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<Product>>(Assert.IsType<ViewResult>(result).Model));
        Assert.True(sqlCapture.Commands.Any(command => command.Contains("translate", StringComparison.OrdinalIgnoreCase)), string.Join("\n---\n", sqlCapture.Commands));
        Assert.All(sqlCapture.Commands.Where(command => command.Contains("translate", StringComparison.OrdinalIgnoreCase)), command =>
            Assert.InRange(command.Split("replace(", StringSplitOptions.None).Length - 1, 0, 24));
    }

    [Fact]
    public void Product_search_translation_generates_postgresql_translate_sql_without_a_database_connection()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused")
            .Options;
        using var db = new ApplicationDbContext(options);

        var sql = db.Products
            .Where(product => product.Name != null &&
                ApplicationDbContext.TranslateSearchText(product.Name, "áÁ", "aa")!.ToLower().Contains("cafe"))
            .ToQueryString();

        Assert.Contains("translate(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIKE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Index_filters_full_catalog_before_paging_and_exposes_pagination_totals()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Teste", Slug = "teste", IsActive = true };
        for (var index = 1; index <= 26; index++)
        {
            db.Products.Add(new Product
            {
                Sku = $"SKU-{index:000}",
                Name = $"Produto {index:000}",
                Brand = "needle",
                ProductCategory = category,
                Unit = "PCT"
            });
        }
        await db.SaveChangesAsync();

        var result = Assert.IsType<ViewResult>(await CreateController(db).Index("NEEDLE", page: 2));
        var products = Assert.IsAssignableFrom<IReadOnlyList<Product>>(result.Model);

        Assert.Single(products);
        Assert.Equal("SKU-026", products[0].Sku);
        Assert.Equal(26, result.ViewData["TotalItems"]);
        Assert.Equal(2, result.ViewData["Page"]);
        Assert.Equal(2, result.ViewData["TotalPages"]);
    }

    [Fact]
    public async Task Index_returns_an_empty_first_page_when_search_has_no_matches()
    {
        await using var db = await TestDbContextFactory.CreateAsync();

        var result = Assert.IsType<ViewResult>(await CreateController(db).Index("inexistente", page: 4));
        var products = Assert.IsAssignableFrom<IReadOnlyList<Product>>(result.Model);

        Assert.Empty(products);
        Assert.Equal(0, result.ViewData["TotalItems"]);
        Assert.Equal(0, result.ViewData["TotalPages"]);
        Assert.Equal(1, result.ViewData["Page"]);
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
            IsAvailable = false,
            IsWmcActive = false,
            WmcStockAvailable = false,
            WmcInitialLoadReady = false
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
        db.ChangeTracker.Clear();
        var saved = await db.Products.AsNoTracking().SingleAsync(item => item.Id == product.Id);
        Assert.True(saved.IsActive);
        Assert.False(saved.IsAvailable);
        Assert.False(saved.IsWmcActive);
        Assert.False(saved.WmcStockAvailable);
        Assert.False(saved.WmcInitialLoadReady);
    }

    [Fact]
    public async Task Edit_invalid_barcode_keeps_activation_unsaved_and_reports_validation_error()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Teste", Slug = "teste", IsActive = true };
        var product = new Product
        {
            Sku = "BIMBO-TEST-1",
            WmcCode = "503768",
            WmcBrandCode = "104",
            Name = "Produto BIMBO",
            Brand = "BIMBO",
            Unit = "PC",
            ProductCategory = category,
            IsActive = false,
            IsAvailable = false
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        SetTempData(controller);
        var result = await controller.Edit(new Product
        {
            Id = product.Id,
            Sku = product.Sku,
            WmcCode = product.WmcCode,
            WmcBrandCode = product.WmcBrandCode,
            Name = product.Name,
            Brand = product.Brand,
            Unit = product.Unit,
            ProductCategoryId = category.Id,
            Ean13 = "1234567890123",
            IsActive = true,
            IsAvailable = false,
            Accent = product.Accent
        });

        Assert.IsType<ViewResult>(result);
        Assert.Contains(
            controller.ModelState.Values.SelectMany(state => state.Errors),
            error => error.ErrorMessage.Contains("GS1", StringComparison.OrdinalIgnoreCase));
        Assert.Null(controller.TempData["ProductSuccess"]);

        db.ChangeTracker.Clear();
        var saved = await db.Products.AsNoTracking().SingleAsync(item => item.Id == product.Id);
        Assert.False(saved.IsActive);
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

    private sealed class SqlCommandCaptureInterceptor : DbCommandInterceptor
    {
        public ConcurrentBag<string> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
