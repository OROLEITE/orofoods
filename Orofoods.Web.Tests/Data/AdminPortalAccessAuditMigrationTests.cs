using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Orofoods.Web.Data;

namespace Orofoods.Web.Tests.Data;

public sealed class AdminPortalAccessAuditMigrationTests
{
    [Fact]
    public void Both_provider_models_and_migrations_include_the_access_audit_table()
    {
        using var sqlite = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:").Options);
        using var postgres = new PostgreSqlApplicationDbContextDesignTimeFactory().CreateDbContext([]);

        foreach (var context in new DbContext[] { sqlite, postgres })
        {
            var model = context.GetService<IDesignTimeModel>().Model;
            var audit = model.FindEntityType("Orofoods.Web.Models.Identity.AdminPortalAccessAudit")!;
            Assert.Contains(audit.GetIndexes(), index => index.Properties.Select(property => property.Name)
                .SequenceEqual(["AdministratorUserId", "SelectedAt"]));
            Assert.Contains(audit.GetIndexes(), index => index.Properties.Select(property => property.Name)
                .SequenceEqual(["AccessType", "TargetId", "SelectedAt"]));

            var migrationAssembly = context.GetService<IMigrationsAssembly>();
            var snapshot = migrationAssembly.ModelSnapshot;
            Assert.NotNull(snapshot);
            Assert.NotNull(snapshot!.Model.FindEntityType("Orofoods.Web.Models.Identity.AdminPortalAccessAudit"));
            const string migrationId = "20261015100000_AddAdminPortalAccessAudits";
            Assert.Contains(migrationId, context.Database.GetMigrations());
            var migration = migrationAssembly.CreateMigration(
                migrationAssembly.Migrations[migrationId], context.Database.ProviderName!);
            Assert.Contains(migration.UpOperations.OfType<CreateTableOperation>(), operation =>
                operation.Name == "AdminPortalAccessAudits");
        }

        var sqliteScript = sqlite.GetService<IMigrator>().GenerateScript(
            "20261014160000_AddCustomerCnpjOrderSnapshotSqlite", "20261015100000_AddAdminPortalAccessAudits");
        var postgresScript = postgres.GetService<IMigrator>().GenerateScript(
            "20261014150149_AddCustomerCnpjOrderSnapshot", "20261015100000_AddAdminPortalAccessAudits");

        Assert.Contains("AdminPortalAccessAudits", sqliteScript, StringComparison.Ordinal);
        Assert.Contains("AdminPortalAccessAudits", postgresScript, StringComparison.Ordinal);
    }
}
