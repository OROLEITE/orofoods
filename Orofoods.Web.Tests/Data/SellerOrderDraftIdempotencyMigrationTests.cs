using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using Orofoods.Web.Data;

namespace Orofoods.Web.Tests.Data;

public sealed class SellerOrderDraftIdempotencyMigrationTests
{
    [Fact]
    public void PostgreSql_model_matches_its_snapshot_after_idempotency_key_change()
    {
        var options = new DbContextOptionsBuilder<PostgreSqlApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .Options;
        using var context = new PostgreSqlApplicationDbContext(options);
        var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot;
        Assert.NotNull(snapshot);

        var model = context.GetService<IDesignTimeModel>().Model;
        var initializedSnapshot = context.GetService<IModelRuntimeInitializer>().Initialize(snapshot!.Model, designTime: true);
        var differences = context.GetService<IMigrationsModelDiffer>().GetDifferences(
            initializedSnapshot.GetRelationalModel(), model.GetRelationalModel());

        Assert.Empty(differences);
        Assert.Contains("20261013220000_AddSellerOrderDraftCreateRequestKey", context.Database.GetMigrations());

        var migrationAssembly = context.GetService<IMigrationsAssembly>();
        var migrationType = migrationAssembly.Migrations["20261013220000_AddSellerOrderDraftCreateRequestKey"];
        var migration = migrationAssembly.CreateMigration(migrationType, context.Database.ProviderName!);
        var migrationTarget = context.GetService<IModelRuntimeInitializer>()
            .Initialize(migration.TargetModel, designTime: true);
        Assert.Empty(context.GetService<IMigrationsModelDiffer>().GetDifferences(
            migrationTarget.GetRelationalModel(), model.GetRelationalModel()));
    }

    [Fact]
    public void Migration_only_adds_a_nullable_create_key_and_its_unique_owner_index()
    {
        var webRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var migration = File.ReadAllText(Path.Combine(webRoot, "Data", "MigrationsPostgreSql", "20261013220000_AddSellerOrderDraftCreateRequestKey.cs"));

        Assert.Contains("name: \"CreateRequestKey\"", migration, StringComparison.Ordinal);
        Assert.Contains("table: \"SellerOrderDrafts\"", migration, StringComparison.Ordinal);
        Assert.Contains("type: \"uuid\"", migration, StringComparison.Ordinal);
        Assert.Contains("nullable: true", migration, StringComparison.Ordinal);
        Assert.Contains("columns: new[] { \"OwnerUserId\", \"CreateRequestKey\" }", migration, StringComparison.Ordinal);
        Assert.Contains("unique: true", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("table: \"Orders\"", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("table: \"Products\"", migration, StringComparison.Ordinal);
    }
}
