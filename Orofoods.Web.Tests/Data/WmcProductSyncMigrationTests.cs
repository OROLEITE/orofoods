namespace Orofoods.Web.Tests.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using Orofoods.Web.Data;

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

    [Fact]
    public void PostgreSql_model_matches_last_snapshot_without_pending_changes()
    {
        var options = new DbContextOptionsBuilder<PostgreSqlApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .Options;
        using var context = new PostgreSqlApplicationDbContext(options);

        var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot;
        Assert.NotNull(snapshot);

        var designModel = context.GetService<IDesignTimeModel>().Model;
        var initializedSnapshot = context.GetService<IModelRuntimeInitializer>()
            .Initialize(snapshot!.Model, designTime: true);
        var differ = context.GetService<IMigrationsModelDiffer>();

        var differences = differ.GetDifferences(
            initializedSnapshot.GetRelationalModel(),
            designModel.GetRelationalModel());
        var summary = string.Join(Environment.NewLine, differences.Select(operation => operation switch
        {
            Microsoft.EntityFrameworkCore.Migrations.Operations.AlterColumnOperation alter
                => $"ALTER {alter.Table}.{alter.Name}: {alter.OldColumn?.ColumnType} -> {alter.ColumnType}",
            _ => operation.GetType().Name
        }));

        Assert.True(differences.Count == 0, summary);
    }
}
