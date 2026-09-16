namespace Orofoods.Web.Tests.Configuration;

public class PostgreSqlConfigurationTests
{
    [Fact]
    public void Application_runtime_uses_npgsql_instead_of_sqlite()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var project = File.ReadAllText(Path.Combine(projectPath, "Orofoods.Web.csproj"));
        var program = File.ReadAllText(Path.Combine(projectPath, "Program.cs"));

        Assert.Contains("Npgsql.EntityFrameworkCore.PostgreSQL", project);
        Assert.Contains("UseNpgsql(connectionString)", program);
        Assert.DoesNotContain("UseSqlite(connectionString)", program);
    }
}
