using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Services.Integrations;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Controllers;

public sealed class WmcProductSelectionControllerTests
{
    private const short Bimbo = 104;
    private const short Aviacao = 205;

    [Fact]
    public async Task Empty_selection_reports_empty_state_only_after_continue_attempt()
    {
        await using var db = await CreateDbAsync();
        var controller = CreateController(db);

        var result = await controller.WmcConfirmSelection([Bimbo], null, null, null, CancellationToken.None);

        var model = AssertProductsError(controller, result, "Selecione ao menos um produto elegível para continuar.");
        Assert.Empty(model.SelectedProductKeys!);
    }

    [Fact]
    public async Task Missing_selection_key_reports_stale_preview_and_preserves_posted_key()
    {
        await using var db = await CreateDbAsync();
        var controller = CreateController(db);

        var result = await controller.WmcConfirmSelection(
            [Bimbo], null, ["104:NOT-IN-PREVIA"], null, CancellationToken.None);

        var model = AssertProductsError(controller, result, "A prévia de produtos está desatualizada. Consulte os produtos novamente antes de continuar.");
        Assert.Equal(["104:NOT-IN-PREVIA"], model.SelectedProductKeys);
    }

    [Fact]
    public async Task Blocked_selection_reports_blocked_state_and_keeps_candidate_disabled()
    {
        await using var db = await CreateDbAsync();
        var controller = CreateController(db);

        var result = await controller.WmcConfirmSelection(
            [Bimbo], null, ["104:503815"], null, CancellationToken.None);

        var model = AssertProductsError(controller, result, "A seleção contém produto bloqueado. Remova-o e selecione apenas produtos elegíveis.");
        var blocked = Assert.Single(model.Candidates!, candidate => candidate.WmcCode == "503815");
        Assert.False(blocked.Selectable);
        Assert.Equal(["104:503815"], model.SelectedProductKeys);
    }

