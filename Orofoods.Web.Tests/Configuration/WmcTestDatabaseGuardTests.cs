using Microsoft.Extensions.Configuration;
using Orofoods.Web.Configuration;

namespace Orofoods.Web.Tests.Configuration;

public sealed class WmcTestDatabaseGuardTests
{
    [Fact]
    public void Requires_the_dedicated_connection_even_when_default_connection_is_present()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Port=5432;Database=orofoods_local;Username=local;Password=secret"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => WmcTestDatabaseGuard.GetRequiredConnectionString(configuration));

        Assert.Contains("OROFOODS_WMC_TEST_POSTGRES_CONNECTION", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Accepts_only_the_fixed_disposable_postgres_host_port_and_database()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OROFOODS_WMC_TEST_POSTGRES_CONNECTION"] = "Host=wmc-postgres-persistente;Port=5432;Database=orofoods_wmc_teste;Username=test;Password=secret"
            })
            .Build();

        var connectionString = WmcTestDatabaseGuard.GetRequiredConnectionString(configuration);

        Assert.Equal("wmc-postgres-persistente", WmcTestDatabaseGuard.ValidateConnectionString(connectionString).Host);
    }

    [Theory]
    [InlineData("Host=localhost;Port=5432;Database=orofoods_wmc_teste;Username=test;Password=secret")]
    [InlineData("Host=wmc-postgres-persistente;Port=5433;Database=orofoods_wmc_teste;Username=test;Password=secret")]
    [InlineData("Host=wmc-postgres-persistente;Port=5432;Database=orofoods_local;Username=test;Password=secret")]
    public void Rejects_connection_strings_that_could_target_an_existing_database(string connectionString)
    {
        Assert.Throws<InvalidOperationException>(
            () => WmcTestDatabaseGuard.ValidateConnectionString(connectionString));
    }

    [Fact]
    public void Rejects_malformed_connection_strings_without_echoing_credentials()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => WmcTestDatabaseGuard.ValidateConnectionString(
                "Host=wmc-postgres-persistente;Port=not-a-port;Database=orofoods_wmc_teste;Password=secret"));

        Assert.DoesNotContain("secret", exception.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("172.21.0.2/32")]
    [InlineData("172.21.0.2")]
    public void Accepts_the_expected_database_on_a_private_docker_address(string address)
    {
        WmcTestDatabaseGuard.ValidateServerIdentity("orofoods_wmc_teste", address);
    }

    [Theory]
    [InlineData("172.21.0.2/not-a-prefix")]
    [InlineData("172.21.0.2/24")]
    [InlineData("172.21.0.2/33")]
    [InlineData("172.21.0.256/32")]
    [InlineData("172.21.0.2/32/32")]
    public void Rejects_an_invalid_postgresql_server_address_format(string address)
    {
        Assert.Throws<InvalidOperationException>(
            () => WmcTestDatabaseGuard.ValidateServerIdentity("orofoods_wmc_teste", address));
    }

    [Theory]
    [InlineData("orofoods_local", "172.21.0.2/32")]
    [InlineData("orofoods_wmc_teste", "8.8.8.8/32")]
    [InlineData("orofoods_wmc_teste", "127.0.0.1/32")]
    public void Rejects_an_incorrect_connected_database_identity(string database, string address)
    {
        Assert.Throws<InvalidOperationException>(
            () => WmcTestDatabaseGuard.ValidateServerIdentity(database, address));
    }
}
