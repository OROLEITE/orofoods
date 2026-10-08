using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public sealed class WmcProductPreviewServiceTests
{
    [Fact]
    public async Task Preview_filters_products_by_real_brand_without_writing()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.ProductCategories.Add(new ProductCategory { Name = "Teste", Slug = "teste" });
        await db.SaveChangesAsync();
        db.Products.Add(new Product { WmcCode = "P-EXISTING", Sku = "SKU", Name = "Existente", ProductCategoryId = 1 });
        await db.SaveChangesAsync();
        var reader = new FakeProductReader(
        [
            new WmcProductRecord("P-EXISTING", "Existente", "A", "CX", null, 4, CodMarca: "5"),
            new WmcProductRecord("P-OTHER", "Outra", "A", "CX", null, 2, CodMarca: "4")
        ]);
        var service = new WmcProductPreviewService(db, reader);

        var result = await service.PreviewAsync(5);

        Assert.Equal((short)5, reader.LastBrandCode);
        Assert.Equal(1, result.ProductsRead);
        Assert.Equal(0, result.NewProducts);
        Assert.Equal(1, result.ExistingProducts);
        Assert.Empty(db.WmcSyncRuns);
        Assert.Single(db.Products);
    }

    [Fact]
    public async Task Preview_reports_duplicates_stock_problems_and_projected_availability()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var reader = new FakeProductReader(
        [
            new WmcProductRecord(" dup ", "Um", "A", "CX", null, 2, CodMarca: "5"),
            new WmcProductRecord("DUP", "Dois", "I", "CX", null, -1, CodMarca: "5"),
            new WmcProductRecord("FRACTION", "Fracionado", "A", "CX", null, 1.5m, CodMarca: "5"),
            new WmcProductRecord("", "Inválido", "X", "CX", null, null, CodMarca: "5")
        ]);
        var service = new WmcProductPreviewService(db, reader);

        var result = await service.PreviewAsync(null);

        Assert.Equal(4, result.ProductsRead);
        Assert.Equal(1, result.DuplicateCodes);
        Assert.Equal(1, result.InvalidCodes);
        Assert.Equal(2, result.ActiveProducts);
        Assert.Equal(1, result.InactiveProducts);
        Assert.Equal(1, result.UnknownSituations);
        Assert.Equal(2, result.ActiveEligibleProducts);
        Assert.Equal(1, result.InactiveIgnoredProducts);
        Assert.Equal(1, result.InvalidSituations);
        Assert.Equal(1, result.StockProblems);
        Assert.Equal(0, result.ProjectedAvailableProducts);
        Assert.Equal(4, result.ProjectedBlockedProducts);
        Assert.Empty(db.WmcSyncRuns);
        Assert.Empty(db.Products);
    }

    [Fact]
    public async Task Preview_reports_read_errors_without_writing()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var service = new WmcProductPreviewService(db, new ThrowingProductReader());

        var result = await service.PreviewAsync(null);

        Assert.NotNull(result.ErrorMessage);
        Assert.Empty(db.Products);
        Assert.Empty(db.WmcSyncRuns);
    }

    private sealed class FakeProductReader(IReadOnlyList<WmcProductRecord> rows) : IWmcProductReader
    {
        public short? LastBrandCode { get; private set; }

        public Task<IReadOnlyList<WmcProductRecord>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(rows);

        public Task<IReadOnlyList<WmcProductRecord>> GetAllByBrandAsync(short? brandCode, CancellationToken cancellationToken = default)
        {
            LastBrandCode = brandCode;
            return Task.FromResult<IReadOnlyList<WmcProductRecord>>(brandCode is null
                ? rows
                : rows.Where(row => short.TryParse(row.CodMarca, out var code) && code == brandCode).ToList());
        }
    }

    private sealed class ThrowingProductReader : IWmcProductReader
    {
        public Task<IReadOnlyList<WmcProductRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("read failed");
    }
}
