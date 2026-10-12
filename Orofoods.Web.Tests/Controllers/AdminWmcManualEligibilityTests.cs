using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Integrations;
using Orofoods.Web.Services.Payments;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Controllers;

public sealed class AdminWmcManualEligibilityTests
{
    [Fact]
    public async Task Order_monitor_marks_only_server_eligible_orders_as_manually_exportable()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var eligible = CreateOrder("ORO-2026-000901", "PIX");
        var unsupportedPayment = CreateOrder("ORO-2026-000902", "UNSUPPORTED");
        var processing = CreateOrder("ORO-2026-000903", "PIX");
        processing.PaymentTerm = eligible.PaymentTerm;
        processing.WmcOrderExportState = new WmcOrderExportState
        {
            Status = WmcOrderExportStatus.Processing,
            ProcessingStartedAt = DateTime.UtcNow
        };
        db.Orders.AddRange(eligible, unsupportedPayment, processing);
        await db.SaveChangesAsync();

        var exportService = new WmcManualExportService(db, new WmcOrderFileGenerator(), new WmcExportAuditService(db));
        var controller = new OrdersController(
            db,
            service: null!,
            integrationService: null!,
            wmcManualExportService: exportService,
            pointPaymentService: null!,
            Options.Create(new MercadoPagoPointOptions()),
            Options.Create(new PaymentEligibilityOptions()),
            hostEnvironment: null!);

        var result = await controller.Index(null, null);
        var eligibleIds = Assert.IsAssignableFrom<IReadOnlySet<int>>(controller.ViewData["WmcManualExportEligibleOrderIds"]);

        Assert.IsType<ViewResult>(result);
        Assert.Contains(eligible.Id, eligibleIds);
        Assert.DoesNotContain(unsupportedPayment.Id, eligibleIds);
        Assert.DoesNotContain(processing.Id, eligibleIds);
    }

    private static Order CreateOrder(string number, string paymentCode) => new()
    {
        Number = number,
        Status = OrderStatus.Approved,
        CreatedAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
        Customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = $"12.345.678/0001-{number[^3..]}", WmcCode = "107072" },
        CreatedByUser = TestDbContextFactory.CreateOrderCreator($"creator-{number}"),
        PaymentTerm = new PaymentTerm { Code = paymentCode, Name = paymentCode, DaysUntilDue = 0 },
        Total = 63.20m,
        Subtotal = 63.20m,
        Items =
        [
            new OrderItem
            {
                Product = new Product
                {
                    Name = "Produto",
                    Sku = number,
                    WmcCode = "610601552",
                    Unit = "UN",
                    ProductCategory = new ProductCategory { Name = "Categoria", Slug = $"category-{number}" }
                },
                ProductNameSnapshot = "Produto",
                SkuSnapshot = number,
                Quantity = 1,
                UnitPrice = 63.20m,
                Subtotal = 63.20m
            }
        ]
    };
}
