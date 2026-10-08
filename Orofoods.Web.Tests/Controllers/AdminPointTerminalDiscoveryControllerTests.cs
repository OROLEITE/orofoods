using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Data;
using Orofoods.Web.Services.Payments;
using Orofoods.Web.Tests.Infrastructure;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Tests.Controllers;

public sealed class AdminPointTerminalDiscoveryControllerTests
{
    [Fact]
    public void Payment_terminals_controller_keeps_administrator_only_authorization_and_get_discovery_action()
    {
        var controllerType = typeof(PaymentTerminalsController);
        var authorize = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        var action = controllerType.GetMethod("DiscoverMercadoPagoTerminals");

        Assert.Equal("Administrador", authorize?.Roles);
        Assert.NotNull(action);
        Assert.NotNull(action!.GetCustomAttribute<HttpGetAttribute>());
    }

    [Fact]
    public async Task Mode_update_is_a_separate_admin_post_and_does_not_persist_terminal_data()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var discovery = new FakeDiscovery();
        var controller = CreateController(db, discovery, "Staging");
        var action = typeof(PaymentTerminalsController).GetMethod("SetMercadoPagoTerminalOperatingModeToPdv");

        var result = Assert.IsType<RedirectToActionResult>(await controller.SetMercadoPagoTerminalOperatingModeToPdv());

        Assert.NotNull(action?.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(action?.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.Equal("NEWLAND_N950__N950NCD600484709", discovery.TerminalId);
        Assert.Equal("PDV", discovery.OperatingMode);
        Assert.Equal(1, discovery.ModeChangeCalls);
        Assert.Equal(0, await db.PaymentTerminals.CountAsync());
        Assert.Equal(nameof(PaymentTerminalsController.Index), result.ActionName);
        Assert.Equal("Terminal configurado em modo PDV com sucesso.", controller.TempData["MercadoPagoPointTerminalMessage"]);
    }

    [Fact]
    public async Task Staging_index_discovers_terminals_without_persisting_them()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var discovery = new FakeDiscovery([
            new MercadoPagoPointTerminal("NEWLAND_N950__N950NCD600484709", "88334226", "139110122", null, "STANDALONE")
        ]);
        var controller = CreateController(db, discovery, "Staging");

        var result = Assert.IsType<ViewResult>(await controller.Index());
        var model = Assert.IsType<PaymentTerminalIndexViewModel>(result.Model);

        Assert.True(model.IsStaging);
        Assert.Single(model.MercadoPagoTerminals);
        Assert.Equal("STANDALONE", model.MercadoPagoTerminals[0].OperatingMode);
        Assert.Equal(1, discovery.Calls);
        Assert.Equal(0, await db.PaymentTerminals.CountAsync());
    }

    [Fact]
    public async Task Non_staging_index_does_not_discover_terminals()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var discovery = new FakeDiscovery();
        var controller = CreateController(db, discovery, "Production");

        var result = Assert.IsType<ViewResult>(await controller.Index());
        var model = Assert.IsType<PaymentTerminalIndexViewModel>(result.Model);

