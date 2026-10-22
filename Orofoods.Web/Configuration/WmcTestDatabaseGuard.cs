using System.Net;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Orofoods.Web.Configuration;

internal static class WmcTestDatabaseGuard
{
    internal const string ConnectionSetting = "OROFOODS_WMC_TEST_POSTGRES_CONNECTION";
    internal const string RequiredHost = "wmc-postgres-persistente";
    internal const int RequiredPort = 5432;
    internal const string RequiredDatabase = "orofoods_wmc_teste";

    internal static string GetRequiredConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration[ConnectionSetting];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"O host Testing exige a configuração explícita {ConnectionSetting}; DefaultConnection e User Secrets não são usados.");
        }

        ValidateConnectionString(connectionString);
        return connectionString;
    }

    internal static NpgsqlConnectionStringBuilder ValidateConnectionString(string connectionString)
    {
        NpgsqlConnectionStringBuilder settings;
        try
        {
            settings = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            throw new InvalidOperationException("A conexão PostgreSQL de teste é inválida.");
        }

        if (!string.Equals(settings.Host, RequiredHost, StringComparison.OrdinalIgnoreCase)
            || settings.Port != RequiredPort
            || !string.Equals(settings.Database, RequiredDatabase, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"O host Testing aceita somente {RequiredHost}:{RequiredPort}/{RequiredDatabase}.");
        }

        return settings;
    }

    internal static async Task AssertConnectedToDisposableDatabaseAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ValidateConnectionString(connectionString);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT current_database(), inet_server_addr()::text";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Não foi possível confirmar a identidade do PostgreSQL de teste.");

        var database = reader.GetString(0);
        var serverAddress = reader.IsDBNull(1) ? null : reader.GetString(1);
        ValidateServerIdentity(database, serverAddress);
    }

    internal static void ValidateServerIdentity(string database, string? serverAddress)
    {
        ValidateServerIdentity(database, ParsePostgreSqlServerAddress(serverAddress));
    }

    internal static void ValidateServerIdentity(string database, IPAddress? address)
    {
        if (!string.Equals(database, RequiredDatabase, StringComparison.Ordinal) || address is null || !IsPrivateAddress(address))
        {
            throw new InvalidOperationException(
                "O PostgreSQL conectado não corresponde ao banco descartável da homologação WMC.");
        }
    }

    private static IPAddress? ParsePostgreSqlServerAddress(string? serverAddress)
    {
        if (string.IsNullOrWhiteSpace(serverAddress))
            return null;

        var slashIndex = serverAddress.IndexOf('/');
        var addressText = slashIndex < 0 ? serverAddress : serverAddress[..slashIndex];
        if (!IPAddress.TryParse(addressText, out var address))
            return null;

        if (slashIndex >= 0)
        {
            if (slashIndex == 0
                || slashIndex != serverAddress.LastIndexOf('/')
                || slashIndex == serverAddress.Length - 1)
            {
                return null;
            }

            var expectedPrefix = address.AddressFamily switch
            {
                System.Net.Sockets.AddressFamily.InterNetwork => "32",
                System.Net.Sockets.AddressFamily.InterNetworkV6 => "128",
                _ => null
            };

            if (!string.Equals(serverAddress[(slashIndex + 1)..], expectedPrefix, StringComparison.Ordinal))
                return null;
        }

        return address.AddressFamily is System.Net.Sockets.AddressFamily.InterNetwork
            or System.Net.Sockets.AddressFamily.InterNetworkV6
                ? address
                : null;
    }

    private static bool IsPrivateAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168);
        }

        return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            && (bytes[0] & 0xfe) == 0xfc;
    }
}
