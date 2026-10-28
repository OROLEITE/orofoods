using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Integrations;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Integrations;

public sealed class WmcJsonOrderAutomaticBackgroundServiceTests
{
    [Fact]
    public async Task Worker_is_disabled_by_default_and_does_not_create_a_scope()
    {
        Assert.False(new WmcJsonOrderOptions().AutomaticWorkerEnabled);
        var services = new ServiceCollection().BuildServiceProvider();
        var worker = new WmcJsonOrderAutomaticBackgroundService(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new WmcJsonOrderOptions { Enabled = true }),
            NullLogger<WmcJsonOrderAutomaticBackgroundService>.Instance);

        await worker.ProcessPendingOrdersAsync();
    }

    [Fact]
    public async Task Worker_does_not_open_a_scope_without_an_activation_marker()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var worker = new WmcJsonOrderAutomaticBackgroundService(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new WmcJsonOrderOptions { Enabled = true, AutomaticWorkerEnabled = true }),
            NullLogger<WmcJsonOrderAutomaticBackgroundService>.Instance);

        await worker.ProcessPendingOrdersAsync();
    }

    [Theory]
    [InlineData(-1)]
    public async Task Worker_does_not_open_a_scope_for_a_negative_activation_marker(int marker)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var worker = new WmcJsonOrderAutomaticBackgroundService(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new WmcJsonOrderOptions
            {
                Enabled = true,
                AutomaticWorkerEnabled = true,
                AutomaticWorkerStartAfterOrderId = marker
            }),
            NullLogger<WmcJsonOrderAutomaticBackgroundService>.Instance);

        await worker.ProcessPendingOrdersAsync();
    }

    [Fact]
    public async Task Worker_processes_only_approved_pending_orders_without_resolving_filedrop()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var setupDb = new ApplicationDbContext(dbOptions);
        await setupDb.Database.EnsureCreatedAsync();

        var seller = new SalesRepresentative { Id = 27, Name = "Vendedor teste", Email = "seller@example.invalid" };
        var category = new ProductCategory { Name = "Teste", Slug = "teste" };
        var historicalPending = CreateOrder("000", OrderStatus.Approved, IntegrationStatus.Pending, seller, category);
        var approvedPending = CreateOrder("001", OrderStatus.Approved, IntegrationStatus.Pending, seller, category);
        var approvedFailed = CreateOrder("002", OrderStatus.Approved, IntegrationStatus.Failed, seller, category);
        var unapprovedPending = CreateOrder("003", OrderStatus.Draft, IntegrationStatus.Pending, seller, category);
        setupDb.Orders.AddRange(historicalPending, approvedPending, approvedFailed, unapprovedPending);
        await setupDb.SaveChangesAsync();

        var handler = new SuccessHandler();
        using var httpClient = new HttpClient(handler);
        var options = CreateEnabledOptions();
        options.AutomaticWorkerStartAfterOrderId = historicalPending.Id;
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(item => item.EnvironmentName).Returns("Test");
        var services = new ServiceCollection();
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<OrderIntegrationService>>(NullLogger<OrderIntegrationService>.Instance);
        services.AddSingleton<IHostEnvironment>(environment.Object);
        services.AddScoped(_ => new ApplicationDbContext(dbOptions));
        services.AddScoped<WmcExportAuditService>();
        services.AddScoped<OrderIntegrationService>();
        services.AddScoped<IWmcTransmissionIdAllocator>(_ => new FixedTransmissionIdAllocator());
        services.AddSingleton(httpClient);
        services.AddSingleton(Options.Create(options));
        services.AddSingleton<WmcMobileOrderPayloadBuilder>();
        services.AddScoped<WmcJsonOrderErpIntegration>();
        services.AddScoped<WmcAutomaticOrderIntegration>();
        services.AddScoped<IErpOrderIntegration>(provider => provider.GetRequiredService<WmcAutomaticOrderIntegration>());
        await using var provider = services.BuildServiceProvider();
        var worker = new WmcJsonOrderAutomaticBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options),
            NullLogger<WmcJsonOrderAutomaticBackgroundService>.Instance);

        await worker.ProcessPendingOrdersAsync();

        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith("/exportar.php", handler.Requests[0].AbsolutePath, StringComparison.Ordinal);
        Assert.EndsWith("/nropedido.php", handler.Requests[1].AbsolutePath, StringComparison.Ordinal);
        setupDb.ChangeTracker.Clear();
        Assert.Equal(IntegrationStatus.Pending, (await setupDb.Orders.SingleAsync(order => order.Id == historicalPending.Id)).IntegrationStatus);
        Assert.Equal(IntegrationStatus.Succeeded, (await setupDb.Orders.SingleAsync(order => order.Id == approvedPending.Id)).IntegrationStatus);
        Assert.Equal(IntegrationStatus.Failed, (await setupDb.Orders.SingleAsync(order => order.Id == approvedFailed.Id)).IntegrationStatus);
        Assert.Equal(IntegrationStatus.Pending, (await setupDb.Orders.SingleAsync(order => order.Id == unapprovedPending.Id)).IntegrationStatus);
    }

    private static WmcJsonOrderOptions CreateEnabledOptions() => new()
    {
        Enabled = true,
        AutomaticWorkerEnabled = true,
        AutomaticWorkerStartAfterOrderId = 0,
        BaseUrl = "https://wmc.example.invalid/",
        DeviceId = "device-test",
        SalesRepresentativeCodes = new Dictionary<int, int> { [27] = 804 },
        SalesRepresentativeSerials = new Dictionary<int, string> { [27] = "serial-test" }
    };

    private static Order CreateOrder(
        string suffix,
        OrderStatus status,
        IntegrationStatus integrationStatus,
        SalesRepresentative seller,
        ProductCategory category) => new()
    {
        Number = $"ORO-2026-020{suffix}",
        Status = status,
        IntegrationStatus = integrationStatus,
        Customer = new Customer
        {
            LegalName = "Cliente teste",
            TradeName = "Cliente teste",
            Cnpj = $"000000000000{suffix}",
            WmcCode = "107072",
            SalesRepresentativeId = seller.Id,
            SalesRepresentative = seller
        },
        CreatedByUser = TestDbContextFactory.CreateOrderCreator(),
        PaymentTerm = new PaymentTerm { Code = "CASH", Name = "A vista", DaysUntilDue = 0 },
        Items =
        [
            new OrderItem
            {
                Product = new Product
                {
                    Sku = $"TEST-{suffix}",
                    WmcCode = $"61060155{suffix}",
                    Name = "Produto teste",
                    Unit = "caixa",
                    Brand = "Teste",
                    ProductCategory = category
                },
                ProductNameSnapshot = "Produto teste",
                Quantity = 1,
                UnitPrice = 10m,
                Subtotal = 10m
            }
        ]
    };

    private sealed class SuccessHandler : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            var body = request.RequestUri!.AbsolutePath.EndsWith("/exportar.php", StringComparison.Ordinal)
                ? "importado com sucesso"
                : "[{\"NROPEDIDO\":776655}]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    private sealed class FixedTransmissionIdAllocator : IWmcTransmissionIdAllocator
    {
        public Task<int> GetOrAllocateAsync(Order order, CancellationToken cancellationToken = default)
        {
            order.WmcTransmissionId = 9001;
            return Task.FromResult(9001);
        }
    }
}
