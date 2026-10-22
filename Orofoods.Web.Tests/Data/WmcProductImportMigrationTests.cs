using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Data;

public sealed class WmcProductImportMigrationTests
{
    [Fact]
    public async Task Import_audit_entity_is_in_both_provider_models()
    {
        var postgresOptions = new DbContextOptionsBuilder<PostgreSqlApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .Options;
        using var postgres = new PostgreSqlApplicationDbContext(postgresOptions);
        await using var sqlite = await TestDbContextFactory.CreateAsync();

        Assert.NotNull(postgres.Model.FindEntityType("Orofoods.Web.Models.Integrations.WmcProductImportRun"));
        Assert.NotNull(sqlite.Model.FindEntityType("Orofoods.Web.Models.Integrations.WmcProductImportRun"));
    }

    [Theory]
    [InlineData("Data/Migrations/20261016161824_AddWmcBimboProductImportAuditSqlite.cs")]
    [InlineData("Data/MigrationsPostgreSql/20261016161823_AddWmcBimboProductImportAuditPostgreSql.cs")]
    public void Provider_migration_creates_audit_run_table(string relativePath)
    {
        var webRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var migrationPath = Path.Combine(webRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(migrationPath), $"Expected migration file {migrationPath}");
        var source = File.ReadAllText(migrationPath);
        Assert.Contains("WmcProductImportRuns", source, StringComparison.Ordinal);
        Assert.Contains("ImportedProductCodes", source, StringComparison.Ordinal);
        Assert.Contains("SkippedProductCodes", source, StringComparison.Ordinal);
        Assert.Contains("ErrorSummary", source, StringComparison.Ordinal);
    }
}
