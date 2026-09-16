using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Tests.Data;

public class WmcExportAuditMigrationTests
{
    [Fact]
    public async Task Current_sqlite_schema_stores_wmc_export_audits()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.GetService<IMigrator>().MigrateAsync();

        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        var user = new ApplicationUser { Id = "user-1", UserName = "user@orofoods.local", NormalizedUserName = "USER@OROFOODS.LOCAL", Email = "user@orofoods.local", NormalizedEmail = "USER@OROFOODS.LOCAL" };
        var order = new Order { Number = "ORO-2026-000802", Customer = customer, CreatedByUser = user };
        db.WmcExportAudits.Add(new WmcExportAudit { Order = order, ExportedByUserId = "admin-id", ExportedByEmail = "admin@orofoods.local", FileName = "WMC_ORO2026000802.txt", Succeeded = true });

        await db.SaveChangesAsync();

        var audit = await db.WmcExportAudits.SingleAsync();
        Assert.Equal("WMC_ORO2026000802.txt", audit.FileName);
        Assert.Equal("admin@orofoods.local", audit.ExportedByEmail);
    }
}
