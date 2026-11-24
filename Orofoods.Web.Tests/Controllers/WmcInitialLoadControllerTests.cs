using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Services.Integrations;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Controllers;

public sealed class WmcInitialLoadControllerTests
{
    [Fact]
    public async Task Initial_get_renders_without_invoking_firebird_readers()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var brandReader = new CountingBrandReader
        {
            Error = new InvalidOperationException("Firebird brand read must not run during initial GET.")
        };
        var customerReader = new CountingCustomerReader
        {
            SellerCodesResult = new InvalidOperationException("Firebird seller read must not run during initial GET.")
        };
        var controller = CreateController(db, brandReader, customerReader);

        var result = await controller.Wmc(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WmcPreviewPageModel>(view.Model);
        Assert.Empty(model.Brands);
        Assert.Equal(0, brandReader.ReadCount);
        Assert.Equal(0, customerReader.SellerCodeReadCount);
    }

    [Fact]
    public async Task Initial_get_renders_when_auxiliary_postgres_read_fails()
    {
        var db = await TestDbContextFactory.CreateAsync();
        var controller = CreateController(
            db,
            new CountingBrandReader { Error = new InvalidOperationException("Firebird read must not run.") },
            new CountingCustomerReader());
        await db.DisposeAsync();

        var result = await controller.Wmc(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<WmcPreviewPageModel>(view.Model);
    }

    [Fact]
    public async Task Products_load_reads_brands_only_when_requested()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var brandReader = new CountingBrandReader
        {
            Result = new[] { new WmcBrandRecord(7, "Marca teste", 3, 2) }
        };
        var controller = CreateController(db, brandReader, new CountingCustomerReader());
        SetQuery(controller, ("loadBrands", "true"));

        var result = await controller.Wmc(CancellationToken.None);

        var model = Assert.IsType<WmcPreviewPageModel>(Assert.IsType<ViewResult>(result).Model);
        var brand = Assert.Single(model.Brands);
        Assert.Equal(7, brand.Code);
        Assert.Equal(1, brandReader.ReadCount);
    }

    [Fact]
    public async Task Products_load_returns_renderable_unavailable_state_when_firebird_fails()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var brandReader = new CountingBrandReader
        {
            Error = new InvalidOperationException("Firebird mirror unavailable.")
        };
        var controller = CreateController(db, brandReader, new CountingCustomerReader());
        SetQuery(controller, ("loadBrands", "true"));

        var result = await controller.Wmc(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WmcPreviewPageModel>(view.Model);
        Assert.Empty(model.Brands);
        Assert.Equal("unavailable", controller.ViewBag.WmcBrandQueryState);
        Assert.Equal(1, brandReader.ReadCount);
        Assert.NotNull(controller.TempData["WmcPreviewError"]);
    }

    [Fact]
    public async Task Products_load_returns_retryable_state_when_brand_reader_cancels()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var brandReader = new CountingBrandReader
        {
            Error = new OperationCanceledException("Firebird query exceeded the request-local timeout.")
        };
        var controller = CreateController(db, brandReader, new CountingCustomerReader());
        SetQuery(controller, ("loadBrands", "true"));

        var result = await controller.Wmc(CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal("unavailable", controller.ViewBag.WmcBrandQueryState);
        Assert.NotNull(controller.TempData["WmcPreviewError"]);
    }

    [Fact]
    public async Task Products_load_propagates_request_cancellation()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var brandReader = new CountingBrandReader
        {
            Error = new OperationCanceledException("Request was canceled.")
        };
        var controller = CreateController(db, brandReader, new CountingCustomerReader());
        SetQuery(controller, ("loadBrands", "true"));
        using var requestCancellation = new CancellationTokenSource();
        requestCancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controller.Wmc(requestCancellation.Token));
        Assert.True(brandReader.CapturedToken.IsCancellationRequested);
        Assert.Null(controller.TempData["WmcPreviewError"]);
    }

    private static IntegrationsController CreateController(
        ApplicationDbContext db,
        CountingBrandReader brandReader,
        CountingCustomerReader customerReader)
    {
        var customerService = new WmcCustomerImportService(
            db,
            customerReader,
            NullLogger<WmcCustomerImportService>.Instance,
            TimeProvider.System);
        var products = new EmptyProductReader();
        var controller = new IntegrationsController(
            db,
            null!,
            new WmcSyncCoordinator(),
            null!,
            Options.Create(new WmcSyncOptions()),
            brandReader,
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
        return controller;
    }

    private static void SetQuery(IntegrationsController controller, params (string Key, string Value)[] values)
    {
        controller.HttpContext.Request.Query = new QueryCollection(values.ToDictionary(
            item => item.Key,
            item => new StringValues(item.Value)));
    }

    private sealed class CountingBrandReader : IWmcBrandReader
    {
        public IReadOnlyList<WmcBrandRecord> Result { get; init; } = [];
        public Exception? Error { get; init; }
        public int ReadCount { get; private set; }
        public CancellationToken CapturedToken { get; private set; }

        public Task<IReadOnlyList<WmcBrandRecord>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            ReadCount++;
            CapturedToken = cancellationToken;
            if (Error is not null) throw Error;
            return Task.FromResult(Result);
        }
    }

    private sealed class CountingCustomerReader : IWmcCustomerReader
    {
        public Exception? SellerCodesResult { get; init; }
        public int SellerCodeReadCount { get; private set; }

        public Task<IReadOnlyList<WmcCustomerRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WmcCustomerRecord>>([]);

        public Task<IReadOnlyList<WmcCustomerRecord>> GetBySellerAsync(string sellerCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WmcCustomerRecord>>([]);

        public Task<IReadOnlyList<string>> GetSellerCodesAsync(CancellationToken cancellationToken = default)
        {
            SellerCodeReadCount++;
            if (SellerCodesResult is not null) throw SellerCodesResult;
            return Task.FromResult<IReadOnlyList<string>>([]);
        }
    }

    private sealed class EmptyProductReader : IWmcProductReader
    {
        public Task<IReadOnlyList<WmcProductRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WmcProductRecord>>([]);

        public Task<IReadOnlyList<WmcProductRecord>> GetAllByBrandAsync(short? brandCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WmcProductRecord>>([]);

        public Task<IReadOnlyList<WmcProductRecord>> GetAllByBrandWithUnitsAsync(short? brandCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WmcProductRecord>>([]);
    }
}
