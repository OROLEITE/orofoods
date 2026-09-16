using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Integrations;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class WmcExportAuditServiceTests
{
    [Fact]
    public async Task Records_the_result_of_a_wmc_export_attempt()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        var user = new ApplicationUser { Id = "user-1", UserName = "user@orofoods.local", NormalizedUserName = "USER@OROFOODS.LOCAL", Email = "user@orofoods.local", NormalizedEmail = "USER@OROFOODS.LOCAL" };
        var order = new Order { Number = "ORO-2026-000801", Customer = customer, CreatedByUser = user };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        await new WmcExportAuditService(db).RecordAsync(order.Id, "admin-id", "admin@orofoods.local", "WMC_ORO2026000801.txt", true, null);

        var audit = await db.WmcExportAudits.SingleAsync();
        Assert.Equal(order.Id, audit.OrderId);
        Assert.Equal("admin-id", audit.ExportedByUserId);
        Assert.Equal("admin@orofoods.local", audit.ExportedByEmail);
        Assert.Equal("WMC_ORO2026000801.txt", audit.FileName);
        Assert.True(audit.Succeeded);
        Assert.Null(audit.Error);
    }
}