        Assert.False(model.IsStaging);
        Assert.Empty(model.MercadoPagoTerminals);
        Assert.Equal(0, discovery.Calls);
    }

    [Fact]
    public async Task Mode_update_failure_redirects_with_safe_message_and_does_not_retry()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var discovery = new FakeDiscovery { ModeChangeException = new PaymentGatewayException(System.Net.HttpStatusCode.Forbidden) };
        var controller = CreateController(db, discovery, "Staging");

        var result = Assert.IsType<RedirectToActionResult>(await controller.SetMercadoPagoTerminalOperatingModeToPdv());

        Assert.Equal(nameof(PaymentTerminalsController.Index), result.ActionName);
        Assert.Equal(1, discovery.ModeChangeCalls);
        Assert.Equal("Não foi possível confirmar a configuração do terminal. Atualize a descoberta para conferir o estado atual antes de tentar novamente.",
            controller.TempData["MercadoPagoPointTerminalMessage"]);
        Assert.Equal(0, await db.PaymentTerminals.CountAsync());
    }

    [Fact]
    public void One_real_test_start_is_administrator_post_with_antiforgery_and_status_is_read_only_get()
    {
        var controllerType = typeof(PaymentTerminalsController);
        var authorize = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        var start = controllerType.GetMethod("StartStagingOneRealPointTest");
        var status = controllerType.GetMethod("RefreshStagingOneRealPointTestStatus");

        Assert.Equal("Administrador", authorize?.Roles);
        Assert.NotNull(start?.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(start?.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.Single(start!.GetParameters());
        Assert.Equal(typeof(CancellationToken), start.GetParameters()[0].ParameterType);
        Assert.NotNull(status?.GetCustomAttribute<HttpGetAttribute>());
        Assert.Null(status?.GetCustomAttribute<HttpPostAttribute>());
    }

    [Fact]
    public async Task One_real_test_post_is_only_available_when_staging_flag_is_enabled_and_does_not_persist()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var discovery = new FakeDiscovery();
        var client = new FakeOneRealTestClient();
        var disabledController = CreateController(db, discovery, "Staging", client, enabled: false);

        Assert.IsType<NotFoundResult>(await disabledController.StartStagingOneRealPointTest());
        Assert.Equal(0, client.StartCalls);

        var controller = CreateController(db, discovery, "Staging", client, enabled: true);
        var result = Assert.IsType<RedirectToActionResult>(await controller.StartStagingOneRealPointTest());

        Assert.Equal(nameof(PaymentTerminalsController.Index), result.ActionName);
        Assert.Equal(1, client.StartCalls);
        Assert.Equal(0, await db.Orders.CountAsync());
        Assert.Equal(0, await db.Payments.CountAsync());
        Assert.Equal("ORDER-TEST-1", controller.TempData["StagingOneRealTestOrderId"]);
        Assert.Equal("created", controller.TempData["StagingOneRealTestStatus"]);
        Assert.Equal("Order enviada ao terminal; confira a maquininha.", controller.TempData["StagingOneRealTestMessage"]);
    }

    [Fact]
    public async Task One_real_test_post_is_unavailable_outside_staging_without_client_call()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var client = new FakeOneRealTestClient();
        var controller = CreateController(db, new FakeDiscovery(), "Production", client, enabled: true);

        Assert.IsType<NotFoundResult>(await controller.StartStagingOneRealPointTest());
        Assert.Equal(0, client.StartCalls);
    }

    [Fact]
    public async Task One_real_test_status_get_is_read_only_and_uses_the_supplied_order_id()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var client = new FakeOneRealTestClient();
        var controller = CreateController(db, new FakeDiscovery(), "Staging", client, enabled: true);

        var result = Assert.IsType<RedirectToActionResult>(await controller.RefreshStagingOneRealPointTestStatus("ORDER-TEST-1"));

        Assert.Equal(nameof(PaymentTerminalsController.Index), result.ActionName);
        Assert.Equal("ORDER-TEST-1", client.LastOrderId);
        Assert.Equal(1, client.StatusCalls);
        Assert.Equal(0, client.StartCalls);
        Assert.Equal(0, await db.Orders.CountAsync());
        Assert.Equal(0, await db.Payments.CountAsync());
    }

    [Fact]
    public async Task Mode_update_action_is_unavailable_outside_staging_without_calling_provider()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var discovery = new FakeDiscovery();
        var controller = CreateController(db, discovery, "Production");

        var result = await controller.SetMercadoPagoTerminalOperatingModeToPdv();

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(0, discovery.ModeChangeCalls);
    }

    [Fact]
    public async Task Discovery_action_is_hidden_outside_staging_without_calling_provider()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var discovery = new FakeDiscovery();
        var controller = CreateController(db, discovery, "Test");

        var result = await controller.DiscoverMercadoPagoTerminals();

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(0, discovery.Calls);
    }

    [Fact]
    public async Task Staging_discovery_returns_only_the_terminal_allowlist()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var discovery = new FakeDiscovery([
            new MercadoPagoPointTerminal("NEWLAND_N950__SERIAL-01", "store-1", "21", "point-of-sale-1", "PDV")
        ]);
        var controller = CreateController(db, discovery, "Staging");

        var result = Assert.IsType<OkObjectResult>(await controller.DiscoverMercadoPagoTerminals());
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        var terminal = Assert.Single(json.RootElement.EnumerateArray().ToArray());

        Assert.Equal(new[] { "id", "store_id", "pos_id", "external_pos_id", "operating_mode" },
            terminal.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal("NEWLAND_N950__SERIAL-01", terminal.GetProperty("id").GetString());
        Assert.DoesNotContain("accessToken", json.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, discovery.Calls);
    }

    private static PaymentTerminalsController CreateController(
        ApplicationDbContext db,
        FakeDiscovery discovery,
        string environment,
        FakeOneRealTestClient? testClient = null,
        bool enabled = false)
    {
        var controller = new PaymentTerminalsController(db, new DriverPaymentTerminalService(db, TimeProvider.System), discovery,
            testClient ?? new FakeOneRealTestClient(), Options.Create(new PointStagingOneRealTestOptions { Enabled = enabled }),
            new TestHostEnvironment(environment), NullLogger<PaymentTerminalsController>.Instance);
        controller.TempData = new TempDataDictionary(new Microsoft.AspNetCore.Http.DefaultHttpContext(), new TestTempDataProvider());
        return controller;
    }

    private sealed class FakeOneRealTestClient : IPointStagingOneRealTestClient
    {
        public int StartCalls { get; private set; }
        public int StatusCalls { get; private set; }
        public string? LastOrderId { get; private set; }
        public bool AttemptStarted => StartCalls > 0;

        public Task<PointStagingOneRealTestResult> StartAsync(CancellationToken cancellationToken = default)
        {
            StartCalls++;
            return Task.FromResult(new PointStagingOneRealTestResult("ORDER-TEST-1", "created", "Order enviada ao terminal; confira a maquininha.", true));
        }

        public Task<PointStagingOneRealTestResult> GetStatusAsync(string orderId, CancellationToken cancellationToken = default)
        {
            StatusCalls++;
            LastOrderId = orderId;
            return Task.FromResult(new PointStagingOneRealTestResult(orderId, "at_terminal", "Status consultado no Mercado Pago.", true));
        }
    }

    private sealed class FakeDiscovery(IReadOnlyList<MercadoPagoPointTerminal>? terminals = null) : IMercadoPagoPointTerminalDiscovery
    {
        public int Calls { get; private set; }
        public int ModeChangeCalls { get; private set; }
        public string? TerminalId { get; private set; }
        public string? OperatingMode { get; private set; }
        public Exception? ModeChangeException { get; init; }

        public Task<IReadOnlyList<MercadoPagoPointTerminal>> ListTerminalsAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(terminals ?? (IReadOnlyList<MercadoPagoPointTerminal>)[]);
        }

        public Task<MercadoPagoPointTerminalModeChangeResult> SetTerminalOperatingModeAsync(
            string terminalId,
            string operatingMode,
            CancellationToken cancellationToken = default)
        {
            ModeChangeCalls++;
            TerminalId = terminalId;
            OperatingMode = operatingMode;
            if (ModeChangeException is not null) throw ModeChangeException;
            return Task.FromResult(new MercadoPagoPointTerminalModeChangeResult(terminalId, "STANDALONE", operatingMode));
        }
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Orofoods.Web.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(Microsoft.AspNetCore.Http.HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(Microsoft.AspNetCore.Http.HttpContext context, IDictionary<string, object> values) { }
    }
}