    [Fact]
    public async Task Manual_import_of_inactive_selection_is_rejected_without_an_import_audit()
    {
        await using var db = await CreateDbAsync();
        var controller = CreateController(db);

        var result = await controller.WmcImportProducts(
            [Bimbo], null, ["104:503816"], null, "true", true, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WmcPreviewPageModel>(view.Model);
        Assert.Equal("Products", model.Step);
        var error = Assert.Single(controller.ModelState[string.Empty]!.Errors);
        Assert.Contains("inativo", error.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        var inactive = Assert.Single(model.Candidates!, candidate => candidate.WmcCode == "503816");
        Assert.False(inactive.Selectable);
        Assert.Equal("inactive", inactive.SituationKind);
        Assert.Empty(db.WmcProductImportAudits);
    }

    [Fact]
    public async Task Existing_eligible_selection_reaches_confirmation_without_implying_update()
    {
        await using var db = await CreateDbAsync();
        var controller = CreateController(db);

        var result = await controller.WmcConfirmSelection(
            [Bimbo], null, ["104:503813"], null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WmcPreviewPageModel>(view.Model);
        Assert.Equal("Confirmation", model.Step);
        Assert.NotNull(model.Confirmation);
        Assert.Equal(1, model.Confirmation!.ExistingWithChanges);
        Assert.Equal(0, model.Confirmation.NewProducts);
        Assert.Equal(1, model.Confirmation.ExistingWithChanges);
        Assert.Equal(0, model.Confirmation.ExistingWithoutChanges);
        Assert.Equal(["104:503813"], model.SelectedProductKeys);
    }

    [Fact]
    public async Task New_aviacao_product_is_counted_only_as_new_and_keeps_official_confirmation_data()
    {
        await using var db = await CreateDbAsync();
        var controller = CreateController(db);

        var result = await controller.WmcConfirmSelection(
            [Aviacao], null, [$"{Aviacao}:101900"], null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WmcPreviewPageModel>(view.Model);
        var summary = Assert.IsType<WmcProductImportSummary>(model.Confirmation);
        Assert.Equal(1, summary.Consulted);
        Assert.Equal(1, summary.Selected);
        Assert.Equal(0, summary.Blocked);
        Assert.Equal(1, summary.NewProducts);
        Assert.Equal(0, summary.ExistingWithChanges);
        Assert.Equal(0, summary.ExistingWithoutChanges);

        var candidate = Assert.Single(model.Candidates!);
        Assert.Equal("101900", candidate.WmcCode);
        Assert.Equal("UN", candidate.UnitProposal!.PrimaryUnitCode);
        Assert.Equal("CX", candidate.UnitProposal.MasterUnitCode);
        Assert.Equal(24m, candidate.UnitProposal.ProposedMasterFactor);
        Assert.Equal("7899679701348", candidate.UnitProposal.ProposedEan13);
        Assert.Equal("17899679701345", candidate.UnitProposal.ProposedDun14);
        Assert.Equal(24m, candidate.UnitProposal.ProposedPackageQuantity);
        Assert.Equal(24m, candidate.UnitProposal.ProposedConversionQuantity);
        Assert.Contains(candidate.Units, unit => unit.UnitCode == "CX" && unit.IsMaster && unit.Factor == 24m);
    }

    [Fact]
    public async Task Confirmation_composes_read_only_presentation_without_changing_selection_or_reimport_contract()
    {
        await using var db = await CreateDbAsync();
        var controller = CreateController(db);

        var result = await controller.WmcConfirmSelection(
            [Aviacao], null, [$"{Aviacao}:101900"], null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WmcPreviewPageModel>(view.Model);
        var presentation = Assert.Single(model.ConfirmationPresentations!);
        Assert.Equal("101900", presentation.Candidate.WmcCode);
        Assert.True(presentation.IsNewProduct);
        Assert.Equal("available", presentation.WmcReadStatus);
        Assert.Null(presentation.LocalState);
        Assert.Equal(["205:101900"], model.SelectedProductKeys);
        Assert.Equal("Confirmation", model.Step);
    }

    [Fact]
    public async Task Product_stage_defaults_to_active_and_reports_global_situation_counters()
    {
        await using var db = await CreateDbAsync();
        var controller = CreateController(db);

        var result = await controller.WmcProducts([Bimbo], null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WmcPreviewPageModel>(view.Model);
        Assert.Equal("Products", model.Step);
        Assert.Equal(WmcProductSituationFilter.Active, model.ProductSituationFilter);
        Assert.NotNull(model.ProductCounters);
        Assert.Equal(4, model.ProductCounters!.Total);
        Assert.Equal(3, model.ProductCounters.Active);
        Assert.Equal(1, model.ProductCounters.Inactive);
        Assert.Equal(2, model.ProductCounters.Eligible);
        Assert.Equal(2, model.ProductCounters.Blocked);
    }

    [Fact]
    public async Task Product_filter_state_is_preserved_through_confirmation()
    {
        await using var db = await CreateDbAsync();
        var controller = CreateController(db);

        var result = await controller.WmcConfirmSelection(
            [Bimbo], null, ["104:503813"], null, CancellationToken.None, WmcProductSituationFilter.All);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WmcPreviewPageModel>(view.Model);
        Assert.Equal("Confirmation", model.Step);
        Assert.Equal(WmcProductSituationFilter.All, model.ProductSituationFilter);
    }

    [Fact]
    public async Task Missing_reimport_authorization_keeps_existing_product_unchanged()
    {
        await using var db = await CreateDbAsync();
        var controller = CreateController(db);

        var result = await controller.WmcImportProducts(
            [Bimbo], null, ["104:503813"], null, null, true, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WmcPreviewPageModel>(view.Model);
        Assert.Equal("Result", model.Step);
        Assert.NotNull(model.ImportResult);
        Assert.Equal(0, model.ImportResult!.Updated);
        Assert.Equal(1, model.ImportResult.Unchanged);
        Assert.Contains(model.ImportResult.Items, item => item.WmcCode == "503813"
            && item.Status == "Ignorado"
            && item.Detail!.Contains("reimporta\u00e7\u00e3o recusada", StringComparison.Ordinal));
        var product = Assert.Single(db.Products.Where(item => item.WmcCode == "503813"));
        Assert.Equal("P\u00e3o BIMBO antigo", product.Name);
        Assert.Equal(1m, product.BasePrice);
        Assert.Equal(["104:503813"], model.SelectedProductKeys);
    }

    [Fact]
    public async Task Explicit_reimport_authorization_updates_existing_product_and_preserves_selection()
    {
        await using var db = await CreateDbAsync();
        var controller = CreateController(db);

        var result = await controller.WmcImportProducts(
            [Bimbo], null, ["104:503813"], null, "true", true, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WmcPreviewPageModel>(view.Model);
        Assert.Equal("Result", model.Step);
        Assert.NotNull(model.ImportResult);
        Assert.Equal(1, model.ImportResult!.Updated);
        Assert.Contains(model.ImportResult.Items, item => item.WmcCode == "503813" && item.Status == "Atualizado");
        var product = Assert.Single(db.Products.Where(item => item.WmcCode == "503813"));
        Assert.Equal("P\u00e3o BIMBO existente", product.Name);
        Assert.Equal(10m, product.BasePrice);
        Assert.Equal(["104:503813"], model.SelectedProductKeys);
    }

    [Fact]
    public async Task Equal_wmc_price_is_presented_as_unchanged_when_the_existing_record_matches()
    {
        await using var db = await CreateDbAsync();
        var product = Assert.Single(db.Products.Local);
        product.Name = "P\u00e3o BIMBO existente";
        product.WmcBrandCode = Bimbo.ToString();
        product.Brand = "BIMBO";
        product.Unit = "PC";
        product.Ean = "7899679701348";
        product.Ean13 = "7899679701348";
        product.Dun14 = "17899679701345";
        product.BasePrice = 10m;
        db.WmcProductUnits.AddRange(
            new WmcProductUnit { ProductId = product.Id, UnitCode = "PC", Description = "PACOTE", Factor = 1m, Barcode = "7899679701348", IsMaster = false },
            new WmcProductUnit { ProductId = product.Id, UnitCode = "CX", Description = "CAIXA", Factor = 18m, Barcode = "17899679701345", IsMaster = true });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.WmcConfirmSelection(
            [Bimbo], null, ["104:503813"], null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WmcPreviewPageModel>(view.Model);
        var candidate = Assert.Single(model.Candidates!, item => item.WmcCode == "503813");
        Assert.Equal("J\u00e1 cadastrado", candidate.Status);
        Assert.DoesNotContain(candidate.Changes, change => change.Field == "Pre\u00e7o base");
        Assert.Equal(10m, candidate.PriceProposal!.CurrentBasePrice);
        Assert.Equal(10m, candidate.PriceProposal.ProposedPrice);
        Assert.Equal(0, model.Confirmation!.ExistingWithChanges);
        Assert.Equal(1, model.Confirmation.ExistingWithoutChanges);
    }

    [Fact]
    public async Task Manual_import_of_blocked_selection_returns_products_without_import_audit()
    {
        await using var db = await CreateDbAsync();
        var controller = CreateController(db);

        var result = await controller.WmcImportProducts(
            [Bimbo], null, ["104:503815"], null, "true", true, CancellationToken.None);

        var model = AssertProductsError(controller, result, "A seleção contém produto bloqueado. Remova-o e selecione apenas produtos elegíveis.");
        Assert.Equal("Products", model.Step);
        Assert.Empty(db.WmcProductImportAudits);
    }

    [Fact]
    public async Task Manual_import_of_empty_selection_returns_products_without_import_audit()
    {
        await using var db = await CreateDbAsync();
        var controller = CreateController(db);

        var result = await controller.WmcImportProducts(
            [Bimbo], null, null, null, "false", true, CancellationToken.None);

        AssertProductsError(controller, result, "Selecione ao menos um produto elegível para continuar.");
        Assert.Empty(db.WmcProductImportAudits);
    }

    [Fact]
    public async Task Manual_import_of_stale_selection_returns_products_without_import_audit()
    {
        await using var db = await CreateDbAsync();
        var controller = CreateController(db);

        var result = await controller.WmcImportProducts(
            [Bimbo], null, ["104:NOT-IN-PREVIA"], null, "false", true, CancellationToken.None);

        AssertProductsError(controller, result, "A prévia de produtos está desatualizada. Consulte os produtos novamente antes de continuar.");
        Assert.Empty(db.WmcProductImportAudits);
    }

    private static WmcPreviewPageModel AssertProductsError(
        IntegrationsController controller,
        IActionResult result,
        string expectedMessage)
    {
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WmcPreviewPageModel>(view.Model);
        Assert.Equal("Products", model.Step);
        var error = Assert.Single(controller.ModelState[string.Empty]!.Errors);
        Assert.Equal(expectedMessage, error.ErrorMessage);
        return model;
    }

    private static async Task<ApplicationDbContext> CreateDbAsync()
    {
        var db = await TestDbContextFactory.CreateAsync();
        db.ProductCategories.Add(new ProductCategory { Name = "Local", Slug = "local" });
        db.Products.Add(new Product
        {
            WmcCode = "503813",
            Sku = "503813",
            Name = "Pão BIMBO antigo",
            ProductCategoryId = 1,
            BasePrice = 1m,
            IsActive = true,
            IsAvailable = true,
            IsWmcActive = true,
            WmcStockAvailable = true
        });
        await db.SaveChangesAsync();
        return db;
    }

    private static IntegrationsController CreateController(ApplicationDbContext db)
    {
        var brandReader = new FakeBrandReader();
        var productReader = new FakeProductReader();
        var controller = new IntegrationsController(
            db,
            null!,
            new WmcSyncCoordinator(),
            null!,
            Options.Create(new WmcSyncOptions()),
            brandReader,
            new WmcProductPreviewService(db, productReader),
            new WmcProductImportService(db, productReader),
            new WmcProductConfirmationPresentationService(db, productReader),
            NullLogger<IntegrationsController>.Instance,
            null!,
            null!,
            null!,
            Options.Create(new WmcInitialLoadReleaseOptions()),
            null,
            null,
            null);
        var context = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        controller.TempData = new TempDataDictionary(context, Mock.Of<ITempDataProvider>());
        return controller;
    }

    private sealed class FakeBrandReader : IWmcBrandReader
    {
        public Task<IReadOnlyList<WmcBrandRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WmcBrandRecord>>([
                new WmcBrandRecord(Bimbo, "BIMBO", 4, 3),
                new WmcBrandRecord(Aviacao, "AVIAÇÃO", 1, 1)
            ]);
    }

    private sealed class FakeProductReader : IWmcProductReader
    {
        private static readonly IReadOnlyList<WmcProductRecord> Rows =
        [
            Valid("503813", "Pão BIMBO existente", 10m),
            Valid("503814", "Pão BIMBO novo", 11m),
            new WmcProductRecord(
                "503815", "Pão BIMBO bloqueado", "A", "PC", 5m, 5m,
                CodMarca: Bimbo.ToString(), PrecoVenda: 0m,
                Units: Units, UnitsSnapshotComplete: true, Marca: "BIMBO"),
            new WmcProductRecord(
                "503816", "Pão BIMBO inativo", "I", "PC", 5m, 5m,
                CodMarca: Bimbo.ToString(), PrecoVenda: 12m,
                Units: Units, UnitsSnapshotComplete: true, Marca: "BIMBO"),
            new WmcProductRecord(
                "101900", "Biscoito AVIAÇÃO", "A", "UN", 24m, 24m,
                CodMarca: Aviacao.ToString(), UnAltern: "CX", QtdeEmbalagem: 24m, QtdeConversao: 24m,
                PrecoVenda: 12m, Ean13: "7899679701348", Dun14: "17899679701345",
                Units: AviacaoUnits, UnitsSnapshotComplete: true, Marca: "AVIAÇÃO")
        ];

        public Task<IReadOnlyList<WmcProductRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Rows);

        public Task<IReadOnlyList<WmcProductRecord>> GetAllByBrandAsync(short? brandCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(ByBrand(brandCode));

        public Task<IReadOnlyList<WmcProductRecord>> GetAllByBrandWithUnitsAsync(short? brandCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(ByBrand(brandCode));

        private static IReadOnlyList<WmcProductRecord> ByBrand(short? brandCode) =>
            brandCode is null
                ? Rows
                : Rows.Where(row => row.CodMarca == brandCode.Value.ToString()).ToArray();

        private static WmcProductRecord Valid(string code, string name, decimal price) =>
            new(code, name, "A", "PC", 5m, 5m,
                CodMarca: Bimbo.ToString(), PrecoVenda: price,
                Units: Units, UnitsSnapshotComplete: true, Marca: "BIMBO");

        private static IReadOnlyList<WmcProductUnitRecord> Units =>
        [
            new("PC", "PACOTE", 1m, "7899679701348", false),
            new("CX", "CAIXA", 18m, "17899679701345", true)
        ];

        private static IReadOnlyList<WmcProductUnitRecord> AviacaoUnits =>
        [
            new("UN", "UNIDADE", 1m, "7899679701348", false),
            new("CX", "CAIXA", 24m, "17899679701345", true)
        ];
    }
}
