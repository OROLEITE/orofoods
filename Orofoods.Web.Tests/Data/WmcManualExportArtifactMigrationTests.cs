using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Data;

public sealed class WmcManualExportArtifactMigrationTests
{
    [Fact]
    public void Artifact_and_rejected_state_are_configured_in_sqlite_and_postgres_models()
    {
        using var sqlite = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:").Options);
        using var postgres = new PostgreSqlApplicationDbContextDesignTimeFactory().CreateDbContext([]);

        foreach (var context in new DbContext[] { sqlite, postgres })
        {
            var model = context.GetService<IDesignTimeModel>().Model;
            var artifact = model.FindEntityType("Orofoods.Web.Models.Integrations.WmcManualExportArtifact");
            Assert.NotNull(artifact);
            Assert.Equal(typeof(string), artifact!.FindProperty("Content")!.ClrType);
            Assert.Equal(typeof(string), artifact.FindProperty("ContentSha256")!.ClrType);
            Assert.NotNull(artifact.FindProperty("AttemptId"));
            Assert.Contains(artifact.GetIndexes(), index => index.IsUnique &&
                index.Properties.Select(property => property.Name).SequenceEqual(["AttemptId"]));

            var state = model.FindEntityType("Orofoods.Web.Models.Integrations.WmcOrderExportState")!;
            Assert.NotNull(state.FindProperty("CurrentArtifactId"));
            Assert.Contains(state.GetCheckConstraints(), constraint =>
                constraint.Name == "CK_WmcOrderExportStates_Status" && constraint.Sql!.Contains("'Rejected'", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Both_migration_sets_include_manual_export_artifact_schema()
    {
        using var sqlite = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:").Options);
        using var postgres = new PostgreSqlApplicationDbContextDesignTimeFactory().CreateDbContext([]);

        Assert.Contains("20261013170000_AddWmcManualExportArtifactSqlite", sqlite.Database.GetMigrations());
        Assert.Contains("20261013170000_AddWmcManualExportArtifact", postgres.Database.GetMigrations());
    }

    [Fact]
    public void Adding_rejection_audit_outcomes_preserves_existing_integer_values()
    {
        Assert.Equal(0, (int)WmcExportOutcome.Unknown);
        Assert.Equal(1, (int)WmcExportOutcome.Generated);
        Assert.Equal(2, (int)WmcExportOutcome.Available);
        Assert.Equal(3, (int)WmcExportOutcome.Failed);
    }

    [Fact]
    public void Provider_migration_scripts_create_artifact_and_extend_status_constraint()
    {
        using var postgres = new PostgreSqlApplicationDbContextDesignTimeFactory().CreateDbContext([]);
        using var sqlite = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:").Options);

        var postgresScript = postgres.GetService<IMigrator>().GenerateScript(
            "20261013160000_AddWmcInitialLoadReleaseAudit",
            "20261013170000_AddWmcManualExportArtifact");
        var sqliteScript = sqlite.GetService<IMigrator>().GenerateScript(
            "20261013160001_AddWmcInitialLoadReleaseAuditSqlite",
            "20261013170000_AddWmcManualExportArtifactSqlite");

        foreach (var script in new[] { postgresScript, sqliteScript })
        {
            Assert.Contains("WmcManualExportArtifacts", script, StringComparison.Ordinal);
            Assert.Contains("ContentSha256", script, StringComparison.Ordinal);
            Assert.Contains("IX_WmcManualExportArtifacts_AttemptId", script, StringComparison.Ordinal);
            Assert.Contains("'Rejected'", script, StringComparison.Ordinal);
            Assert.Contains("CurrentArtifactId", script, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Sqlite_migration_preserves_legacy_state_and_audits_without_inventing_artifact()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;
        await using var db = new ApplicationDbContext(options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261013160001_AddWmcInitialLoadReleaseAuditSqlite");

        var sqliteConnection = (Microsoft.Data.Sqlite.SqliteConnection)db.Database.GetDbConnection();
        await using (var command = sqliteConnection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = OFF;";
            await command.ExecuteNonQueryAsync();
        }
        await InsertLegacyRowAsync(sqliteConnection, "Customers", new Dictionary<string, string>
        {
            ["LegalName"] = "'Cliente Ltda'", ["TradeName"] = "'Cliente'", ["Cnpj"] = "'03932292000199'"
        });
        var orderId = await InsertLegacyRowAsync(sqliteConnection, "Orders", new Dictionary<string, string>
        {
            ["Number"] = "'ORO-2026-000006'", ["CreatedAt"] = "'2026-10-13T12:00:00Z'",
            ["Status"] = "1", ["Total"] = "'19.90'"
        });
        await using (var legacyState = sqliteConnection.CreateCommand())
        {
            legacyState.CommandText = """
                INSERT INTO "WmcOrderExportStates" ("OrderId", "Status", "RequiresManualReview", "UpdatedAt", "Version", "FileName")
                VALUES ($orderId, 'Exported', 0, '2026-10-13T13:00:00Z', 2, 'WMC_ORO2026000006.txt');
                INSERT INTO "WmcExportAudits" ("OrderId", "ExportedAt", "FileName", "Succeeded", "Outcome")
                VALUES ($orderId, '2026-10-13T13:00:00Z', 'WMC_ORO2026000006.txt', 1, 1);
                """;
            legacyState.Parameters.AddWithValue("$orderId", orderId);
            await legacyState.ExecuteNonQueryAsync();
        }

        await migrator.MigrateAsync();

        var state = await db.WmcOrderExportStates.SingleAsync();
        Assert.Equal(WmcOrderExportStatus.Exported, state.Status);
        Assert.Equal("WMC_ORO2026000006.txt", state.FileName);
        Assert.Equal(2, state.Version);
        Assert.Null(state.CurrentArtifactId);
        Assert.Equal(1, await db.WmcExportAudits.CountAsync());
        Assert.Equal(0, await db.WmcManualExportArtifacts.CountAsync());
    }

    private static async Task<int> InsertLegacyRowAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        string table,
        IReadOnlyDictionary<string, string> overrides)
    {
        var columns = new List<(string Name, string Type, bool Required, bool PrimaryKey, bool HasDefault)>();
        await using (var schema = connection.CreateCommand())
        {
            schema.CommandText = $"PRAGMA table_info(\"{table}\")";
            await using var reader = await schema.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add((reader.GetString(1), reader.GetString(2), reader.GetInt32(3) == 1,
                    reader.GetInt32(5) == 1, !reader.IsDBNull(4)));
            }
        }

        var values = columns.Where(column => !column.PrimaryKey && !column.HasDefault
                && column.Required || overrides.ContainsKey(column.Name))
            .ToDictionary(column => column.Name,
                column => overrides.TryGetValue(column.Name, out var value)
                    ? value
                    : column.Type.Contains("INT", StringComparison.OrdinalIgnoreCase) ? "0" : "''");
        var names = string.Join(", ", values.Keys.Select(name => $"\"{name}\""));
        var rawValues = string.Join(", ", values.Values);
        await using (var insert = connection.CreateCommand())
        {
            insert.CommandText = $"INSERT INTO \"{table}\" ({names}) VALUES ({rawValues}); SELECT last_insert_rowid();";
            return Convert.ToInt32(await insert.ExecuteScalarAsync());
        }
    }
}
