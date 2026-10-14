using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
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
        const string migrationId = "20261013220000_AddSellerOrderDraftCreateRequestKey";
        var migrationIds = context.Database.GetMigrations().ToArray();
        var migrationIndex = Array.IndexOf(migrationIds, migrationId);
        Assert.True(migrationIndex > 0);

        var migrationType = migrationAssembly.Migrations[migrationId];
        var migration = migrationAssembly.CreateMigration(migrationType, context.Database.ProviderName!);
        var modelInitializer = context.GetService<IModelRuntimeInitializer>();
        var migrationTarget = modelInitializer.Initialize(migration.TargetModel, designTime: true);
        var previousMigrationType = migrationAssembly.Migrations[migrationIds[migrationIndex - 1]];
        var previousMigration = migrationAssembly.CreateMigration(previousMigrationType, context.Database.ProviderName!);
        var previousTarget = modelInitializer.Initialize(previousMigration.TargetModel, designTime: true);
        var historicalChanges = context.GetService<IMigrationsModelDiffer>().GetDifferences(
            previousTarget.GetRelationalModel(), migrationTarget.GetRelationalModel());

        // This migration's target is historical: later migrations add the fiscal snapshot and audit table.
        // Verify its target model describes exactly the schema operations in this migration instead of
        // comparing it with today's model, which correctly contains those later changes.
        Assert.Equal(
            historicalChanges.Select(GetOperationSignature),
            migration.UpOperations.Select(GetOperationSignature));
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

    private static MigrationOperationSignature GetOperationSignature(MigrationOperation operation) => operation switch
    {
        AddColumnOperation addColumn => new(
            "AddColumn", addColumn.Table, addColumn.Name, addColumn.ClrType,
            addColumn.ColumnType, addColumn.IsNullable, addColumn.MaxLength),
        CreateIndexOperation createIndex => new(
            "CreateIndex", createIndex.Table, createIndex.Name,
            Columns: string.Join(",", createIndex.Columns), IsUnique: createIndex.IsUnique),
        _ => throw new InvalidOperationException($"Unexpected migration operation: {operation.GetType().Name}")
    };

    private sealed record MigrationOperationSignature(
        string Operation,
        string Table,
        string Name,
        Type? ClrType = null,
        string? ColumnType = null,
        bool? IsNullable = null,
        int? MaxLength = null,
        string? Columns = null,
        bool? IsUnique = null);
}
