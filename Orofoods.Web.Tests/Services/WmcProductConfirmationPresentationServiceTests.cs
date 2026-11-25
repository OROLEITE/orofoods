using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public sealed class WmcProductConfirmationPresentationServiceTests
{
    [Fact]
    public async Task New_active_avicao_candidate_keeps_snapshot_and_shows_read_only_wmc_diagnostics()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var candidate = Candidate("101900", 104);
        var reader = new FakeReader(new Dictionary<short, IReadOnlyList<WmcProductRecord>>
        {
            [104] = [new WmcProductRecord(
                "101900", "Produto AVIAÇÃO", "A", "UN", 7, 9,
                PrecoVenda: 10m, EstoqueReservado: 2m, CodMarca: "104", Marca: "AVIAÇÃO")]
        });

        var result = await new WmcProductConfirmationPresentationService(db, reader)
            .BuildAsync([104], [candidate]);

        var presentation = Assert.Single(result);
        Assert.Same(candidate, presentation.Candidate);
        Assert.True(presentation.IsNewProduct);
        Assert.Null(presentation.LocalState);
        Assert.Contains("IsActive=true", presentation.NewProductExpectedState, StringComparison.Ordinal);
        Assert.Contains("IsAvailable=false", presentation.NewProductExpectedState, StringComparison.Ordinal);
        Assert.Equal("available", presentation.WmcReadStatus);
        Assert.Equal(7m, presentation.WmcStock!.EstoqueDisponivel);
        Assert.Equal(9m, presentation.WmcStock.EstoqueAtual);
        Assert.Equal(2m, presentation.WmcStock.EstoqueReservado);
        Assert.Equal("Consulta WMC somente leitura", presentation.WmcStock.Source);
        Assert.Equal("não confirmada", presentation.WmcStock.UnitSemantics);
        Assert.True(presentation.WmcStock.ConsultedAtUtc <= DateTimeOffset.UtcNow);
        Assert.Equal(1, reader.BrandCalls);
    }

    [Fact]
    public async Task Existing_product_presents_local_flags_reserved_stock_and_known_blockers_without_tracking()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.ProductCategories.Add(new ProductCategory { Name = "Local", Slug = "local" });
        var product = new Product
        {
            WmcCode = "101900", Sku = "101900", Name = "Produto local", ProductCategoryId = 1,
            Unit = "UN", WmcAlternateUnit = "CX", WmcPackageQuantity = 24, WmcConversionQuantity = 24,
            Ean = "7899679701348", Ean13 = "7899679701348", Dun14 = "17899679701345",
            IsWmcActive = true, IsActive = false, IsAvailable = false,
            WmcInitialLoadReady = false, WmcStockAvailable = false
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        db.ProductInventories.Add(new ProductInventory
        {
            ProductId = product.Id, QuantityOnHand = 3, QuantityReserved = 5, IsManuallyAdjusted = true
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var candidate = Candidate("101900", 104, product.Id, "Bloqueio do snapshot original.");
        var reader = new FakeReader(new Dictionary<short, IReadOnlyList<WmcProductRecord>>
        {
            [104] = [new WmcProductRecord("101900", "Produto AVIAÇÃO", "A", "UN", 8, 10, EstoqueReservado: 2)]
        });

        var result = await new WmcProductConfirmationPresentationService(db, reader)
            .BuildAsync([104], [candidate]);

        var presentation = Assert.Single(result);
        Assert.False(presentation.IsNewProduct);
        Assert.Equal("available", presentation.LocalReadStatus);
        Assert.Equal(product.Id, presentation.LocalState!.ProductId);
        Assert.True(presentation.LocalState.IsWmcActive);
        Assert.False(presentation.LocalState.IsActive);
        Assert.False(presentation.LocalState.IsAvailable);
        Assert.False(presentation.LocalState.WmcInitialLoadReady);
        Assert.False(presentation.LocalState.WmcStockAvailable);
        Assert.Equal(3, presentation.LocalState.QuantityOnHand);
        Assert.Equal(5, presentation.LocalState.QuantityReserved);
        Assert.Equal(0, presentation.LocalState.AvailableQuantity);
        Assert.True(presentation.LocalState.IsManuallyAdjusted);
        Assert.Contains(presentation.KnownBlockers, blocker => blocker.Contains("Bloqueio do snapshot original", StringComparison.Ordinal));
        Assert.Contains(presentation.KnownBlockers, blocker => blocker.Contains("inativo", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Missing_local_inventory_is_not_presented_as_zero()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.ProductCategories.Add(new ProductCategory { Name = "Local", Slug = "local" });
        var product = new Product { WmcCode = "P-1", Sku = "P-1", Name = "Produto", ProductCategoryId = 1 };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var candidate = Candidate("P-1", 7, product.Id);
        var reader = new FakeReader(new Dictionary<short, IReadOnlyList<WmcProductRecord>>
        {
            [7] = [new WmcProductRecord("P-1", "Produto", "A", "PT", 0, 0)]
        });

        var presentation = Assert.Single(await new WmcProductConfirmationPresentationService(db, reader)
            .BuildAsync([7], [candidate]));

        Assert.Equal("not-informed", presentation.LocalState!.InventoryReadStatus);
        Assert.Null(presentation.LocalState.QuantityOnHand);
        Assert.Null(presentation.LocalState.QuantityReserved);
        Assert.Null(presentation.LocalState.AvailableQuantity);
    }

    [Fact]
    public async Task Wmc_read_failure_keeps_candidate_and_mandatory_blockers_with_unavailable_values()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var candidate = Candidate("FAIL-1", 104, blockReason: "Produto bloqueado no snapshot original.");
        var reader = new FakeReader(new Dictionary<short, IReadOnlyList<WmcProductRecord>>(), new InvalidOperationException("falha Firebird controlada"));

        var presentation = Assert.Single(await new WmcProductConfirmationPresentationService(db, reader)
            .BuildAsync([104], [candidate]));

        Assert.Same(candidate, presentation.Candidate);
        Assert.Equal("unavailable", presentation.WmcReadStatus);
        Assert.Null(presentation.WmcStock);
        Assert.Contains(presentation.KnownBlockers, blocker => blocker.Contains("Produto bloqueado", StringComparison.Ordinal));
    }

    private static WmcProductImportCandidate Candidate(string code, short brandCode, int? productId = null, string? blockReason = null) =>
        new(
            code,
            "Produto de teste",
            "7899679701348",
            "A",
            brandCode.ToString(),
            "Marca de teste",
            "UN",
            productId,
            productId is null ? "Novo" : "Com alterações",
            true,
            [],
            [new WmcProductUnitPreview("UN", "UNIDADE", 1m, "7899679701348", false)],
            Stock: 7m,
            PrecoVenda: 10m,
            BlockReason: blockReason,
            SituationKind: WmcProductSituationFilter.Active);

    private sealed class FakeReader(
        IReadOnlyDictionary<short, IReadOnlyList<WmcProductRecord>> rowsByBrand,
        Exception? failure = null) : IWmcProductReader
    {
        public int BrandCalls { get; private set; }

        public Task<IReadOnlyList<WmcProductRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WmcProductRecord>>(rowsByBrand.Values.SelectMany(rows => rows).ToArray());

        public Task<IReadOnlyList<WmcProductRecord>> GetAllByBrandAsync(short? brandCode, CancellationToken cancellationToken = default)
        {
            BrandCalls++;
            if (failure is not null) return Task.FromException<IReadOnlyList<WmcProductRecord>>(failure);
            return Task.FromResult<IReadOnlyList<WmcProductRecord>>(rowsByBrand.GetValueOrDefault(brandCode ?? 0, []));
        }
    }
}
