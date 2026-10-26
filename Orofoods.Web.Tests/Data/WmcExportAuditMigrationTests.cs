using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Data;

public class WmcExportAuditMigrationTests
{
    [Fact]
    public async Task Wmc_export_audit_model_stores_the_seller_code_and_destination_for_reconciliation()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var auditType = db.Model.FindEntityType(typeof(WmcExportAudit));

        Assert.NotNull(auditType!.FindProperty("WmcSalesRepresentativeCode"));
        Assert.NotNull(auditType.FindProperty("WmcDestination"));
    }

    [Fact]
    public async Task Current_sqlite_schema_stores_wmc_export_audits()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        var user = new ApplicationUser { Id = "user-1", UserName = "user@orofoods.local", NormalizedUserName = "USER@OROFOODS.LOCAL", Email = "user@orofoods.local", NormalizedEmail = "USER@OROFOODS.LOCAL" };
        var order = new Order { Number = "ORO-2026-000802", Customer = customer, CreatedByUser = user, WmcTransmissionId = 9001 };
        db.WmcExportAudits.Add(new WmcExportAudit
        {
            Order = order,
            ExportedByUserId = "admin-id",
            ExportedByEmail = "admin@orofoods.local",
            FileName = "WMC_ORO2026000802.txt",
            Succeeded = true,
            Outcome = WmcExportOutcome.Succeeded,
            WmcTransmissionId = 9001,
            WmcSalesRepresentativeCode = 804,
            WmcDestination = "https://wmc.example.invalid/integracao",
            NroPedidoHttpStatusCode = 200,
            NroPedidoResponseBody = "[{\"NROPEDIDO\":776655}]"
        });

        await db.SaveChangesAsync();

        var audit = await db.WmcExportAudits.SingleAsync();
        Assert.Equal("WMC_ORO2026000802.txt", audit.FileName);
        Assert.Equal("admin@orofoods.local", audit.ExportedByEmail);
        Assert.Equal(WmcExportSource.Manual, audit.Source);
        Assert.Equal(WmcExportOutcome.Succeeded, audit.Outcome);
        Assert.Equal(9001, audit.WmcTransmissionId);
        Assert.Equal(804, audit.WmcSalesRepresentativeCode);
        Assert.Equal("https://wmc.example.invalid/integracao", audit.WmcDestination);
        Assert.Equal(200, audit.NroPedidoHttpStatusCode);
        Assert.Equal("[{\"NROPEDIDO\":776655}]", audit.NroPedidoResponseBody);
        Assert.Equal(9001, (await db.Orders.SingleAsync()).WmcTransmissionId);
    }

    [Fact]
    public async Task Enforces_unique_wmc_transmission_ids_per_order()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente", TradeName = "Cliente", Cnpj = "00000000000000" };
        var creator = TestDbContextFactory.CreateOrderCreator();
        db.Orders.AddRange(
            new Order { Number = "ORO-2026-000803", Customer = customer, CreatedByUser = creator, WmcTransmissionId = 1 },
            new Order { Number = "ORO-2026-000804", Customer = customer, CreatedByUser = creator, WmcTransmissionId = 1 });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void Sqlite_and_postgresql_register_the_wmc_http_audit_migration_without_connecting()
    {
        var sqliteOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var sqlite = new ApplicationDbContext(sqliteOptions);
        Assert.Contains("20261021140000_AddWmcJsonOrderAuditState", sqlite.Database.GetMigrations());

        using var postgresql = new PostgreSqlApplicationDbContextDesignTimeFactory().CreateDbContext([]);
        Assert.Contains("20261021140000_AddWmcJsonOrderAuditState", postgresql.Database.GetMigrations());
        Assert.Contains("20261021150000_AddWmcTransmissionIdentity", postgresql.Database.GetMigrations());
        Assert.Contains("20261021160000_AddWmcOrderReconciliationMetadata", postgresql.Database.GetMigrations());
        Assert.Contains("20261021150000_AddWmcTransmissionIdentity", sqlite.Database.GetMigrations());
        Assert.Contains("20261021160000_AddWmcOrderReconciliationMetadata", sqlite.Database.GetMigrations());

        var script = postgresql.Database.GetService<IMigrator>().GenerateScript(
            "20261021140000_AddWmcJsonOrderAuditState",
            "20261021160000_AddWmcOrderReconciliationMetadata");
        Assert.Contains("CREATE SEQUENCE \"WmcTransmissionIdSequence\" AS integer START WITH 1", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NO CYCLE", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IX_Orders_WmcTransmissionId", script, StringComparison.Ordinal);
        Assert.Contains("WmcSalesRepresentativeCode", script, StringComparison.Ordinal);
        Assert.Contains("WmcDestination", script, StringComparison.Ordinal);
    }
}
