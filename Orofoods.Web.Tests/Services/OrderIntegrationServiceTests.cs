using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Moq;
using Orofoods.Web.Integrations.Erp;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class OrderIntegrationServiceTests
{
    [Fact]
    public async Task Loads_customer_and_products_before_sending_the_order_to_the_erp()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Paes", Slug = "paes", IsActive = true };
        var product = new Product { Sku = "BIM-001", WmcCode = "610601552", Name = "Pao", ProductCategory = category, Brand = "BIMBO", Unit = "caixa", BasePrice = 50m };
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99", WmcCode = "107072" };
        var user = new ApplicationUser { Id = "user-1", UserName = "user@orofoods.local", NormalizedUserName = "USER@OROFOODS.LOCAL", Email = "user@orofoods.local", NormalizedEmail = "USER@OROFOODS.LOCAL" };
        var order = new Order { Number = "ORO-2026-000777", Customer = customer, CreatedByUser = user, Total = 100m, ConfirmedAt = DateTime.UtcNow };
        order.Items.Add(new OrderItem { Product = product, ProductNameSnapshot = "Pao", SkuSnapshot = "BIM-001", Quantity = 2, UnitPrice = 50m, Subtotal = 100m });
        db.Add(order);
        await db.SaveChangesAsync();
        var erp = new CapturingErpOrderIntegration();

        await new OrderIntegrationService(db, erp, CreateHostEnvironment()).SendAsync(order.Id);

        Assert.True(erp.ReceivedMappedOrder);
        var persisted = await db.Orders.SingleAsync(item => item.Id == order.Id);
        Assert.Equal("WMC_ORO2026000777.txt", persisted.ExternalOrderId);
    }

    [Fact]
    public async Task Marks_the_order_as_failed_when_the_erp_adapter_throws()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        var user = new ApplicationUser { Id = "user-2", UserName = "user2@orofoods.local", NormalizedUserName = "USER2@OROFOODS.LOCAL", Email = "user2@orofoods.local", NormalizedEmail = "USER2@OROFOODS.LOCAL" };
        var order = new Order { Number = "ORO-2026-000778", Customer = customer, CreatedByUser = user, ConfirmedAt = DateTime.UtcNow };
        db.Add(order);
        await db.SaveChangesAsync();

        await new OrderIntegrationService(db, new ThrowingErpOrderIntegration(), CreateHostEnvironment()).SendAsync(order.Id);

        var persisted = await db.Orders.SingleAsync(item => item.Id == order.Id);
        Assert.Equal(IntegrationStatus.Failed, persisted.IntegrationStatus);
        Assert.Equal("Falha inesperada ao enviar o pedido ao ERP.", persisted.IntegrationError);
    }

    [Fact]
    public async Task Does_not_resend_an_order_with_an_uncertain_delivery_result()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = new Order
        {
            Number = "ORO-2026-000779",
            Customer = new Customer { LegalName = "Cliente", TradeName = "Cliente", Cnpj = "00000000000000" },
            CreatedByUser = TestDbContextFactory.CreateOrderCreator(),
            IntegrationStatus = IntegrationStatus.Pending,
            ConfirmedAt = DateTime.UtcNow
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var erp = new CountingErpOrderIntegration();
        var service = new OrderIntegrationService(db, erp, CreateHostEnvironment());

        await service.SendAsync(order.Id);
        Assert.Equal(IntegrationStatus.Unknown, (await db.Orders.SingleAsync(item => item.Id == order.Id)).IntegrationStatus);
        await service.SendAsync(order.Id);

        Assert.Equal(1, erp.CallCount);
        Assert.Equal(IntegrationStatus.Unknown, (await db.Orders.SingleAsync(item => item.Id == order.Id)).IntegrationStatus);
    }

    [Fact]
    public async Task Persists_the_wmc_transmission_id_and_returned_order_number_and_does_not_resend_success()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = new Order
        {
            Number = "ORO-2026-000781",
            Customer = new Customer { LegalName = "Cliente", TradeName = "Cliente", Cnpj = "00000000000000" },
            CreatedByUser = TestDbContextFactory.CreateOrderCreator(),
            ConfirmedAt = DateTime.UtcNow
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var erp = new ConfirmedWmcOrderIntegration();
        var service = new OrderIntegrationService(db, erp, CreateHostEnvironment());

        await service.SendAsync(order.Id);
        await service.SendAsync(order.Id);

        var persisted = await db.Orders.SingleAsync(item => item.Id == order.Id);
        Assert.Equal(1, erp.CallCount);
        Assert.Equal(IntegrationStatus.Succeeded, persisted.IntegrationStatus);
        Assert.Equal(9001, persisted.WmcTransmissionId);
        Assert.Equal("9001", persisted.ExternalOrderId);
        Assert.Equal("776655", persisted.ErpOrderNumber);
    }

    [Fact]
    public async Task Uses_the_explicit_file_drop_adapter_without_routing_through_the_json_selector()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = new Order
        {
            Number = "ORO-2026-000780",
            Customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" },
            CreatedByUser = TestDbContextFactory.CreateOrderCreator(),
            ConfirmedAt = DateTime.UtcNow
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var jsonSelector = new CountingErpOrderIntegration();
        var fileDrop = new CapturingErpOrderIntegration();

        await new OrderIntegrationService(db, jsonSelector, CreateHostEnvironment()).SendAsync(order.Id, fileDrop);

        Assert.Equal(0, jsonSelector.CallCount);
        Assert.Equal(1, fileDrop.CallCount);
        Assert.Equal(IntegrationStatus.Succeeded, (await db.Orders.SingleAsync(item => item.Id == order.Id)).IntegrationStatus);
    }

    [Fact]
    public async Task Does_not_start_erp_transmission_in_production()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = new Order
        {
            Number = "ORO-2026-000782",
            Customer = new Customer { LegalName = "Cliente", TradeName = "Cliente", Cnpj = "00000000000000" },
            CreatedByUser = TestDbContextFactory.CreateOrderCreator()
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var erp = new CountingErpOrderIntegration();

        await new OrderIntegrationService(db, erp, CreateHostEnvironment("Production")).SendAsync(order.Id);

        Assert.Equal(0, erp.CallCount);
        Assert.Equal(IntegrationStatus.Pending, (await db.Orders.SingleAsync(item => item.Id == order.Id)).IntegrationStatus);
        Assert.Empty(await db.WmcExportAudits.ToListAsync());
    }

    [Fact]
    public async Task Does_not_transmit_unconfirmed_or_cancelled_orders()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var creator = TestDbContextFactory.CreateOrderCreator();
        var unconfirmed = new Order
        {
            Number = "ORO-2026-000783",
            Customer = new Customer { LegalName = "Cliente", TradeName = "Cliente", Cnpj = "00000000000001" },
            CreatedByUser = creator
        };
        var cancelled = new Order
        {
            Number = "ORO-2026-000784",
            Customer = new Customer { LegalName = "Cliente", TradeName = "Cliente", Cnpj = "00000000000002" },
            CreatedByUser = creator,
            ConfirmedAt = DateTime.UtcNow,
            Status = OrderStatus.Cancelled
        };
        db.Orders.AddRange(unconfirmed, cancelled);
        await db.SaveChangesAsync();
        var erp = new CountingErpOrderIntegration();
        var service = new OrderIntegrationService(db, erp, CreateHostEnvironment());

        await service.SendAsync(unconfirmed.Id);
        await service.SendAsync(cancelled.Id);

        Assert.Equal(0, erp.CallCount);
        Assert.All(await db.Orders.ToListAsync(), order => Assert.Equal(IntegrationStatus.Pending, order.IntegrationStatus));
        Assert.Empty(await db.WmcExportAudits.ToListAsync());
    }

    private sealed class CapturingErpOrderIntegration : IErpOrderIntegration
    {
        public bool ReceivedMappedOrder { get; private set; }
        public int CallCount { get; private set; }

        public Task<ErpOrderResult> SendOrderAsync(Order order, CancellationToken cancellationToken = default)
        {
            CallCount++;
            ReceivedMappedOrder = order.Customer?.WmcCode == "107072" && order.Items.Single().Product?.WmcCode == "610601552";
            return Task.FromResult(new ErpOrderResult(true, "WMC_ORO2026000777.txt"));
        }
    }

    private sealed class ThrowingErpOrderIntegration : IErpOrderIntegration
    {
        public Task<ErpOrderResult> SendOrderAsync(Order order, CancellationToken cancellationToken = default) =>
            throw new IOException("Diretorio indisponivel.");
    }

    private sealed class CountingErpOrderIntegration : IErpOrderIntegration
    {
        public int CallCount { get; private set; }

        public Task<ErpOrderResult> SendOrderAsync(Order order, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new ErpOrderResult(false, Error: "Resultado incerto.", OutcomeUnknown: true));
        }
    }

    private sealed class ConfirmedWmcOrderIntegration : IErpOrderIntegration
    {
        public int CallCount { get; private set; }

        public Task<ErpOrderResult> SendOrderAsync(Order order, CancellationToken cancellationToken = default)
        {
            CallCount++;
            order.WmcTransmissionId ??= 9001;
            return Task.FromResult(new ErpOrderResult(true, "9001", "776655"));
        }
    }

    private static IHostEnvironment CreateHostEnvironment(string environmentName = "Test")
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(item => item.EnvironmentName).Returns(environmentName);
        return environment.Object;
    }

}
