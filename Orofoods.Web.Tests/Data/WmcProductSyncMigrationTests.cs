namespace Orofoods.Web.Tests.Data;

public class WmcProductSyncMigrationTests
{
    [Fact]
    public void PostgreSql_migration_enforces_normalized_wmc_code_uniqueness()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var migration = File.ReadAllText(Path.Combine(root, "Data", "MigrationsPostgreSql", "20261008120000_AddWmcProductSyncState.cs"));

        Assert.Contains("LOWER(BTRIM(\"WmcCode\"))", migration, StringComparison.Ordinal);
        Assert.Contains("NULLIF(BTRIM(\"WmcCode\"), '') IS NOT NULL", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("migrationBuilder.CreateIndex(\n            name: \"IX_Products_WmcCode\"", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void Product_model_does_not_reintroduce_a_raw_wmc_code_index()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var context = File.ReadAllText(Path.Combine(root, "Data", "ApplicationDbContext.cs"));
        var snapshot = File.ReadAllText(Path.Combine(root, "Data", "MigrationsPostgreSql", "PostgreSqlApplicationDbContextModelSnapshot.cs"));

        Assert.DoesNotContain("HasIndex(x => x.WmcCode)", context, StringComparison.Ordinal);
        Assert.DoesNotContain("b.HasIndex(\"WmcCode\")", snapshot, StringComparison.Ordinal);
    }
}
