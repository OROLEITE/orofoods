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
        Assert.Equal(1, model.Confirmation!.ExistingProducts);
        Assert.Equal(["104:503813"], model.SelectedProductKeys);
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
                new WmcBrandRecord(Bimbo, "BIMBO", 3, 3)
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
                Units: Units, UnitsSnapshotComplete: true, Marca: "BIMBO")
        ];

        public Task<IReadOnlyList<WmcProductRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Rows);

        public Task<IReadOnlyList<WmcProductRecord>> GetAllByBrandAsync(short? brandCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(Rows);

        public Task<IReadOnlyList<WmcProductRecord>> GetAllByBrandWithUnitsAsync(short? brandCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(Rows);

        private static WmcProductRecord Valid(string code, string name, decimal price) =>
            new(code, name, "A", "PC", 5m, 5m,
                CodMarca: Bimbo.ToString(), PrecoVenda: price,
                Units: Units, UnitsSnapshotComplete: true, Marca: "BIMBO");

        private static IReadOnlyList<WmcProductUnitRecord> Units =>
        [
            new("PC", "PACOTE", 1m, "7899679701348", false),
            new("CX", "CAIXA", 18m, "17899679701345", true)
        ];
    }
}
