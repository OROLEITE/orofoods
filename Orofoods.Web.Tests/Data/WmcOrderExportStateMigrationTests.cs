using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Data;

public sealed class WmcOrderExportStateMigrationTests
{
    [Fact]
    public void Both_database_migration_sets_include_persistent_order_export_state()
    {
        var sqliteOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var sqlite = new ApplicationDbContext(sqliteOptions);

        var postgresOptions = new DbContextOptionsBuilder<PostgreSqlApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=5432;Database=design_time;Username=design_time")
            .Options;
        using var postgres = new PostgreSqlApplicationDbContext(postgresOptions);

        Assert.Contains("20261012120000_AddWmcOrderExportStateSqlite", sqlite.Database.GetMigrations());
        Assert.Contains("20261012120000_AddWmcOrderExportState", postgres.Database.GetMigrations());
    }

    [Fact]
    public void Latest_sqlite_and_postgres_snapshots_match_the_current_wmc_model()
    {
        var sqliteOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var sqlite = new ApplicationDbContext(sqliteOptions);
        using var postgres = new PostgreSqlApplicationDbContextDesignTimeFactory().CreateDbContext([]);

        Assert.Equal(GetStateDateColumnType(sqlite, fromSnapshot: true), GetStateDateColumnType(sqlite, fromSnapshot: false));
        Assert.Equal(GetStateDateColumnType(postgres, fromSnapshot: true), GetStateDateColumnType(postgres, fromSnapshot: false));
        Assert.Equal("timestamp without time zone", GetAuditDateColumnType(postgres, fromSnapshot: true));
        Assert.Equal("timestamp without time zone", GetAuditDateColumnType(postgres, fromSnapshot: false));
        Assert.Empty(GetWmcPendingModelChanges(sqlite).Select(DescribeMigrationOperation));
        Assert.Empty(GetWmcPendingModelChanges(postgres).Select(DescribeMigrationOperation));
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var postgresMigration = File.ReadAllText(Path.Combine(projectPath, "Data", "MigrationsPostgreSql", "20261012120000_AddWmcOrderExportState.cs"));
        var postgresAuditMigration = File.ReadAllText(Path.Combine(projectPath, "Data", "MigrationsPostgreSql", "20260830204356_AddWmcExportAudits.cs"));
        var postgresAuditNormalizationMigration = File.ReadAllText(Path.Combine(projectPath, "Data", "MigrationsPostgreSql", "20260916124739_AddCommercialActivities.cs"));
        Assert.Contains($"UpdatedAt = table.Column<DateTime>(type: \"{GetStateDateColumnType(postgres, fromSnapshot: false)}\"", postgresMigration, StringComparison.Ordinal);
        Assert.Equal(GetAuditDateColumnType(postgres, fromSnapshot: true), GetAuditDateColumnType(postgres, fromSnapshot: false));
        Assert.Contains("ExportedAt = table.Column<DateTime>(type: \"timestamp with time zone\"", postgresAuditMigration, StringComparison.Ordinal);
        Assert.Contains("type: \"timestamp without time zone\"", postgresAuditNormalizationMigration, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Data/Migrations/20261012120000_AddWmcOrderExportStateSqlite.cs")]
    [InlineData("Data/MigrationsPostgreSql/20261012120000_AddWmcOrderExportState.cs")]
    public void Migration_creates_state_and_conservatively_backfills_existing_audit_history(string relativePath)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var migration = File.ReadAllText(Path.Combine(root, relativePath));

        Assert.Contains("name: \"WmcOrderExportStates\"", migration, StringComparison.Ordinal);
        Assert.Contains("table.PrimaryKey(\"PK_WmcOrderExportStates\", x => x.OrderId)", migration, StringComparison.Ordinal);
        Assert.Contains("CK_WmcOrderExportStates_Status", migration, StringComparison.Ordinal);
        Assert.Contains("FK_WmcOrderExportStates_Orders_OrderId", migration, StringComparison.Ordinal);
        Assert.Contains("WmcExportAudits", migration, StringComparison.Ordinal);
        Assert.Contains("'Pending'", migration, StringComparison.Ordinal);
        Assert.True(migration.Contains("TRUE", StringComparison.OrdinalIgnoreCase)
            || migration.Contains("1,", StringComparison.Ordinal));
        Assert.Contains("MAX(audit.\"ExportedAt\"", migration, StringComparison.Ordinal);
        Assert.Contains("latest.\"Id\" DESC", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("InsertData", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void PostgreSql_migration_generates_sql_without_connecting_to_a_database()
    {
        using var postgres = new PostgreSqlApplicationDbContextDesignTimeFactory().CreateDbContext([]);
        var script = postgres.GetService<IMigrator>().GenerateScript(
            "20261008120000_AddWmcProductSyncState",
            "20261012120000_AddWmcOrderExportState");

        Assert.Contains("CREATE TABLE \"WmcOrderExportStates\"", script, StringComparison.Ordinal);
        Assert.Contains("CK_WmcOrderExportStates_Status", script, StringComparison.Ordinal);
        Assert.Contains("PRIMARY KEY (\"OrderId\")", script, StringComparison.Ordinal);
        Assert.Contains("FK_WmcOrderExportStates_Orders_OrderId", script, StringComparison.Ordinal);
        Assert.Contains("MAX(audit.\"ExportedAt\")", script, StringComparison.Ordinal);
        Assert.DoesNotContain("AT TIME ZONE 'UTC'", script, StringComparison.Ordinal);
        Assert.Contains("'Pending'", script, StringComparison.Ordinal);

        var fullScript = postgres.GetService<IMigrator>().GenerateScript();
        Assert.Contains("ALTER TABLE \"WmcExportAudits\" ALTER COLUMN \"ExportedAt\" TYPE timestamp without time zone", fullScript, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sqlite_migration_backfills_legacy_audits_as_pending_manual_review_and_preserves_history()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        // The SQLite snapshot predates many non-WMC model changes; ignore that repository-wide warning to apply the historical chain and test this provider migration.
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;
        await using var db = new ApplicationDbContext(options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261008120000_AddWmcProductSyncStateSqlite");
        await ExecuteSqlAsync(connection, """
            INSERT INTO "AspNetUsers"
                ("Id", "AccessFailedCount", "EmailConfirmed", "IsActive", "LockoutEnabled", "PhoneNumberConfirmed", "TwoFactorEnabled",
                 "UserName", "NormalizedUserName", "Email", "NormalizedEmail", "SecurityStamp", "ConcurrencyStamp")
            VALUES ('legacy-order-creator', 0, 0, 1, 0, 0, 0,
                    'legacy-order-creator', 'LEGACY-ORDER-CREATOR', 'legacy@orofoods.local', 'LEGACY@OROFOODS.LOCAL', 'seed', 'seed');
            """);
        await ExecuteSqlAsync(connection, """
            INSERT INTO "Customers"
                ("Id", "LegalName", "TradeName", "Cnpj", "Status", "MinimumOrder", "CreditLimit", "CreditUsed",
                 "CreatedAt", "IsActive", "Email", "Phone", "ResponsibleDocument",
                 "ResponsibleName", "StateRegistration", "WhatsApp")
            VALUES (901, 'Legacy Ltda', 'Legacy', '12345678000199', 1, '0', '0', '0',
                    '2026-10-01 00:00:00', 1, '', '', '', '', '', '');
            """);
        await ExecuteSqlAsync(connection, """
            INSERT INTO "Orders"
                ("Id", "Number", "CustomerId", "CreatedByUserId", "CreatedAt", "Status", "Total", "Subtotal",
                 "Freight", "PaymentMethod", "Notes", "IntegrationStatus")
            VALUES (909991, 'ORO-2026-009991', 901, 'legacy-order-creator', '2026-10-01 00:00:00',
                    4, '63.20', '63.20', '0', 'PIX', '', 0);
            """);
        await ExecuteSqlAsync(connection, """
            INSERT INTO "Orders"
                ("Id", "Number", "CustomerId", "CreatedByUserId", "CreatedAt", "Status", "Total", "Subtotal",
                 "Freight", "PaymentMethod", "Notes", "IntegrationStatus")
            VALUES (909992, 'ORO-2026-009992', 901, 'legacy-order-creator', '2026-10-01 00:00:00',
                    4, '10.00', '10.00', '0', 'PIX', '', 0);
            """);
        var oldAuditTime = "2026-10-10 12:00:00";
        await ExecuteSqlAsync(connection, $"""
            INSERT INTO "WmcExportAudits"
                ("OrderId", "ExportedAt", "FileName", "Succeeded", "Error")
            VALUES
                (909991, '{oldAuditTime}', 'older-attempt.txt', 1, NULL),
                (909991, '2026-10-10 12:01:00', 'latest-attempt.txt', 0, 'Legacy failure');
            """);

        await migrator.MigrateAsync();

        var state = await db.WmcOrderExportStates.SingleAsync();
        Assert.Equal(909991, state.OrderId);
        Assert.Equal(WmcOrderExportStatus.Pending, state.Status);
        Assert.True(state.RequiresManualReview);
        Assert.Equal("latest-attempt.txt", state.FileName);
        Assert.Null(state.ExportedAt);
        Assert.Null(state.ImportedAt);
        Assert.Equal(new DateTime(2026, 10, 10, 12, 1, 0), state.UpdatedAt);
        Assert.Equal(2, await db.WmcExportAudits.CountAsync());
        Assert.Equal(1, await db.WmcExportAudits.CountAsync(audit => audit.Succeeded));
        Assert.Equal("Legacy failure", (await db.WmcExportAudits.SingleAsync(audit => !audit.Succeeded)).Error);
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => ExecuteSqlAsync(connection, """
            INSERT INTO "WmcOrderExportStates" ("OrderId", "Status", "RequiresManualReview", "UpdatedAt", "Version")
            VALUES (909992, 'Invalid', 0, '2026-10-12 00:00:00', 1);
            """));
    }

    private static async Task ExecuteSqlAsync(Microsoft.Data.Sqlite.SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static string GetStateDateColumnType(DbContext context, bool fromSnapshot)
    {
        var model = fromSnapshot
            ? context.GetService<IMigrationsAssembly>().ModelSnapshot!.Model
            : context.GetService<IDesignTimeModel>().Model;
        var entity = model.FindEntityType("Orofoods.Web.Models.Integrations.WmcOrderExportState")!;
        return entity.FindProperty("UpdatedAt")!.GetColumnType()!;
    }

    private static string GetAuditDateColumnType(DbContext context, bool fromSnapshot)
    {
        var model = fromSnapshot
            ? context.GetService<IMigrationsAssembly>().ModelSnapshot!.Model
            : context.GetService<IDesignTimeModel>().Model;
        var entity = model.FindEntityType("Orofoods.Web.Models.Integrations.WmcExportAudit")!;
        return entity.FindProperty("ExportedAt")!.GetColumnType()!;
    }

    private static IEnumerable<Microsoft.EntityFrameworkCore.Migrations.Operations.MigrationOperation> GetWmcPendingModelChanges(DbContext context)
    {
        var assembly = context.GetService<IMigrationsAssembly>();
        var snapshot = assembly.ModelSnapshot?.Model;
        if (snapshot is null) return [];
        snapshot = context.GetService<IModelRuntimeInitializer>().Initialize(snapshot);
        var current = context.GetService<IDesignTimeModel>().Model;
        var differ = context.GetService<IMigrationsModelDiffer>();
        return differ.GetDifferences(snapshot.GetRelationalModel(), current.GetRelationalModel())
            .Where(operation =>
            {
                var table = operation.GetType().GetProperty("Table")?.GetValue(operation)?.ToString();
                var name = operation.GetType().GetProperty("Name")?.GetValue(operation)?.ToString();
                return table is "WmcExportAudits" or "WmcOrderExportStates"
                    || (table == "Products" && name?.StartsWith("Wmc", StringComparison.Ordinal) == true)
                    || name?.StartsWith("FK_WmcExportAudits_", StringComparison.Ordinal) == true
                    || name?.StartsWith("FK_WmcOrderExportStates_", StringComparison.Ordinal) == true;
            });
    }

    private static string DescribeMigrationOperation(Microsoft.EntityFrameworkCore.Migrations.Operations.MigrationOperation operation) =>
        $"{operation.GetType().Name}:{operation.GetType().GetProperty("Table")?.GetValue(operation)}:{operation.GetType().GetProperty("Name")?.GetValue(operation)}";
}
