using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Integrations;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Controllers;

public class AdminWmcMonitorControllerTests
{
    [Theory]
    [InlineData(OrderStatus.Draft)]
    [InlineData(OrderStatus.Received)]
    [InlineData(OrderStatus.UnderReview)]
    [InlineData(OrderStatus.Picking)]
    [InlineData(OrderStatus.Invoiced)]
    [InlineData(OrderStatus.OutForDelivery)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task Manual_wmc_export_does_not_generate_or_audit_non_approved_orders(OrderStatus status)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12345678000199" };
        var creator = TestDbContextFactory.CreateOrderCreator("wmc-export-user");
        var order = new Order { Customer = customer, CreatedByUser = creator, Number = "ORO-2026-123", Status = status };
        db.AddRange(customer, creator, order);
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.ExportWmc(order.Id, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Empty(await db.WmcExportAudits.ToListAsync());
        Assert.Equal("Somente pedidos aprovados podem ser exportados para o WMC.", controller.TempData["WmcError"]);
    }

    [Fact]
    public async Task Wmc_export_filters_use_the_latest_audit_id_when_timestamps_match()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12345678000199" };
        var creator = TestDbContextFactory.CreateOrderCreator("wmc-filter-user");
        var order = new Order { Customer = customer, CreatedByUser = creator, Number = "ORO-2026-124", Status = OrderStatus.Approved };
        db.AddRange(customer, creator, order);
        await db.SaveChangesAsync();
        var exportedAt = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
        db.WmcExportAudits.AddRange(
            new WmcExportAudit { Id = 10, OrderId = order.Id, ExportedAt = exportedAt, Succeeded = false, Error = "Falha anterior" },
            new WmcExportAudit { Id = 20, OrderId = order.Id, ExportedAt = exportedAt, Succeeded = true, FileName = "WMC-1.txt" });
        await db.SaveChangesAsync();

        var result = await CreateController(db).Index(null, null, "succeeded");

        var orders = Assert.IsAssignableFrom<IReadOnlyList<Order>>(Assert.IsType<ViewResult>(result).Model);
        Assert.Single(orders);

        var failedResult = await CreateController(db).Index(null, null, "failed");
        var failedOrders = Assert.IsAssignableFrom<IReadOnlyList<Order>>(Assert.IsType<ViewResult>(failedResult).Model);
        Assert.Empty(failedOrders);
    }

    private static IntegrationsController CreateController(Orofoods.Web.Data.ApplicationDbContext db)
    {
        var controller = new IntegrationsController(
            db,
            null!,
            new WmcOrderFileGenerator(),
            new WmcExportAuditService(db),
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
        var context = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        controller.TempData = new TempDataDictionary(context, Mock.Of<ITempDataProvider>());
        return controller;
    }
}
