using OrofoodsProductionDbReadOnly;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using System.Text.Json;

namespace OrofoodsProductionDbReadOnlyCrm.Tests;

public sealed class ProductionDiagnosticContractTests
{
    [Fact]
    public void Environment_guard_requires_exact_production_value()
    {
        Assert.True(ProductionDiagnosticContract.IsProductionEnvironment("Production"));
        Assert.False(ProductionDiagnosticContract.IsProductionEnvironment("production"));
        Assert.False(ProductionDiagnosticContract.IsProductionEnvironment("Staging"));
        Assert.False(ProductionDiagnosticContract.IsProductionEnvironment(null));
    }

    [Fact]
    public void Host_guard_accepts_only_the_authorized_postgres_server()
    {
        Assert.True(ProductionDiagnosticContract.IsExpectedHost("psql-orofoods-prd-01.postgres.database.azure.com"));
        Assert.True(ProductionDiagnosticContract.IsExpectedHost("psql-orofoods-prd-01.postgres.database.azure.com."));
        Assert.False(ProductionDiagnosticContract.IsExpectedHost("psql-orofoods-stg-01.postgres.database.azure.com"));
        Assert.False(ProductionDiagnosticContract.IsExpectedHost("localhost"));
    }

    [Fact]
    public void Read_only_sql_contains_guards_without_projecting_phone_numbers()
    {
        var sql = ProductionDiagnosticContract.ReadOnlySql;

        Assert.Contains("SET TRANSACTION READ ONLY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("transaction_read_only", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("__EFMigrationsHistory", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WhatsAppConversations", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT(*)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT \"PhoneNumber\"", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration_statuses_report_pending_and_unknown_applied_ids()
    {
        var lines = ProductionDiagnosticContract.FormatMigrationStatuses(
            ["20260101000000_First", "20260102000000_Second"],
            ["20260101000000_First", "20260103000000_Unexpected"]);

        Assert.Equal(
            [
                "20260101000000_First APPLIED",
                "20260102000000_Second PENDING",
                "20260103000000_Unexpected APPLIED_NOT_IN_PROJECT"
            ],
            lines);
    }

    [Fact]
    public void Migration_manifest_contains_crm_guard_and_unique_ids()
    {
        var manifestPath = Path.Combine(
            AppContext.BaseDirectory,
            "migrations.json");

        var ids = ProductionDiagnosticContract.LoadMigrationManifest(manifestPath);

        Assert.Contains("20261110151548_AddWhatsAppCrmOperationGuards", ids);
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Migration_manifest_matches_migrations_registered_for_postgresql_application_context()
    {
        using var context = new PostgreSqlApplicationDbContext(
            new DbContextOptionsBuilder<PostgreSqlApplicationDbContext>()
                .UseNpgsql("Host=127.0.0.1;Database=manifest_test;Username=manifest_test")
                .Options);
        var projectMigrationIds = context.Database.GetMigrations().ToArray();
        var manifestIds = JsonSerializer.Deserialize<string[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "migrations.json")));

        Assert.NotNull(manifestIds);
        Assert.Equal(manifestIds!.Length, manifestIds.Distinct(StringComparer.Ordinal).Count());
        Assert.True(
            projectMigrationIds.SequenceEqual(manifestIds, StringComparer.Ordinal),
            $"Migration manifest must match PostgreSQLApplicationDbContext order. " +
            $"Project migrations: {JsonSerializer.Serialize(projectMigrationIds)}. " +
            $"Manifest migrations: {JsonSerializer.Serialize(manifestIds)}.");
    }
}
