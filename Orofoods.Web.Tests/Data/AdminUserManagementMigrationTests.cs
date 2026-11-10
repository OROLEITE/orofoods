using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Data.Sqlite;
using Orofoods.Web.Data;

namespace Orofoods.Web.Tests.Data;

public sealed class AdminUserManagementMigrationTests
{
    [Fact]
    public async Task Sqlite_migration_backfills_existing_login_and_rolls_back_cleanly()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection).Options);
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20261015100000_AddAdminPortalAccessAudits");
        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO AspNetUsers
                (Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, IsActive,
                 LockoutEnabled, PhoneNumberConfirmed, TwoFactorEnabled, AccessFailedCount)
            VALUES ('legacy-user', 'legacy-login', 'LEGACY-LOGIN', 'legacy@test.local',
                    'LEGACY@TEST.LOCAL', 0, 1, 0, 0, 0, 0);
            """);

        await migrator.MigrateAsync("20261015130000_AddAdminUserManagement");

        var fullName = await context.Database.SqlQueryRaw<string>(
            "SELECT FullName AS Value FROM AspNetUsers WHERE Id = 'legacy-user'").SingleAsync();
        Assert.Equal("legacy-login", fullName);
        var mustChangePassword = await context.Database.SqlQueryRaw<bool>(
            "SELECT MustChangePassword AS Value FROM AspNetUsers WHERE Id = 'legacy-user'").SingleAsync();
        Assert.False(mustChangePassword);

        await migrator.MigrateAsync("20261015100000_AddAdminPortalAccessAudits");
        var columns = await context.Database.SqlQueryRaw<string>(
            "SELECT name AS Value FROM pragma_table_info('AspNetUsers')").ToListAsync();
        Assert.DoesNotContain("FullName", columns);
        Assert.DoesNotContain("MustChangePassword", columns);
    }

    [Fact]
    public void Both_provider_migrations_add_identity_fields_and_audited_user_changes()
    {
        using var sqlite = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:").Options);
        using var postgres = new PostgreSqlApplicationDbContextDesignTimeFactory().CreateDbContext([]);

        foreach (var context in new DbContext[] { sqlite, postgres })
        {
            var model = context.GetService<IDesignTimeModel>().Model;
            var user = model.FindEntityType("Orofoods.Web.Models.Identity.ApplicationUser")!;
            Assert.NotNull(user.FindProperty("FullName"));
            Assert.NotNull(user.FindProperty("MustChangePassword"));
            Assert.NotNull(model.FindEntityType("Orofoods.Web.Models.Identity.AdminUserChangeAudit"));

            var suffix = context is PostgreSqlApplicationDbContext
                ? "_AddAdminUserManagementPostgreSql"
                : "_AddAdminUserManagement";
            var migrationId = context.Database.GetMigrations().Single(id => id.EndsWith(suffix, StringComparison.Ordinal));
            Assert.Contains(migrationId, context.Database.GetMigrations());
            var assembly = context.GetService<IMigrationsAssembly>();
            var migration = assembly.CreateMigration(assembly.Migrations[migrationId], context.Database.ProviderName!);
            Assert.Contains(migration.UpOperations.OfType<AddColumnOperation>(), operation =>
                operation.Table == "AspNetUsers" && operation.Name == "FullName");
            Assert.Contains(migration.UpOperations.OfType<AddColumnOperation>(), operation =>
                operation.Table == "AspNetUsers" && operation.Name == "MustChangePassword");
            Assert.Contains(migration.UpOperations.OfType<CreateTableOperation>(), operation =>
                operation.Name == "AdminUserChangeAudits");
            Assert.Single(migration.UpOperations.OfType<CreateTableOperation>());
            Assert.Equal(2, migration.UpOperations.OfType<AddColumnOperation>().Count());
            Assert.Contains(migration.UpOperations.OfType<SqlOperation>(), operation =>
                operation.Sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase)
                && operation.Sql.Contains("UserName", StringComparison.Ordinal));
        }

        var sqliteScript = sqlite.GetService<IMigrator>().GenerateScript(
            "20261015100000_AddAdminPortalAccessAudits",
            sqlite.Database.GetMigrations().Single(id => id.EndsWith("_AddAdminUserManagement", StringComparison.Ordinal)));
        var postgresScript = postgres.GetService<IMigrator>().GenerateScript(
            "20261015100000_AddAdminPortalAccessAudits",
            postgres.Database.GetMigrations().Single(id => id.EndsWith("_AddAdminUserManagementPostgreSql", StringComparison.Ordinal)));
        Assert.Contains("AdminUserChangeAudits", sqliteScript, StringComparison.Ordinal);
        Assert.Contains("AdminUserChangeAudits", postgresScript, StringComparison.Ordinal);
    }
}
