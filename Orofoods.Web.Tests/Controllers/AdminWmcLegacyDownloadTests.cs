using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Integrations;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Controllers;

public sealed class AdminWmcLegacyDownloadTests
{
    [Fact]
    public async Task Existing_export_download_route_uses_the_same_duplicate_protection_and_audit()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = new Order
        {
            Number = "ORO-2026-008881",
            CustomerCnpjSnapshot = "03932292000199",
            Status = OrderStatus.Approved,
            CreatedAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
            Customer = new Customer
            {
                LegalName = "Cliente Ltda",
                TradeName = "Cliente",
                Cnpj = "03.932.292/0001-99",
                WmcCode = "107072"
            },
            CreatedByUser = TestDbContextFactory.CreateOrderCreator("legacy-route-order-creator"),
            PaymentTerm = new PaymentTerm { Code = "PIX", Name = "PIX", DaysUntilDue = 0 },
            Total = 25m,
            Subtotal = 25m,
            Items =
            [
                new OrderItem
                {
                    Product = new Product
                    {
                        Name = "Produto",
                        Sku = "SKU-LEGACY-1",
                        WmcCode = "610601552",
                        Unit = "UN",
                        ProductCategory = new ProductCategory { Name = "Categoria", Slug = "legacy-route-category" }
                    },
                    ProductNameSnapshot = "Produto",
                    SkuSnapshot = "SKU-LEGACY-1",
                    Quantity = 1,
                    UnitPrice = 25m,
                    Subtotal = 25m
                }
            ]
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var first = Assert.IsType<FileContentResult>(await controller.ExportWmc(order.Id, CancellationToken.None));
        var second = Assert.IsType<RedirectToActionResult>(await controller.ExportWmc(order.Id, CancellationToken.None));

        Assert.Equal("WMC_ORO2026008881.txt", first.FileDownloadName);
        Assert.Equal(nameof(IntegrationsController.Index), second.ActionName);
        Assert.Contains("já foi exportado", controller.TempData["WmcError"]?.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Single(await db.WmcExportAudits.Where(audit => audit.Succeeded).ToListAsync());
        Assert.Single(await db.WmcExportAudits.Where(audit => !audit.Succeeded).ToListAsync());
        Assert.Equal(WmcOrderExportStatus.Exported, (await db.WmcOrderExportStates.SingleAsync()).Status);
    }

    private static IntegrationsController CreateController(ApplicationDbContext db)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "legacy-route-admin"),
                new Claim(ClaimTypes.Name, "admin@orofoods.local")
            ], "test"))
        };
        var controller = new IntegrationsController(
            db,
            new WmcManualExportService(db, WmcTestOrderFileGenerator.Create(), new WmcExportAuditService(db)),
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
            TempData = new TempDataDictionary(context, new TestTempDataProvider())
        };
        return controller;
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
