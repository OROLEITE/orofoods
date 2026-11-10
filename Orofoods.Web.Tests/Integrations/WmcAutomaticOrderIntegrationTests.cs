using System.Net;
using Microsoft.Extensions.Options;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Integrations;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Integrations;

public class WmcAutomaticOrderIntegrationTests
{
    [Fact]
    public async Task Does_not_fall_back_to_automatic_txt_when_json_is_disabled()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var handler = new ThrowingHandler();
        using var client = new HttpClient(handler);
        var json = new WmcJsonOrderErpIntegration(
            client,
            Options.Create(new WmcJsonOrderOptions { Enabled = false }),
            new WmcMobileOrderPayloadBuilder(),
            new WmcExportAuditService(db),
            new FixedWmcTransmissionIdAllocator(9001));
        var integration = new WmcAutomaticOrderIntegration(Options.Create(new WmcJsonOrderOptions()), json);
        var order = new Order
        {
            Number = "ORO-2026-000901",
            Status = OrderStatus.Approved,
            CustomerCnpjSnapshot = "12345678000195",
            PaymentTerm = new PaymentTerm { Code = "CASH", Name = "A vista", DaysUntilDue = 0 },
            Customer = new Customer { WmcCode = "107072", LegalName = "Cliente teste", TradeName = "Cliente teste", Cnpj = "12345678000195" },
            CreatedByUser = TestDbContextFactory.CreateOrderCreator(),
            Items =
            [
                new OrderItem
                {
                    Product = new Product { WmcCode = "610601552", Unit = "caixa", Name = "Produto teste", ProductCategory = new ProductCategory { Name = "Teste", Slug = "teste" } },
                    ProductNameSnapshot = "Produto teste",
                    Quantity = 1,
                    UnitPrice = 10m,
                    Subtotal = 10m
                }
            ]
        };
        db.Add(order);
        await db.SaveChangesAsync();

        var result = await integration.SendOrderAsync(order);

        Assert.False(result.Succeeded);
        Assert.Contains("JSON", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handler.CallCount);
        Assert.Empty(db.WmcExportAudits);
    }

    [Fact]
    public async Task Selects_the_json_transmitter_when_enabled()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = new Order
        {
            Number = "ORO-2026-000902",
            Status = OrderStatus.Approved,
            Customer = new Customer
            {
                LegalName = "Cliente teste",
                TradeName = "Cliente teste",
                Cnpj = "00000000000000",
                WmcCode = "107072",
                SalesRepresentativeId = 27,
                SalesRepresentative = new Orofoods.Web.Models.Customers.SalesRepresentative { Id = 27, Name = "Vendedor teste", Email = "seller@example.invalid" }
            },
            CreatedByUser = TestDbContextFactory.CreateOrderCreator(),
            Items =
            [
                new OrderItem
                {
                    Product = new Product
                    {
                        Sku = "TEST-1",
                        WmcCode = "610601552",
                        Name = "Produto teste",
                        Unit = "caixa",
                        Brand = "Teste",
                        ProductCategory = new ProductCategory { Name = "Teste", Slug = "teste" }
                    },
                    ProductNameSnapshot = "Produto teste",
                    Quantity = 1,
                    UnitPrice = 10m,
                    Subtotal = 10m
                }
            ]
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var handler = new SuccessHandler();
        using var client = new HttpClient(handler);
        var jsonOptions = Options.Create(new WmcJsonOrderOptions
        {
            Enabled = true,
            AutomaticWorkerEnabled = true,
            AutomaticWorkerStartAfterOrderId = 0,
            HomologationOrderIdConfirmedUnique = true,
            HomologationOrderId = order.Id,
            BaseUrl = "https://wmc.example.invalid/",
            DeviceId = "device-test",
            SalesRepresentativeSerials = new Dictionary<int, string> { [27] = "serial-test" },
            SalesRepresentativeCodes = new Dictionary<int, int> { [27] = 804 }
        });
        var json = new WmcJsonOrderErpIntegration(
            client,
            jsonOptions,
            new WmcMobileOrderPayloadBuilder(),
            new WmcExportAuditService(db),
            new FixedWmcTransmissionIdAllocator(9001));
        var integration = new WmcAutomaticOrderIntegration(jsonOptions, json);

        var result = await integration.SendOrderAsync(order);

        Assert.True(result.Succeeded);
        Assert.Equal(2, handler.CallCount);
        Assert.Empty(db.WmcExportAudits.Where(audit => audit.Source == Orofoods.Web.Models.Integrations.WmcExportSource.Manual));
    }

    [Fact]
    public async Task Does_not_transmit_json_when_the_automatic_worker_is_disabled()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seller = new Orofoods.Web.Models.Customers.SalesRepresentative
        {
            Id = 27,
            Name = "Vendedor teste",
            Email = "seller@example.invalid"
        };
        var order = new Order
        {
            Number = "ORO-2026-000903",
            Status = OrderStatus.Approved,
            Customer = new Customer
            {
                LegalName = "Cliente teste",
                TradeName = "Cliente teste",
                Cnpj = "00000000000000",
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
                        Sku = "TEST-3",
                        WmcCode = "610601553",
                        Name = "Produto teste",
                        Unit = "caixa",
                        Brand = "Teste",
                        ProductCategory = new ProductCategory { Name = "Teste", Slug = "teste" }
                    },
                    ProductNameSnapshot = "Produto teste",
                    Quantity = 1,
                    UnitPrice = 10m,
                    Subtotal = 10m
                }
            ]
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var handler = new SuccessHandler();
        using var client = new HttpClient(handler);
        var options = Options.Create(new WmcJsonOrderOptions
        {
            Enabled = true,
            AutomaticWorkerEnabled = false,
            HomologationOrderId = order.Id,
            HomologationOrderIdConfirmedUnique = true,
            BaseUrl = "https://wmc.example.invalid/",
            DeviceId = "device-test",
            SalesRepresentativeCodes = new Dictionary<int, int> { [seller.Id] = 804 },
            SalesRepresentativeSerials = new Dictionary<int, string> { [seller.Id] = "serial-test" }
        });
        var json = new WmcJsonOrderErpIntegration(
            client,
            options,
            new WmcMobileOrderPayloadBuilder(),
            new WmcExportAuditService(db),
            new FixedWmcTransmissionIdAllocator(9001));
        var integration = new WmcAutomaticOrderIntegration(options, json);

        var result = await integration.SendOrderAsync(order);

        Assert.False(result.Succeeded);
        Assert.Contains("automatic", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handler.CallCount);
        Assert.Empty(db.WmcExportAudits);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            throw new HttpRequestException("HTTP real não deve ser chamado neste teste.");
        }
    }

    private sealed class SuccessHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var body = request.RequestUri?.AbsolutePath.EndsWith("nropedido.php", StringComparison.Ordinal) == true
                ? "[{\"NROPEDIDO\":776655}]"
                : "importado com sucesso";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
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
