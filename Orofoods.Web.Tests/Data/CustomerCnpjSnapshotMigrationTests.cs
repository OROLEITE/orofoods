using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.Sqlite;
using Orofoods.Web.Data;

namespace Orofoods.Web.Tests.Data;

public sealed class CustomerCnpjSnapshotMigrationTests
{
    [Fact]
    public void Both_provider_models_include_nullable_order_snapshot_and_indexed_audit()
    {
        using var sqlite = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:").Options);
        using var postgres = new PostgreSqlApplicationDbContextDesignTimeFactory().CreateDbContext([]);

        foreach (var context in new DbContext[] { sqlite, postgres })
        {
            var model = context.GetService<IDesignTimeModel>().Model;
            var order = model.FindEntityType("Orofoods.Web.Models.Orders.Order")!;
            var snapshot = order.FindProperty("CustomerCnpjSnapshot")!;
            Assert.Equal(typeof(string), snapshot.ClrType);
            Assert.True(snapshot.IsNullable);
            Assert.Equal(18, snapshot.GetMaxLength());

            var audit = model.FindEntityType("Orofoods.Web.Models.Customers.CustomerCnpjAudit")!;
            Assert.Contains(audit.GetIndexes(), index => index.Properties.Select(property => property.Name).SequenceEqual(["CustomerId", "ChangedAt"]));
            Assert.Contains(audit.GetIndexes(), index => index.Properties.Select(property => property.Name).SequenceEqual(["OrderId", "ChangedAt"]));
        }
    }

    [Fact]
    public void Both_migration_sets_add_nullable_snapshot_and_audit_without_backfill_or_rebuilding_wmc_history()
    {
        using var sqlite = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:").Options);
        using var postgres = new PostgreSqlApplicationDbContextDesignTimeFactory().CreateDbContext([]);

        Assert.Contains("20261014160000_AddCustomerCnpjOrderSnapshotSqlite", sqlite.Database.GetMigrations());
        Assert.Contains("20261014150149_AddCustomerCnpjOrderSnapshot", postgres.Database.GetMigrations());

        var sqliteScript = sqlite.GetService<IMigrator>().GenerateScript(
            "20261013170000_AddWmcManualExportArtifactSqlite", "20261014160000_AddCustomerCnpjOrderSnapshotSqlite");
        var postgresScript = postgres.GetService<IMigrator>().GenerateScript(
            "20261013220000_AddSellerOrderDraftCreateRequestKey", "20261014150149_AddCustomerCnpjOrderSnapshot");

        foreach (var script in new[] { sqliteScript, postgresScript })
        {
            Assert.Contains("CustomerCnpjSnapshot", script, StringComparison.Ordinal);
            Assert.Contains("CustomerCnpjAudits", script, StringComparison.Ordinal);
            Assert.DoesNotContain("UPDATE \"Orders\"", script, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("UPDATE Orders", script, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("WmcManualExportArtifacts", script, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("WmcExportAudits", script, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Sqlite_migration_leaves_legacy_snapshot_null_and_preserves_wmc_artifact_and_audit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
        await using var db = new ApplicationDbContext(options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261013170000_AddWmcManualExportArtifactSqlite");

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = OFF;";
            await command.ExecuteNonQueryAsync();
        }

        var customerId = await InsertLegacyRowAsync(connection, "Customers", new Dictionary<string, string>
        {
            ["LegalName"] = "'Cliente Ltda'", ["TradeName"] = "'Cliente'", ["Cnpj"] = "'03932292000199'"
        });
        var orderId = await InsertLegacyRowAsync(connection, "Orders", new Dictionary<string, string>
        {
            ["Number"] = "'ORO-2026-000006'", ["CreatedAt"] = "'2026-10-13T12:00:00Z'",
            ["CustomerId"] = customerId.ToString(), ["Status"] = "1", ["Total"] = "'19.90'"
        });
        var attemptId = Guid.NewGuid();
        await InsertLegacyRowAsync(connection, "WmcManualExportArtifacts", new Dictionary<string, string>
        {
            ["OrderId"] = orderId.ToString(), ["AttemptId"] = $"'{attemptId}'",
            ["FileName"] = "'WMC_ORO-2026-000006.txt'", ["Content"] = "'TXT ORIGINAL\\r\\n'",
            ["ContentSha256"] = "'hash-original'", ["CreatedAt"] = "'2026-10-13T12:00:00Z'"
        });
        await InsertLegacyRowAsync(connection, "WmcExportAudits", new Dictionary<string, string>
        {
            ["OrderId"] = orderId.ToString(), ["AttemptId"] = $"'{attemptId}'", ["Source"] = "0",
            ["Outcome"] = "1", ["ExportedAt"] = "'2026-10-13T12:00:00Z'", ["Succeeded"] = "1",
            ["FileName"] = "'WMC_ORO-2026-000006.txt'"
        });

        await migrator.MigrateAsync();

        await using (var order = connection.CreateCommand())
        {
            order.CommandText = "SELECT \"CustomerCnpjSnapshot\" FROM \"Orders\" WHERE \"Id\" = $id";
            order.Parameters.AddWithValue("$id", orderId);
            Assert.True(await order.ExecuteScalarAsync() is DBNull);
        }
        await using (var artifact = connection.CreateCommand())
        {
            artifact.CommandText = "SELECT \"Content\", \"ContentSha256\" FROM \"WmcManualExportArtifacts\" WHERE \"OrderId\" = $id";
            artifact.Parameters.AddWithValue("$id", orderId);
            await using var reader = await artifact.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("TXT ORIGINAL\\r\\n", reader.GetString(0));
            Assert.Equal("hash-original", reader.GetString(1));
        }
        await using (var audit = connection.CreateCommand())
        {
            audit.CommandText = "SELECT COUNT(*) FROM \"WmcExportAudits\" WHERE \"OrderId\" = $id";
            audit.Parameters.AddWithValue("$id", orderId);
            Assert.Equal(1L, (long)(await audit.ExecuteScalarAsync())!);
        }
    }

    private static async Task<int> InsertLegacyRowAsync(
        SqliteConnection connection,
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

        var values = columns.Where(column => !column.PrimaryKey && !column.HasDefault && column.Required
                || overrides.ContainsKey(column.Name))
            .ToDictionary(column => column.Name,
                column => overrides.TryGetValue(column.Name, out var value)
                    ? value
                    : column.Type.Contains("INT", StringComparison.OrdinalIgnoreCase) ? "0" : "''");
        var names = string.Join(", ", values.Keys.Select(name => $"\"{name}\""));
        var rawValues = string.Join(", ", values.Values);
        await using var insert = connection.CreateCommand();
        insert.CommandText = $"INSERT INTO \"{table}\" ({names}) VALUES ({rawValues}); SELECT last_insert_rowid();";
        return Convert.ToInt32(await insert.ExecuteScalarAsync());
    }
}
