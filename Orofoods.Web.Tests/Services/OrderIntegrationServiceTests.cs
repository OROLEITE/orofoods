using Microsoft.EntityFrameworkCore;
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
        var order = new Order { Number = "ORO-2026-000777", Customer = customer, CreatedByUser = user, Total = 100m };
        order.Items.Add(new OrderItem { Product = product, ProductNameSnapshot = "Pao", SkuSnapshot = "BIM-001", Quantity = 2, UnitPrice = 50m, Subtotal = 100m });
        db.Add(order);
        await db.SaveChangesAsync();
        var erp = new CapturingErpOrderIntegration();

        await new OrderIntegrationService(db, erp).SendAsync(order.Id);

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
        var order = new Order { Number = "ORO-2026-000778", Customer = customer, CreatedByUser = user };
        db.Add(order);
        await db.SaveChangesAsync();

        await new OrderIntegrationService(db, new ThrowingErpOrderIntegration()).SendAsync(order.Id);

        var persisted = await db.Orders.SingleAsync(item => item.Id == order.Id);
        Assert.Equal(IntegrationStatus.Failed, persisted.IntegrationStatus);
        Assert.Equal("Falha inesperada ao enviar o pedido ao ERP.", persisted.IntegrationError);
    }

    private sealed class CapturingErpOrderIntegration : IErpOrderIntegration
    {
        public bool ReceivedMappedOrder { get; private set; }

        public Task<ErpOrderResult> SendOrderAsync(Order order, CancellationToken cancellationToken = default)
        {
            ReceivedMappedOrder = order.Customer?.WmcCode == "107072" && order.Items.Single().Product?.WmcCode == "610601552";
            return Task.FromResult(new ErpOrderResult(true, "WMC_ORO2026000777.txt"));
        }
    }

    private sealed class ThrowingErpOrderIntegration : IErpOrderIntegration
    {
        public Task<ErpOrderResult> SendOrderAsync(Order order, CancellationToken cancellationToken = default) =>
            throw new IOException("Diretorio indisponivel.");
    }
}
