using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
using Orofoods.Web.Services.Integrations;
using Orofoods.Web.Services.Orders;

namespace Orofoods.Web.Tests.Integrations;

public class WmcJsonOrderHomologationBackgroundServiceTests
{
    [Fact]
    public async Task Dispatches_only_the_configured_order_without_a_file_drop_worker_or_pending_order_scan()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var setupDb = new ApplicationDbContext(dbOptions);
        await setupDb.Database.EnsureCreatedAsync();

        var seller = new SalesRepresentative { Id = 27, Name = "Vendedor HML", Email = "seller@example.invalid" };
        var category = new ProductCategory { Name = "Pães", Slug = "paes" };
        var selectedOrder = CreateOrder("ORO-2026-010001", seller, category, "01");
        var unrelatedOrder = CreateOrder("ORO-2026-010002", seller, category, "02");
        setupDb.Orders.AddRange(selectedOrder, unrelatedOrder);
        await setupDb.SaveChangesAsync();

        var handler = new SuccessHandler();
        var httpClient = new HttpClient(handler);
        var options = new WmcJsonOrderOptions
        {
            Enabled = true,
            HomologationWorkerEnabled = true,
            HomologationOrderId = selectedOrder.Id,
            HomologationOrderIdConfirmedUnique = true,
            BaseUrl = "https://wmc.example.invalid/",
            DeviceId = "orofoods-hml-device",
            SalesRepresentativeSerials = new Dictionary<int, string> { [27] = "orofoods-hml-serial" },
            SalesRepresentativeCodes = new Dictionary<int, int> { [27] = 804 }
        };
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<OrderIntegrationService>>(NullLogger<OrderIntegrationService>.Instance);
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(item => item.EnvironmentName).Returns("Test");
        services.AddSingleton<IHostEnvironment>(environment.Object);
        services.AddScoped(_ => new ApplicationDbContext(dbOptions));
        services.AddScoped<WmcExportAuditService>();
        services.AddScoped<OrderIntegrationService>();
        services.AddScoped<IWmcTransmissionIdAllocator>(_ => new FixedWmcTransmissionIdAllocator(9001));
        services.AddSingleton<IErpOrderIntegration, UnusedIntegration>();
        services.AddSingleton(httpClient);
        services.AddSingleton(Options.Create(options));
        services.AddSingleton<WmcMobileOrderPayloadBuilder>();
        services.AddScoped<WmcJsonOrderErpIntegration>();
        await using var provider = services.BuildServiceProvider();
        var worker = new WmcJsonOrderHomologationBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options),
            NullLogger<WmcJsonOrderHomologationBackgroundService>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!;

        Assert.Equal(2, handler.CallCount);
        setupDb.ChangeTracker.Clear();
        Assert.Equal(IntegrationStatus.Succeeded, (await setupDb.Orders.SingleAsync(order => order.Id == selectedOrder.Id)).IntegrationStatus);
        Assert.Equal(IntegrationStatus.Pending, (await setupDb.Orders.SingleAsync(order => order.Id == unrelatedOrder.Id)).IntegrationStatus);
        await worker.StopAsync(CancellationToken.None);
        httpClient.Dispose();
    }

    private static Order CreateOrder(string number, SalesRepresentative seller, ProductCategory category, string suffix)
    {
        return new Order
        {
            Number = number,
            ConfirmedAt = new DateTime(2026, 10, 21, 10, 30, 0, DateTimeKind.Utc),
            Customer = new Customer
            {
                LegalName = "Cliente homologação",
                TradeName = "Cliente HML",
                Cnpj = $"000000000000{suffix}",
                WmcCode = "CLIENTE-HML",
                SalesRepresentativeId = 27,
                SalesRepresentative = seller
            },
            CreatedByUser = Orofoods.Web.Tests.Infrastructure.TestDbContextFactory.CreateOrderCreator(),
            Items =
            [
                new OrderItem
                {
                    Product = new Product
                    {
                        Sku = $"HML-{suffix}",
                        WmcCode = $"PRODUTO-HML-{suffix}",
                        Name = "Produto homologação",
                        Unit = "caixa",
                        ProductCategory = category
                    },
                    ProductNameSnapshot = "Produto homologação",
                    SkuSnapshot = $"HML-{suffix}",
                    Quantity = 1,
                    UnitPrice = 10m,
                    Subtotal = 10m
                }
            ]
        };
    }

    private sealed class SuccessHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("importado com sucesso") });
        }
    }

    private sealed class UnusedIntegration : IErpOrderIntegration
    {
        public Task<ErpOrderResult> SendOrderAsync(Order order, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The homologation worker must select the HTTP integration directly.");
    }

    private sealed class FixedWmcTransmissionIdAllocator(int transmissionId) : IWmcTransmissionIdAllocator
    {
        public Task<int> GetOrAllocateAsync(Order order, CancellationToken cancellationToken = default)
        {
            order.WmcTransmissionId ??= transmissionId;
            return Task.FromResult(order.WmcTransmissionId.Value);
        }
    }
}
