using Microsoft.Extensions.Configuration;
using Orofoods.Web.Configuration;

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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Required_default_connection_rejects_missing_empty_or_whitespace_values(string? configuredValue)
    {
        var values = configuredValue is null
            ? Array.Empty<KeyValuePair<string, string?>>()
            : [new KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection", configuredValue)];
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => DatabaseConfiguration.GetRequiredDefaultConnectionString(configuration));

        Assert.Equal("Connection string 'DefaultConnection' is required.", exception.Message);
    }

    [Fact]
    public void Required_default_connection_accepts_a_non_empty_external_configuration_value()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                [new KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection", "configured-externally")])
            .Build();

        var connectionString = DatabaseConfiguration.GetRequiredDefaultConnectionString(configuration);

        Assert.Equal("configured-externally", connectionString);
    }
}
