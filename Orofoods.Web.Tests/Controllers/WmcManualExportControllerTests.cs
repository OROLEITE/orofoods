using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Integrations;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Controllers;

public class WmcManualExportControllerTests
{
    [Fact]
    public async Task Manual_txt_export_records_rejected_order_attempt()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99", WmcCode = "107072" };
        var creator = TestDbContextFactory.CreateOrderCreator("wmc-manual-user");
        var order = new Order { Number = "ORO-2026-000804", Customer = customer, CreatedByUser = creator, Status = OrderStatus.Received };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.ExportWmc(order.Id, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var audit = Assert.Single(await db.WmcExportAudits.ToListAsync());
        Assert.False(audit.Succeeded);
        Assert.Equal(order.Id, audit.OrderId);
        Assert.Equal("wmc-manual-user", audit.ExportedByUserId);
        Assert.Equal("Somente pedidos aprovados podem ser exportados para o WMC.", audit.Error);
        Assert.Equal("Somente pedidos aprovados podem ser exportados para o WMC.", controller.TempData["WmcError"]);
    }

    private static IntegrationsController CreateController(ApplicationDbContext db)
    {
        var controller = new IntegrationsController(
            db,
            null!,
            new WmcManualExportService(db, WmcTestOrderFileGenerator.Create(), new WmcExportAuditService(db)),
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "wmc-manual-user"), new Claim(ClaimTypes.Name, "manual@orofoods.local")],
                "test"))
        };
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        controller.TempData = new TempDataDictionary(context, Mock.Of<ITempDataProvider>());
        return controller;
    }
}
