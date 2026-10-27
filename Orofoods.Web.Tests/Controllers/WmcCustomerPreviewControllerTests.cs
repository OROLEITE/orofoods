using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Services.Integrations;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Controllers;

public sealed class WmcCustomerPreviewControllerTests
{
    [Fact]
    public async Task Customer_preview_returns_the_wmc_page_model_when_firebird_is_unavailable()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var products = new EmptyProductReader();
        var customerService = new WmcCustomerImportService(
            db,
            new UnavailableCustomerReader(),
            NullLogger<WmcCustomerImportService>.Instance,
            TimeProvider.System);
        var controller = new IntegrationsController(
            db,
            null!,
            new WmcSyncCoordinator(),
            null!,
            Options.Create(new WmcSyncOptions()),
            new EmptyBrandReader(),
            new WmcProductPreviewService(db, products),
            new WmcProductImportService(db, products, Options.Create(new WmcPriceImportOptions())),
            NullLogger<IntegrationsController>.Instance,
            null!,
            null!,
            null!,
            Options.Create(new WmcInitialLoadReleaseOptions()),
            null,
            null,
            customerService);
        var context = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        controller.TempData = new TempDataDictionary(context, Mock.Of<ITempDataProvider>());

        var result = await controller.WmcCustomerPreview("V1", CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<WmcPreviewPageModel>(view.Model);
        var preview = Assert.IsType<WmcCustomerImportPreview>(controller.ViewBag.WmcCustomerPreview);
        Assert.NotNull(preview.ErrorMessage);
    }

    private sealed class EmptyBrandReader : IWmcBrandReader
    {
        public Task<IReadOnlyList<WmcBrandRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WmcBrandRecord>>([]);
    }

    private sealed class EmptyProductReader : IWmcProductReader
    {
        public Task<IReadOnlyList<WmcProductRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WmcProductRecord>>([]);
    }

    private sealed class UnavailableCustomerReader : IWmcCustomerReader
    {
        public Task<IReadOnlyList<WmcCustomerRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Firebird mirror unavailable.");

        public Task<IReadOnlyList<WmcCustomerRecord>> GetBySellerAsync(string sellerCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Firebird mirror unavailable.");

        public Task<IReadOnlyList<string>> GetSellerCodesAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Firebird mirror unavailable.");
    }
}
