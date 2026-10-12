using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Integrations;
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
        Assert.NotEqual(Guid.Empty, audit.AttemptId);
        Assert.Equal(WmcExportSource.Manual, audit.Source);
        Assert.Equal(WmcExportOutcome.Generated, audit.Outcome);
        Assert.NotNull(audit.GeneratedAt);
    }

    [Fact]
    public async Task Repeated_updates_for_one_attempt_keep_a_single_audit_record()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        var user = TestDbContextFactory.CreateOrderCreator("wmc-attempt-user");
        var order = new Order { Number = "ORO-2026-000802", Customer = customer, CreatedByUser = user };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var attemptId = Guid.NewGuid();
        var generatedAt = DateTime.UtcNow.AddSeconds(-1);
        var service = new WmcExportAuditService(db);

        await service.RecordAttemptAsync(new WmcExportAudit
        {
            OrderId = order.Id,
            AttemptId = attemptId,
            Source = WmcExportSource.Automatic,
            Outcome = WmcExportOutcome.Generated,
            Succeeded = true,
            GeneratedAt = generatedAt,
            FileName = "WMC_ORO2026000802.txt"
        });
        await service.RecordAttemptAsync(new WmcExportAudit
        {
            OrderId = order.Id,
            AttemptId = attemptId,
            Source = WmcExportSource.Automatic,
            Outcome = WmcExportOutcome.Available,
            Succeeded = true,
            GeneratedAt = generatedAt,
            FileName = "WMC_ORO2026000802.txt"
        });

        var audits = await db.WmcExportAudits.ToListAsync();
        var audit = Assert.Single(audits);
        Assert.Equal(attemptId, audit.AttemptId);
        Assert.Equal(WmcExportSource.Automatic, audit.Source);
        Assert.Equal(WmcExportOutcome.Available, audit.Outcome);
        Assert.Equal(generatedAt, audit.GeneratedAt);
    }

    [Fact]
    public async Task Summarizes_whitespace_and_length_in_errors()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        var user = TestDbContextFactory.CreateOrderCreator("wmc-error-user");
        var order = new Order { Number = "ORO-2026-000803", Customer = customer, CreatedByUser = user };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        await new WmcExportAuditService(db).RecordAttemptAsync(new WmcExportAudit
        {
            OrderId = order.Id,
            AttemptId = Guid.NewGuid(),
            Source = WmcExportSource.Automatic,
            Outcome = WmcExportOutcome.Failed,
            Succeeded = false,
            Error = "  falha\r\n   detalhada " + new string('x', 800)
        });

        var error = (await db.WmcExportAudits.SingleAsync()).Error!;
        Assert.StartsWith("falha detalhada", error);
        Assert.DoesNotContain('\n', error);
        Assert.True(error.Length <= 500);
    }
}
