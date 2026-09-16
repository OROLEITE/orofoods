namespace Orofoods.Web.Tests.Data;

public class MigrationIsolationTests
{
    [Fact]
    public void Sqlite_migration_folder_does_not_contain_postgresql_context_metadata()
    {
        var migrationsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web/Data/Migrations"));
        var migrationDesigners = Directory.GetFiles(migrationsPath, "*.Designer.cs");

        Assert.All(migrationDesigners, file =>
            Assert.DoesNotContain("PostgreSqlApplicationDbContext", File.ReadAllText(file)));
    }
}
