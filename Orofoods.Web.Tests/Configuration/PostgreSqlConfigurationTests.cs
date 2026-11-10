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

    [Fact]
    public void Required_default_connection_rejects_an_unresolved_key_vault_reference()
    {
        const string unresolvedReference = "@Microsoft.KeyVault(SecretUri=https://kv-orofoods-prd-01.vault.azure.net/secrets/orofoods-prd-connection)";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                [new KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection", unresolvedReference)])
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => DatabaseConfiguration.GetRequiredDefaultConnectionString(configuration));

        Assert.Contains("unresolved Azure Key Vault reference", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(unresolvedReference, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_startup_does_not_migrate_or_seed_roles_automatically()
    {
        var program = ReadProgram();
        var startupDatabaseBlock = program[program.LastIndexOf("using (var scope = app.Services.CreateScope()", StringComparison.Ordinal)..];

        Assert.Contains("else if (!app.Environment.IsProduction())", startupDatabaseBlock);
        Assert.Contains("await db.Database.MigrateAsync();", startupDatabaseBlock);
        Assert.Contains("await SeedData.EnsureSellerRoleAsync(roleManager);", startupDatabaseBlock);
        Assert.Contains("KeyVaultReferenceGuard.EnsureResolved(\"Jwt:Key\", jwtKey)", program);
    }

    [Fact]
    public void Production_does_not_register_automatic_wmc_workers()
    {
        var program = ReadProgram();

        AssertWorkerRegistrationIsOutsideProduction(program, "WmcJsonOrderHomologationBackgroundService");
        AssertWorkerRegistrationIsOutsideProduction(program, "WmcJsonOrderAutomaticBackgroundService");
        AssertWorkerRegistrationIsOutsideProduction(program, "WmcSyncWorker");
    }

    private static string ReadProgram()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        return File.ReadAllText(Path.Combine(projectPath, "Program.cs"));
    }

    private static void AssertWorkerRegistrationIsOutsideProduction(string program, string workerType)
    {
        var registration = $"builder.Services.AddHostedService<{workerType}>();";
        var registrationIndex = program.IndexOf(registration, StringComparison.Ordinal);
        var lastEnvironmentGuard = program.LastIndexOf("if (!builder.Environment.IsProduction())", registrationIndex, StringComparison.Ordinal);

        Assert.True(registrationIndex >= 0, $"Registration not found for {workerType}.");
        Assert.True(lastEnvironmentGuard >= 0, $"{workerType} must be registered only outside Production.");
        Assert.DoesNotContain("}", program[lastEnvironmentGuard..registrationIndex]);
    }
}
