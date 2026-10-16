using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public sealed class WmcBimboStagingImportServiceTests
{
    private const string ActorId = "admin-user-1";
    private static readonly IReadOnlyDictionary<string, string> ExpectedEans = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["503768"] = "7899679700365",
        ["503775"] = "7899679701478",
        ["503777"] = "7899679701751",
        ["503805"] = "7899679700327",
        ["503811"] = "7899679701072",
        ["503813"] = "7899679701348",
        ["503833"] = "7899679702130",
        ["503838"] = "7899679702659",
        ["504338"] = "7899679702307",
        ["504342"] = "7899679704936"
    };

    [Fact]
    public async Task Simulation_validates_all_ten_products_without_writing_products_inventory_or_audit()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var service = CreateService(db, ValidBimboRows());

        var result = await service.SimulateAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(10, result.Products.Count);
        Assert.All(result.Products, product =>
        {
            Assert.True(product.CanSelect);
            Assert.Empty(product.Issues);
            Assert.Equal("PCT", product.SalesUnitCode);
        });
        Assert.Empty(await db.Products.ToListAsync());
        Assert.Empty(await db.ProductInventories.ToListAsync());
        Assert.Empty(await db.WmcProductImportRuns.ToListAsync());
    }

    [Fact]
    public async Task Selected_import_revalidates_and_imports_only_the_selected_approved_product()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var service = CreateService(db, ValidBimboRows());

        var result = await service.ImportSelectedAsync(ActorId, ["503838"]);

        Assert.True(result.Succeeded);
        Assert.Equal(["503838"], result.ImportedWmcCodes);
        Assert.Empty(result.SkippedWmcCodes);
        Assert.Equal(["503838"], await db.Products.Select(product => product.WmcCode!).ToListAsync());
        Assert.Equal(8, (await db.ProductInventories.SingleAsync()).QuantityOnHand);
    }

    [Fact]
    public async Task Selected_import_is_not_blocked_by_a_local_duplicate_for_an_unselected_code()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Produtos", Slug = "produtos", IsActive = true };
        db.ProductCategories.Add(category);
        db.Products.AddRange(
            new Product { Sku = "DUP-503768-A", WmcCode = "503768", Name = "Duplicado A", ProductCategory = category },
            new Product { Sku = "DUP-503768-B", WmcCode = "503768", Name = "Duplicado B", ProductCategory = category });
        await db.SaveChangesAsync();
        var service = CreateService(db, ValidBimboRows());

        var result = await service.ImportSelectedAsync(ActorId, ["503838"]);

        Assert.True(result.Succeeded, result.ErrorSummary);
        Assert.Equal(["503838"], result.ImportedWmcCodes);
        Assert.Equal(3, await db.Products.CountAsync());
    }

    [Fact]
    public async Task Selected_import_rejects_an_invalid_selected_product_without_partial_writes()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var rows = ValidBimboRows().Select(row => row.CodProduto == "503838" ? row with { Ean = "7899679702658" } : row).ToArray();
        var service = CreateService(db, rows);

        var result = await service.ImportSelectedAsync(ActorId, ["503838", "503833"]);

        Assert.False(result.Succeeded);
        Assert.Equal("SOURCE_EAN13_INVALID", result.ErrorSummary);
        Assert.Empty(await db.Products.ToListAsync());
        Assert.Empty(await db.ProductInventories.ToListAsync());
    }

    [Fact]
    public async Task Admin_status_allows_staging_activation_with_pending_category_and_missing_dun_when_package_data_is_valid()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var service = CreateService(db, ValidBimboRows());
        await service.ImportSelectedAsync(ActorId, ["503838"]);

        var pending = Assert.Single(await service.GetAdminProductsAsync(allowStagingActivation: true), product => product.WmcCode == "503838");
        Assert.Equal(WmcBimboProductRules.CommercialStatusPending, pending.CommercialStatus);
        Assert.False(pending.CanActivate);

        var product = await db.Products.SingleAsync(item => item.WmcCode == "503838");
        var category = await db.ProductCategories.SingleAsync();
        category.IsActive = false;
        product.ProductCategoryId = category.Id;
        product.LogisticsUnitCode = "CX";
        product.PackagesPerCase = 8;
        await db.SaveChangesAsync();

        var inactive = Assert.Single(await service.GetAdminProductsAsync(allowStagingActivation: true), item => item.WmcCode == "503838");
        Assert.Equal(WmcBimboProductRules.CommercialStatusInactive, inactive.CommercialStatus);
        Assert.True(inactive.CanActivate);
        Assert.Null(inactive.Dun14);
    }

    [Fact]
    public async Task Imports_only_the_approved_bimbo_batch_and_copies_package_stock_without_conversion()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var service = CreateService(db, ValidBimboRows());

        var result = await service.ImportAsync(ActorId);

        Assert.True(result.Succeeded);
        Assert.Equal(10, result.ImportedWmcCodes.Count);
        Assert.Empty(result.SkippedWmcCodes);
        Assert.Equal(10, db.Products.Count());
        foreach (var product in await db.Products.ToListAsync())
        {
            Assert.Equal(ExpectedEans[product.WmcCode!], product.Ean13);
            Assert.Equal(ExpectedEans[product.WmcCode!], product.Ean);
        }

        var brioche = await db.Products.SingleAsync(product => product.WmcCode == "503838");
        Assert.Equal("PCT", brioche.Unit);
        Assert.Equal("PCT", brioche.CommercialUnitCode);
        Assert.Equal("7899679702659", brioche.Ean);
        Assert.Equal("7899679702659", brioche.Ean13);
        Assert.Null(brioche.Dun14);
        Assert.Equal("CX", brioche.LogisticsUnitCode);
        Assert.Null(brioche.PackagesPerCase);
        Assert.Equal(1m, brioche.BasePrice);
        Assert.False(brioche.IsActive);
        Assert.False(brioche.IsAvailable);
        Assert.False(brioche.WmcInitialLoadReady);
        Assert.Equal(8, (await db.ProductInventories.SingleAsync(item => item.ProductId == brioche.Id)).QuantityOnHand);
        Assert.Equal(1, await db.WmcProductImportRuns.CountAsync());
        var audit = await db.WmcProductImportRuns.SingleAsync();
        Assert.Equal((short)104, audit.BrandCode);
        Assert.Equal(ActorId, audit.ActorUserId);
        Assert.Equal("Succeeded", audit.Outcome);
    }

    [Fact]
    public async Task Imports_package_factor_from_description_for_each_approved_code_without_converting_stock_or_price()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var rows = ValidBimboRows()
            .Select((row, index) => row with { Produto = $"BIMBO {row.CodProduto} 12PCX180UN", EstoqueAtual = index + 3 })
            .ToArray();
        var service = CreateService(db, rows);

        var result = await service.ImportAsync(ActorId);

        Assert.True(result.Succeeded, result.ErrorSummary);
        Assert.Equal(10, result.ImportedWmcCodes.Count);
        foreach (var row in rows)
        {
            var product = await db.Products.SingleAsync(item => item.WmcCode == row.CodProduto);
            Assert.Equal("PCT", product.Unit);
            Assert.Equal("PCT", product.CommercialUnitCode);
            Assert.Equal("CX", product.LogisticsUnitCode);
            Assert.Equal(12, product.PackagesPerCase);
            Assert.Equal(1m, product.BasePrice);
            var inventory = await db.ProductInventories.SingleAsync(item => item.ProductId == product.Id);
            Assert.Equal((int)row.EstoqueAtual!.Value, inventory.QuantityOnHand);
        }
    }

    [Theory]
    [InlineData("BIMBO 503838 180UN")]
    [InlineData("BIMBO 503838 0PCX180UN")]
    [InlineData("BIMBO 503838 12PCX0UN")]
    [InlineData("BIMBO 503838 12PCX180UN 24PCX360UN")]
    [InlineData("BIMBO 503838 12PCX180UN PCX")]
    [InlineData("BIMBO 503838 999999999999999999999PCX180UN")]
    public async Task Invalid_or_ambiguous_package_descriptions_leave_the_factor_unset(string description)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var rows = ValidBimboRows()
            .Select(row => row.CodProduto == "503838" ? row with { Produto = description } : row)
            .ToArray();
        var service = CreateService(db, rows);

        var result = await service.ImportSelectedAsync(ActorId, ["503838"]);

        Assert.True(result.Succeeded, result.ErrorSummary);
        var product = await db.Products.SingleAsync(item => item.WmcCode == "503838");
        Assert.Equal("PCT", product.CommercialUnitCode);
        Assert.Equal("CX", product.LogisticsUnitCode);
        Assert.Null(product.PackagesPerCase);
        Assert.Equal(8, (await db.ProductInventories.SingleAsync(item => item.ProductId == product.Id)).QuantityOnHand);
    }

    [Fact]
    public async Task Repeated_import_skips_existing_rows_without_overwriting_local_values()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var service = CreateService(db, ValidBimboRows());
        await service.ImportAsync(ActorId);
        var existing = await db.Products.SingleAsync(product => product.WmcCode == "503838");
        existing.Name = "Nome comercial local";
        existing.BasePrice = 2.25m;
        await db.SaveChangesAsync();

        var repeated = await service.ImportAsync(ActorId);

        Assert.True(repeated.Succeeded);
        Assert.Empty(repeated.ImportedWmcCodes);
        Assert.Equal(10, repeated.SkippedWmcCodes.Count);
        Assert.Equal("Nome comercial local", existing.Name);
        Assert.Equal(2.25m, existing.BasePrice);
        Assert.Equal(10, await db.Products.CountAsync());
    }

    [Fact]
    public async Task Missing_product_rejects_the_complete_batch_without_product_or_inventory_writes()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var service = CreateService(db, ValidBimboRows().Where(row => row.CodProduto != "503838").ToArray());

        var result = await service.ImportAsync(ActorId);

        Assert.False(result.Succeeded);
        Assert.Empty(await db.Products.ToListAsync());
        Assert.Empty(await db.ProductInventories.ToListAsync());
        Assert.Equal("Rejected", (await db.WmcProductImportRuns.SingleAsync()).Outcome);
    }

    [Theory]
    [InlineData("inactive", "I", 8)]
    [InlineData("unknown", "?", 8)]
    [InlineData("negative stock", "A", -1)]
    [InlineData("fractional stock", "A", 1.5)]
    public async Task Invalid_product_or_stock_rejects_batch_without_partial_writes(string _, string situation, double stock)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var rows = ValidBimboRows().Select(row => row.CodProduto == "503838"
            ? row with { Situacao = situation, EstoqueAtual = (decimal)stock }
            : row).ToArray();
        var service = CreateService(db, rows);

        var result = await service.ImportAsync(ActorId);

        Assert.False(result.Succeeded);
        Assert.Empty(await db.Products.ToListAsync());
        Assert.Empty(await db.ProductInventories.ToListAsync());
    }

    [Fact]
    public async Task Duplicate_wmc_rows_reject_batch_before_writes()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var rows = ValidBimboRows().Append(ValidBimboRows()[0]).ToArray();
        var service = CreateService(db, rows);

        var result = await service.ImportAsync(ActorId);

        Assert.False(result.Succeeded);
        Assert.Empty(await db.Products.ToListAsync());
        Assert.Empty(await db.ProductInventories.ToListAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("7899679702658")]
    [InlineData("78996797026590")]
    public async Task Invalid_CODBARRASFAB_rejects_batch_before_product_or_inventory_writes(string? barcode)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var rows = ValidBimboRows().Select(row => row.CodProduto == "503838" ? row with { Ean = barcode } : row).ToArray();
        var service = CreateService(db, rows);

        var result = await service.ImportAsync(ActorId);

        Assert.False(result.Succeeded);
        Assert.Equal("SOURCE_EAN13_INVALID", result.ErrorSummary);
        Assert.Empty(await db.Products.ToListAsync());
        Assert.Empty(await db.ProductInventories.ToListAsync());
    }

    [Fact]
    public async Task Known_brioche_EAN_must_match_the_confirmed_physical_label()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var rows = ValidBimboRows().Select(row => row.CodProduto == "503838"
            ? row with { Ean = "4006381333931" }
            : row).ToArray();
        var service = CreateService(db, rows);

        var result = await service.ImportAsync(ActorId);

        Assert.False(result.Succeeded);
        Assert.Equal("SOURCE_EAN13_MISMATCH", result.ErrorSummary);
        Assert.Empty(await db.Products.ToListAsync());
        Assert.Empty(await db.ProductInventories.ToListAsync());
    }

    [Fact]
    public async Task Any_source_EAN_that_differs_from_its_homologation_reference_rejects_the_batch()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var rows = ValidBimboRows().Select(row => row.CodProduto == "503768"
            ? row with { Ean = "4006381333931" }
            : row).ToArray();
        var service = CreateService(db, rows);

        var result = await service.ImportAsync(ActorId);

        Assert.False(result.Succeeded);
        Assert.Equal("SOURCE_EAN13_MISMATCH", result.ErrorSummary);
        Assert.Empty(await db.Products.ToListAsync());
        Assert.Empty(await db.ProductInventories.ToListAsync());
    }

    [Fact]
    public async Task Database_failure_rolls_back_products_and_inventory_and_records_failure()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailBimboImport BEFORE INSERT ON Products WHEN NEW.WmcCode = '503838' BEGIN SELECT RAISE(ABORT, 'synthetic test failure'); END;");
        var service = CreateService(db, ValidBimboRows());

        var result = await service.ImportAsync(ActorId);

        Assert.False(result.Succeeded);
        Assert.Empty(await db.Products.ToListAsync());
        Assert.Empty(await db.ProductInventories.ToListAsync());
        Assert.Equal("Failed", (await db.WmcProductImportRuns.SingleAsync()).Outcome);
        Assert.DoesNotContain("synthetic test failure", result.ErrorSummary ?? "", StringComparison.Ordinal);
    }

    private static WmcBimboStagingImportService CreateService(ApplicationDbContext db, IReadOnlyList<WmcProductRecord> rows) =>
        new(db, new FixedWmcProductReader(rows), NullLogger<WmcBimboStagingImportService>.Instance, TimeProvider.System);

    private static WmcProductRecord[] ValidBimboRows() => WmcBimboStagingImportService.ApprovedProductCodes
        .Select((code, index) => new WmcProductRecord(
            code,
            $"BIMBO {code}",
            "A",
            "PC",
            index + 1,
            index + 1,
            Ean: ExpectedEans[code],
            CodMarca: "104"))
        .ToArray();

    private sealed class FixedWmcProductReader(IReadOnlyList<WmcProductRecord> rows) : IWmcProductReader
    {
        public Task<IReadOnlyList<WmcProductRecord>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(rows);
        public Task<IReadOnlyList<WmcProductRecord>> GetAllByBrandAsync(short? brandCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(brandCode == 104 ? rows : Array.Empty<WmcProductRecord>() as IReadOnlyList<WmcProductRecord>);
    }
}
