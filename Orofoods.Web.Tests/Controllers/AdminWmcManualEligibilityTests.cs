using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
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
using System.Security.Claims;
using Moq;

namespace Orofoods.Web.Tests.Controllers;

public sealed class AdminWmcManualEligibilityTests
{
    [Fact]
    public async Task Order_details_load_diagnostic_context_without_creating_export_records()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateOrder("ORO-2026-000904", "PIX");
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var controller = new OrdersController(
            db,
            service: null!,
            new WmcManualExportService(db, WmcTestOrderFileGenerator.Create(), new WmcExportAuditService(db)),
            pointPaymentService: null!,
            Options.Create(new MercadoPagoPointOptions()),
            Options.Create(new PaymentEligibilityOptions()),
            new TestHostEnvironment());

        var result = await controller.Details(order.Id);

        Assert.IsType<ViewResult>(result);
        Assert.Contains(typeof(OrdersController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>(), attribute => attribute.Roles == "Administrador");
        var diagnostic = Assert.IsType<WmcManualExportDiagnostic>(controller.ViewData["WmcManualExportDiagnostic"]);
        Assert.True(diagnostic.CanExport);
        Assert.Equal(0, await db.WmcExportAudits.CountAsync());
        Assert.Equal(0, await db.WmcOrderExportStates.CountAsync());
    }

    [Fact]
    public async Task Legacy_reprocess_action_routes_to_the_json_import_queue()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateOrder("ORO-2026-000905", "PIX");
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var controller = new OrdersController(
            db,
            service: null!,
            wmcManualExportService: new WmcManualExportService(db, WmcTestOrderFileGenerator.Create(), new WmcExportAuditService(db)),
            pointPaymentService: null!,
            Options.Create(new MercadoPagoPointOptions()),
            Options.Create(new PaymentEligibilityOptions()),
            hostEnvironment: null!);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "admin-user"),
                    new Claim(ClaimTypes.Name, "admin@example.test")
                ], "test"))
            }
        };
        controller.TempData = new TempDataDictionary(controller.HttpContext, Mock.Of<ITempDataProvider>());

        var result = controller.ReprocessIntegration(order.Id);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("WmcOrderImports", redirect.ControllerName);
        Assert.Empty(await db.WmcOrderExportStates.ToListAsync());
        Assert.Empty(await db.WmcManualExportArtifacts.ToListAsync());
        Assert.Empty(await db.WmcExportAudits.ToListAsync());
    }

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

        var exportService = new WmcManualExportService(db, WmcTestOrderFileGenerator.Create(), new WmcExportAuditService(db));
        var controller = new OrdersController(
            db,
            service: null!,
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

    private static Order CreateOrder(string number, string paymentCode)
    {
        var cnpj = number.EndsWith("902", StringComparison.Ordinal) ? "00.000.000/E08G-12"
            : number.EndsWith("903", StringComparison.Ordinal) ? "12.ABC.345/01DE-35"
            : "03.932.292/0001-99";
        return new Order
        {
        Number = number,
        CustomerCnpjSnapshot = cnpj,
        Status = OrderStatus.Approved,
        CreatedAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
        Customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = cnpj, WmcCode = "107072" },
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

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Orofoods.Web.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
