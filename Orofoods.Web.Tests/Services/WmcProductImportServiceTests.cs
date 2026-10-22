using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public sealed class WmcProductImportServiceTests
{
    [Fact]
    public async Task Preview_exposes_PC_and_CX_factor_and_barcode_from_the_official_unit_snapshot()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.ProductCategories.Add(new ProductCategory { Name = "Local", Slug = "local" });
        await db.SaveChangesAsync();
        var reader = new FakeReader([new WmcProductRecord("503813", "Pao BIMBO", "A", "PC", 42.5m, null,
            Ean: "7899679701348",
            Units: [new("PC", "PACOTE", 1m, "7899679701348", false), new("CX", "CAIXA", 18m, "17899679701345", true)])]);

        var candidate = Assert.Single(await new WmcProductImportService(db, reader).GetCandidatesAsync(1));

        Assert.Equal("PC", candidate.Unit);
        Assert.Equal(42.5m, candidate.Stock);
        Assert.Equal("7899679701348", candidate.Ean);
        Assert.NotNull(candidate.UnitProposal);
        Assert.Equal("7899679701348", candidate.UnitProposal!.ProposedEan13);
        Assert.Equal("17899679701345", candidate.UnitProposal.ProposedDun14);
        Assert.Equal(1m, candidate.UnitProposal.ProposedPcFactor);
        Assert.Equal(18m, candidate.UnitProposal.ProposedCxFactor);
        Assert.Collection(candidate.Units,
            unit => { Assert.Equal("CX", unit.UnitCode); Assert.Equal(18m, unit.Factor); Assert.Equal("17899679701345", unit.Barcode); Assert.True(unit.IsMaster); },
            unit => { Assert.Equal("PC", unit.UnitCode); Assert.Equal(1m, unit.Factor); Assert.Equal("7899679701348", unit.Barcode); Assert.False(unit.IsMaster); });
    }

    [Fact]
    public async Task Reimport_maps_valid_official_pc_and_cx_identifiers_without_touching_price_or_stock()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.ProductCategories.Add(new ProductCategory { Name = "Local", Slug = "local" });
        var product = new Product
        {
            WmcCode = "503813", Sku = "WMC-503813", Name = "Produto", ProductCategoryId = 1,
            Ean = "EAN-LOCAL", Ean13 = "7890000000000", Dun14 = "17890000000000", BasePrice = 1m
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        db.ProductInventories.Add(new ProductInventory { ProductId = product.Id, QuantityOnHand = 360, QuantityReserved = 4 });
        await db.SaveChangesAsync();
        var reader = new FakeReader([new WmcProductRecord("503813", "Produto", "A", "PC", null, null,
            Units: [new("PC", "PACOTE", 1m, "7899679701348", false), new("CX", "CAIXA", 18m, "17899679701345", true)])]);

        var result = await new WmcProductImportService(db, reader).ImportAsync(104, ["503813"], true);

        var persisted = await db.Products.AsNoTracking().SingleAsync(item => item.WmcCode == "503813");
        var units = await db.WmcProductUnits.AsNoTracking().Where(unit => unit.ProductId == product.Id).ToListAsync();
        var inventory = await db.ProductInventories.AsNoTracking().SingleAsync(item => item.ProductId == product.Id);
        Assert.Equal("Succeeded", result.Status);
        Assert.Equal("7899679701348", persisted.Ean);
        Assert.Equal("7899679701348", persisted.Ean13);
        Assert.Equal("17899679701345", persisted.Dun14);
        Assert.Equal(1m, persisted.BasePrice);
        Assert.Equal(360, inventory.QuantityOnHand);
        Assert.Equal(4, inventory.QuantityReserved);
        Assert.Equal(18m, Assert.Single(units, unit => unit.UnitCode == "CX").Factor);
    }

    [Fact]
    public async Task Duplicate_official_units_fail_without_overwriting_local_identifiers()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.ProductCategories.Add(new ProductCategory { Name = "Local", Slug = "local" });
        var product = new Product
        {
            WmcCode = "503813", Sku = "WMC-503813", Name = "Produto", ProductCategoryId = 1,
            Ean = "EAN-LOCAL", Ean13 = "7890000000000", Dun14 = "17890000000000"
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var reader = new FakeReader([new WmcProductRecord("503813", "Produto", "A", "PC", null, null,
            Units: [new("PC", "PACOTE", 1m, "7899679701348", false), new("PC", "PACOTE 2", 1m, "7899679701348", false), new("CX", "CAIXA", 18m, "17899679701345", true)])]);

        var result = await new WmcProductImportService(db, reader).ImportAsync(104, ["503813"], true);

        var persisted = await db.Products.AsNoTracking().SingleAsync(item => item.WmcCode == "503813");
        Assert.Equal("Failed", result.Status);
        Assert.Equal("EAN-LOCAL", persisted.Ean);
        Assert.Equal("7890000000000", persisted.Ean13);
        Assert.Equal("17890000000000", persisted.Dun14);
    }

    [Fact]
    public async Task Empty_or_invalid_unit_barcodes_preserve_local_identifiers_but_keep_valid_cx_factor()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.ProductCategories.Add(new ProductCategory { Name = "Local", Slug = "local" });
        var product = new Product
        {
            WmcCode = "503813", Sku = "WMC-503813", Name = "Produto", ProductCategoryId = 1,
            Ean = "EAN-LOCAL", Ean13 = "7890000000000", Dun14 = "17890000000000"
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        db.WmcProductUnits.AddRange(
            new WmcProductUnit { ProductId = product.Id, UnitCode = "PC", Factor = 2m, Barcode = "EAN-LOCAL-UNIT" },
            new WmcProductUnit { ProductId = product.Id, UnitCode = "CX", Factor = 6m, Barcode = "DUN-LOCAL-UNIT", IsMaster = true });
        await db.SaveChangesAsync();
        var reader = new FakeReader([new WmcProductRecord("503813", "Produto", "A", "PC", null, null,
            Units: [new("PC", "PACOTE", 1m, null, false), new("CX", "CAIXA", 18m, "invalido", true)])]);

        var result = await new WmcProductImportService(db, reader).ImportAsync(104, ["503813"], true);

        var persisted = await db.Products.AsNoTracking().SingleAsync(item => item.WmcCode == "503813");
        var units = await db.WmcProductUnits.AsNoTracking().Where(unit => unit.ProductId == product.Id).ToListAsync();
        Assert.Equal("Succeeded", result.Status);
        Assert.Equal("EAN-LOCAL", persisted.Ean);
        Assert.Equal("7890000000000", persisted.Ean13);
        Assert.Equal("17890000000000", persisted.Dun14);
        Assert.Equal(1m, Assert.Single(units, unit => unit.UnitCode == "PC").Factor);
        Assert.Equal("EAN-LOCAL-UNIT", Assert.Single(units, unit => unit.UnitCode == "PC").Barcode);
        Assert.Equal(18m, Assert.Single(units, unit => unit.UnitCode == "CX").Factor);
        Assert.Equal("DUN-LOCAL-UNIT", Assert.Single(units, unit => unit.UnitCode == "CX").Barcode);
    }

    [Fact]
    public async Task Reimport_updates_WMC_fields_and_preserves_local_prices_packaging_images_and_commercial_settings()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.ProductCategories.Add(new ProductCategory { Name = "Local", Slug = "local" });
        db.PriceTables.Add(new PriceTable { Name = "Cliente especial" });
        await db.SaveChangesAsync();
        var product = new Product
        {
            WmcCode = "P1", Sku = "LOCAL-SKU", Name = "Antigo", ProductCategoryId = 1, Brand = "Marca antiga",
            Unit = "UN antiga", BasePrice = 20, PromotionalPrice = 18, IsPromotional = true, IsActive = true,
            IsAvailable = true, Ean = "EAN local", WmcPackageQuantity = 2, WmcConversionQuantity = 3,
            UnitsPerPackage = 8, UnitsPerCase = 24, MinimumCases = 2, IsFeatured = true,
            Images = [new ProductImage { Url = "/uploads/local-product.webp", AltText = "Imagem local", IsPrimary = true }]
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var priceTable = await db.PriceTables.SingleAsync();
        db.PriceTableItems.Add(new PriceTableItem { PriceTableId = priceTable.Id, ProductId = product.Id, Price = 17m, PromotionalPrice = 15m });
        await db.SaveChangesAsync();
        var reader = new FakeReader([new WmcProductRecord("P1", "Novo nome", "A", "CX", null, null, "EAN1", "9", "FD", 6, 7, null, 1m, Marca: "Marca WMC")]);
        var service = new WmcProductImportService(db, reader);
        var candidate = (await service.GetCandidatesAsync(9)).Single();
        Assert.DoesNotContain(candidate.Changes, change => change.Field.Contains("preço", StringComparison.OrdinalIgnoreCase));

        var result = await service.ImportAsync(9, ["P1"], true);

        Assert.Equal("Succeeded", result.Status);
        Assert.Equal(1, result.Updated);
        Assert.Equal("Novo nome", product.Name);
        Assert.Equal("Marca WMC", product.Brand);
        Assert.Equal("EAN local", product.Ean);
        Assert.Equal("FD", product.WmcAlternateUnit);
        Assert.Equal(20m, product.BasePrice);
        Assert.Equal(18m, product.PromotionalPrice);
        Assert.Equal(2m, product.WmcPackageQuantity);
        Assert.Equal(3m, product.WmcConversionQuantity);
        Assert.Equal(8, product.UnitsPerPackage);
        Assert.Equal(24, product.UnitsPerCase);
        Assert.Equal(2, product.MinimumCases);
        Assert.True(product.IsPromotional);
        Assert.True(product.IsFeatured);
        Assert.Equal("LOCAL-SKU", product.Sku);
        Assert.Equal(1, product.ProductCategoryId);
        Assert.True(product.IsActive);
        Assert.True(product.IsAvailable);
        var priceTableItem = await db.PriceTableItems.AsNoTracking().SingleAsync();
        Assert.Equal(17m, priceTableItem.Price);
        Assert.Equal(15m, priceTableItem.PromotionalPrice);
        var image = await db.ProductImages.AsNoTracking().SingleAsync();
        Assert.Equal("/uploads/local-product.webp", image.Url);
        Assert.Equal("Imagem local", image.AltText);
        Assert.Equal(1, db.WmcProductImportAudits.Single().Updated);
    }

    [Fact]
    public async Task No_reimport_creates_only_new_selection_and_never_touches_unselected_products()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.ProductCategories.Add(new ProductCategory { Name = "Local", Slug = "local" });
        await db.SaveChangesAsync();
        var existing = new Product { WmcCode = "OLD", Sku = "LOCAL", Name = "Local", ProductCategoryId = 1, BasePrice = 17 };
        var untouched = new Product { WmcCode = "UNSELECTED", Sku = "OTHER", Name = "Unselected", ProductCategoryId = 1 };
        db.Products.AddRange(existing, untouched);
        await db.SaveChangesAsync();
        var reader = new FakeReader([
            new WmcProductRecord("OLD", "WMC changed", "A", "CX", null, null, CodMarca: "4", PrecoVenda: 3),
            new WmcProductRecord("NEW", "Novo", "A", "CX", null, null, CodMarca: "4", PrecoVenda: 2),
            new WmcProductRecord("UNSELECTED", "Not selected changed", "A", "CX", null, null, CodMarca: "4", PrecoVenda: 99)
        ]);

        var result = await new WmcProductImportService(db, reader).ImportAsync(4, ["OLD", "NEW"], false);

        Assert.Equal(1, result.Created);
        Assert.Equal(1, result.Unchanged);
        Assert.Equal("Local", existing.Name);
        Assert.Equal(17, existing.BasePrice);
        Assert.Equal("Unselected", untouched.Name);
        Assert.Equal(3, await db.Products.CountAsync());
    }

    [Fact]
    public async Task Missing_source_values_do_not_overwrite_and_explicit_zero_factor_is_preserved()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.ProductCategories.Add(new ProductCategory { Name = "Local", Slug = "local" });
        await db.SaveChangesAsync();
        var product = new Product { WmcCode = "P", Sku = "P", Name = "Local name", ProductCategoryId = 1, BasePrice = 5, Ean = "keep", WmcPackageQuantity = 4 };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var reader = new FakeReader([new WmcProductRecord("P", "", null, " ", null, null, null, null, null, 0, null, null, null)]);

        await new WmcProductImportService(db, reader).ImportAsync(3, ["P"], true);

        Assert.Equal("Local name", product.Name);
        Assert.Equal("keep", product.Ean);
        Assert.Equal(5, product.BasePrice);
        Assert.Equal(4, product.WmcPackageQuantity);
        Assert.True(product.IsWmcActive);
    }

    [Fact]
    public async Task Zero_WMC_price_and_legacy_packaging_values_do_not_overwrite_local_values_and_unknown_situation_blocks_WMC_sales()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.ProductCategories.Add(new ProductCategory { Name = "Local", Slug = "local" });
        await db.SaveChangesAsync();
        var product = new Product
        {
            WmcCode = "P0", Sku = "P0", Name = "Current", ProductCategoryId = 1,
            BasePrice = 7, WmcPackageQuantity = 2, WmcConversionQuantity = 4,
            IsWmcActive = true, WmcStockAvailable = true, IsActive = true, IsAvailable = true
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var reader = new FakeReader([new WmcProductRecord("P0", "Updated", "?", "CX", null, null, QtdeEmbalagem: 0, QtdeConversao: 0, PrecoVenda: 0)]);

        var result = await new WmcProductImportService(db, reader).ImportAsync(3, ["P0"], true);

        Assert.Equal("Succeeded", result.Status);
        Assert.Equal(7, product.BasePrice);
        Assert.Equal(2, product.WmcPackageQuantity);
        Assert.Equal(4, product.WmcConversionQuantity);
        Assert.False(product.IsWmcActive);
        Assert.True(product.IsActive);
        Assert.True(product.IsAvailable);
        Assert.True(product.WmcStockAvailable);
    }

    [Theory]
    [InlineData("503833", 1)]
    [InlineData("503838", 1)]
    public async Task Official_unit_factors_are_imported_from_PRODUTOS_UNCONV_snapshot(string code, decimal cxFactor)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.ProductCategories.Add(new ProductCategory { Name = "Local", Slug = "local" });
        await db.SaveChangesAsync();
        var product = new Product { WmcCode = code, Sku = code, Name = "Produto", ProductCategoryId = 1, WmcPackageQuantity = 99, WmcConversionQuantity = 88 };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var reader = new FakeReader([new WmcProductRecord(code, "Produto", "A", "PC", null, null,
            QtdeEmbalagem: 6, QtdeConversao: 12,
            Units: [new("PC", "PACOTE", 1m, "7890000000001", false), new("CX", "CAIXA", cxFactor, "17890000000001", true)])]);

        var result = await new WmcProductImportService(db, reader).ImportAsync(5, [code], true);

        var units = await db.WmcProductUnits.AsNoTracking().Where(unit => unit.ProductId == product.Id).ToListAsync();
        Assert.Equal("Succeeded", result.Status);
        Assert.Equal(1m, Assert.Single(units, unit => unit.UnitCode == "PC").Factor);
        Assert.Equal(cxFactor, Assert.Single(units, unit => unit.UnitCode == "CX").Factor);
        Assert.Equal(99m, product.WmcPackageQuantity);
        Assert.Equal(88m, product.WmcConversionQuantity);
    }

    [Fact]
    public async Task Changed_selection_is_rejected_and_audited_without_product_writes()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.ProductCategories.Add(new ProductCategory { Name = "Local", Slug = "local" });
        await db.SaveChangesAsync();
        var result = await new WmcProductImportService(db, new FakeReader([])).ImportAsync(3, ["GONE"], true);

        Assert.Equal("Failed", result.Status);
        Assert.Empty(db.Products);
        Assert.Equal("Failed", db.WmcProductImportAudits.Single().Status);
    }

    [Fact]
    public async Task Reimport_reconciles_the_complete_official_unit_list_by_product_and_unit()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.ProductCategories.Add(new ProductCategory { Name = "Local", Slug = "local" });
        await db.SaveChangesAsync();
        var product = new Product { WmcCode = "P1", Sku = "P1", Name = "Produto", ProductCategoryId = 1, BasePrice = 5 };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        db.WmcProductUnits.AddRange(
            new WmcProductUnit { ProductId = product.Id, UnitCode = "PC", Description = "PACOTE", Factor = 2m, Barcode = "EAN-ANTIGO", IsMaster = false },
            new WmcProductUnit { ProductId = product.Id, UnitCode = "CX", Description = "CAIXA", Factor = 6m, Barcode = "DUN-ANTIGO", IsMaster = true },
            new WmcProductUnit { ProductId = product.Id, UnitCode = "UN", Description = "UNIDADE", Factor = 1m, Barcode = "OBSOLETO", IsMaster = false });
        await db.SaveChangesAsync();
        var reader = new FakeReader([new WmcProductRecord("P1", "Produto", "A", "PC", null, null,
            Units: [new("PC", "PACOTE", 1m, "7899679701348", false), new("CX", "CAIXA", 18m, "17899679701345", true)])]);

        var result = await new WmcProductImportService(db, reader).ImportAsync(3, ["P1"], true);

        var units = await db.WmcProductUnits.AsNoTracking().Where(unit => unit.ProductId == product.Id).OrderBy(unit => unit.UnitCode).ToListAsync();
        Assert.Equal("Succeeded", result.Status);
        Assert.Equal(1, result.Updated);
        Assert.Equal(2, units.Count);
        Assert.Collection(units,
            unit => { Assert.Equal("CX", unit.UnitCode); Assert.Equal(18m, unit.Factor); Assert.Equal("17899679701345", unit.Barcode); Assert.True(unit.IsMaster); },
            unit => { Assert.Equal("PC", unit.UnitCode); Assert.Equal(1m, unit.Factor); Assert.Equal("7899679701348", unit.Barcode); Assert.False(unit.IsMaster); });
        Assert.DoesNotContain(units, unit => unit.UnitCode == "UN");
    }

    [Fact]
    public async Task Declining_reimport_preserves_existing_units_and_imports_units_for_new_products_only()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.ProductCategories.Add(new ProductCategory { Name = "Local", Slug = "local" });
        await db.SaveChangesAsync();
        var existing = new Product { WmcCode = "OLD", Sku = "OLD", Name = "Local", ProductCategoryId = 1, BasePrice = 17 };
        var unselected = new Product { WmcCode = "KEEP", Sku = "KEEP", Name = "Keep", ProductCategoryId = 1, BasePrice = 8 };
        db.Products.AddRange(existing, unselected);
        await db.SaveChangesAsync();
        db.WmcProductUnits.AddRange(
            new WmcProductUnit { ProductId = existing.Id, UnitCode = "CX", Description = "CAIXA", Factor = 4m, Barcode = "OLD-CX", IsMaster = true },
            new WmcProductUnit { ProductId = unselected.Id, UnitCode = "PC", Description = "PACOTE", Factor = 1m, Barcode = "KEEP-PC", IsMaster = false });
        await db.SaveChangesAsync();
        var reader = new FakeReader([
            new WmcProductRecord("OLD", "Alteração ignorada", "A", "PC", null, null, Units: [new("CX", "CAIXA", 18m, "NEW-CX", true)]),
            new WmcProductRecord("NEW", "Novo", "A", "PC", null, null, Units: [new("PC", "PACOTE", 1m, "NEW-PC", false), new("CX", "CAIXA", 12m, "NEW-CX", true)]),
            new WmcProductRecord("KEEP", "Não selecionado", "A", "PC", null, null, Units: [new("PC", "PACOTE", 1m, "CHANGED-PC", false)])]);

        var result = await new WmcProductImportService(db, reader).ImportAsync(3, ["OLD", "NEW"], false);

        var existingUnit = await db.WmcProductUnits.AsNoTracking().SingleAsync(unit => unit.ProductId == existing.Id);
        var unselectedUnit = await db.WmcProductUnits.AsNoTracking().SingleAsync(unit => unit.ProductId == unselected.Id);
        var newProduct = await db.Products.SingleAsync(item => item.WmcCode == "NEW");
        var newUnits = await db.WmcProductUnits.AsNoTracking().Where(unit => unit.ProductId == newProduct.Id).OrderBy(unit => unit.UnitCode).ToListAsync();
        Assert.Equal(1, result.Created);
        Assert.Equal(1, result.Unchanged);
        Assert.Equal("Local", existing.Name);
        Assert.Equal(4m, existingUnit.Factor);
        Assert.Equal("OLD-CX", existingUnit.Barcode);
        Assert.Equal("KEEP-PC", unselectedUnit.Barcode);
        Assert.Equal(2, newUnits.Count);
        Assert.Contains(newUnits, unit => unit.UnitCode == "CX" && unit.Factor == 12m && unit.IsMaster);
        Assert.Contains(newUnits, unit => unit.UnitCode == "PC" && unit.Factor == 1m && !unit.IsMaster);
    }

    [Fact]
    public async Task Reimport_does_not_remove_units_when_the_unit_snapshot_is_incomplete()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.ProductCategories.Add(new ProductCategory { Name = "Local", Slug = "local" });
        await db.SaveChangesAsync();
        var product = new Product { WmcCode = "P1", Sku = "P1", Name = "Produto", ProductCategoryId = 1 };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        db.WmcProductUnits.AddRange(
            new WmcProductUnit { ProductId = product.Id, UnitCode = "PC", Description = "PACOTE", Factor = 1m, Barcode = "EAN-PC" },
            new WmcProductUnit { ProductId = product.Id, UnitCode = "CX", Description = "CAIXA", Factor = 18m, Barcode = "DUN-CX", IsMaster = true });
        await db.SaveChangesAsync();
        var reader = new FakeReader(
            [new WmcProductRecord("P1", "Produto", "A", "PC", null, null, Units: [])],
            unitsSnapshotComplete: false);

        var result = await new WmcProductImportService(db, reader).ImportAsync(3, ["P1"], true);

        var units = await db.WmcProductUnits.AsNoTracking().Where(unit => unit.ProductId == product.Id).OrderBy(unit => unit.UnitCode).ToListAsync();
        Assert.Equal("Failed", result.Status);
        Assert.Equal(2, units.Count);
        Assert.Contains(units, unit => unit.UnitCode == "PC" && unit.Barcode == "EAN-PC");
        Assert.Contains(units, unit => unit.UnitCode == "CX" && unit.Barcode == "DUN-CX");
    }

    [Fact]
    public async Task Reimport_does_not_remove_local_units_when_the_official_unit_list_is_empty()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.ProductCategories.Add(new ProductCategory { Name = "Local", Slug = "local" });
        await db.SaveChangesAsync();
        var product = new Product { WmcCode = "P1", Sku = "P1", Name = "Produto", ProductCategoryId = 1, Ean = "EAN-LOCAL" };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        db.WmcProductUnits.Add(new WmcProductUnit { ProductId = product.Id, UnitCode = "CX", Factor = 18m, Barcode = "DUN-LOCAL", IsMaster = true });
        await db.SaveChangesAsync();
        var reader = new FakeReader(
            [new WmcProductRecord("P1", "Produto", "A", "PC", null, null, Ean: "EAN-WMC", Units: [])]);

        var result = await new WmcProductImportService(db, reader).ImportAsync(3, ["P1"], true);

        var units = await db.WmcProductUnits.AsNoTracking().Where(unit => unit.ProductId == product.Id).ToListAsync();
        Assert.Equal("Succeeded", result.Status);
        Assert.Equal("EAN-LOCAL", product.Ean);
        var unit = Assert.Single(units);
        Assert.Equal("CX", unit.UnitCode);
        Assert.Equal("DUN-LOCAL", unit.Barcode);
        Assert.Equal(18m, unit.Factor);
    }

    [Fact]
    public async Task Reimport_does_not_remove_units_when_the_unit_query_fails()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.ProductCategories.Add(new ProductCategory { Name = "Local", Slug = "local" });
        await db.SaveChangesAsync();
        var product = new Product { WmcCode = "P1", Sku = "P1", Name = "Produto", ProductCategoryId = 1 };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        db.WmcProductUnits.AddRange(
            new WmcProductUnit { ProductId = product.Id, UnitCode = "PC", Description = "PACOTE", Factor = 1m, Barcode = "EAN-PC" },
            new WmcProductUnit { ProductId = product.Id, UnitCode = "CX", Description = "CAIXA", Factor = 18m, Barcode = "DUN-CX", IsMaster = true });
        await db.SaveChangesAsync();
        var reader = new FakeReader(
            [new WmcProductRecord("P1", "Produto", "A", "PC", null, null)],
            unitsQueryFailure: new InvalidOperationException("Firebird unit query failed"));

        var result = await new WmcProductImportService(db, reader).ImportAsync(3, ["P1"], true);

        var units = await db.WmcProductUnits.AsNoTracking().Where(unit => unit.ProductId == product.Id).OrderBy(unit => unit.UnitCode).ToListAsync();
        Assert.Equal("Failed", result.Status);
        Assert.Equal(2, units.Count);
        Assert.Contains(units, unit => unit.UnitCode == "PC" && unit.Barcode == "EAN-PC");
        Assert.Contains(units, unit => unit.UnitCode == "CX" && unit.Barcode == "DUN-CX");
    }

    [Fact]
    public async Task Validated_price_updates_base_and_explicit_table_without_overwriting_promotion_and_is_idempotent()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.ProductCategories.Add(new ProductCategory { Name = "Local", Slug = "local" });
        var table = new PriceTable { Name = "Lista 1 Orofoods", IsActive = true };
        db.PriceTables.Add(table);
        await db.SaveChangesAsync();
        var product = new Product
        {
            WmcCode = "503813", Sku = "503813", Name = "Produto", ProductCategoryId = 1,
            BasePrice = 1m, PromotionalPrice = 18.99m, IsPromotional = true
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        db.PriceTableItems.Add(new PriceTableItem { PriceTableId = table.Id, ProductId = product.Id, Price = 1m, PromotionalPrice = 17.50m });
        await db.SaveChangesAsync();
        var reader = new FakeReader([new WmcProductRecord("503813", "Produto", "A", "PC", null, null, PrecoVenda: 20.39m,
            Units: [new("PC", "PACOTE", 1m, "7899679701348", false), new("CX", "CAIXA", 18m, "17899679701345", true)])]);
        var options = Options.Create(new WmcPriceImportOptions
        {
            SourcePriceValidated = true,
            SourcePriceUnitCode = "PC",
            TargetPriceTableId = table.Id
        });
        var service = new WmcProductImportService(db, reader, options);

        var candidate = Assert.Single(await service.GetCandidatesAsync(104));
        Assert.True(candidate.PriceProposal!.SafeToApply);
        Assert.Equal(20.39m, candidate.PriceProposal.ProposedPrice);
        var result = await service.ImportAsync(104, ["503813"], true);
        var repeat = await service.ImportAsync(104, ["503813"], true);

        var persistedItem = await db.PriceTableItems.AsNoTracking().SingleAsync();
        Assert.Equal("Succeeded", result.Status);
        Assert.Equal(1, result.Updated);
        Assert.Equal(20.39m, product.BasePrice);
        Assert.Equal(20.39m, persistedItem.Price);
        Assert.Equal(17.50m, persistedItem.PromotionalPrice);
        Assert.Equal(0, repeat.Updated);
        Assert.Equal(1, repeat.Unchanged);
    }

    [Fact]
    public async Task Price_update_is_blocked_without_proven_lista_1_and_units_still_persist()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.ProductCategories.Add(new ProductCategory { Name = "Local", Slug = "local" });
        var product = new Product
        {
            WmcCode = "503813", Sku = "503813", Name = "Produto", ProductCategoryId = 1,
            BasePrice = 1m, Ean = "local", Ean13 = "7890000000000", Dun14 = "17890000000000"
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var reader = new FakeReader([new WmcProductRecord("503813", "Produto", "A", "PC", null, null, PrecoVenda: 20.39m,
            Units: [new("PC", "PACOTE", 1m, "7899679701348", false), new("CX", "CAIXA", 18m, "17899679701345", true)])]);

        var result = await new WmcProductImportService(db, reader).ImportAsync(104, ["503813"], true);

        var persisted = await db.Products.AsNoTracking().SingleAsync();
        Assert.Equal("Succeeded", result.Status);
        Assert.True(result.Blocked > 0);
        Assert.Contains(result.Items, item => item.Detail != null && item.Detail.Contains("Lista 1", StringComparison.Ordinal));
        Assert.Equal(1m, persisted.BasePrice);
        Assert.Equal("7899679701348", persisted.Ean13);
        Assert.Equal("17899679701345", persisted.Dun14);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Invalid_wmc_price_is_blocked_without_changing_local_price(decimal sourcePrice)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.ProductCategories.Add(new ProductCategory { Name = "Local", Slug = "local" });
        var product = new Product { WmcCode = "P", Sku = "P", Name = "Produto", ProductCategoryId = 1, BasePrice = 7m };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var reader = new FakeReader([new WmcProductRecord("P", "Produto", "A", "PC", null, null, PrecoVenda: sourcePrice,
            Units: [new("PC", "PACOTE", 1m, "7899679701348", false), new("CX", "CAIXA", 18m, "17899679701345", true)])]);

        var result = await new WmcProductImportService(db, reader).ImportAsync(1, ["P"], true);

        Assert.Equal("Succeeded", result.Status);
        Assert.Equal(7m, product.BasePrice);
        Assert.Contains(result.Items, item => item.Detail != null && item.Detail.Contains("zero ou negativo", StringComparison.Ordinal));
    }

    private sealed class FakeReader(
        IReadOnlyList<WmcProductRecord> rows,
        bool unitsSnapshotComplete = true,
        Exception? unitsQueryFailure = null) : IWmcProductReader
    {
        public Task<IReadOnlyList<WmcProductRecord>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(rows);
        public Task<IReadOnlyList<WmcProductRecord>> GetAllByBrandAsync(short? brandCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WmcProductRecord>>(rows);
        public Task<IReadOnlyList<WmcProductRecord>> GetAllByBrandWithUnitsAsync(short? brandCode, CancellationToken cancellationToken = default) =>
            unitsQueryFailure is null
                ? Task.FromResult<IReadOnlyList<WmcProductRecord>>(rows.Select(row => row with { Units = row.Units ?? [], UnitsSnapshotComplete = unitsSnapshotComplete }).ToArray())
                : Task.FromException<IReadOnlyList<WmcProductRecord>>(unitsQueryFailure);
    }
}
