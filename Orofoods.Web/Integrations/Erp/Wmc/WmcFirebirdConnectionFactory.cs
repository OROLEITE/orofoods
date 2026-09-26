using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Options;

namespace Orofoods.Web.Integrations.Erp.Wmc;

/// <summary>Builds short-lived connections to the WMC Firebird mirror. Read-only is enforced by the WMC-provided account and by never exposing a write-capable API here.</summary>
public interface IWmcConnectionFactory
{
    Task<FbConnection> OpenAsync(CancellationToken cancellationToken = default);
}

public sealed class WmcFirebirdConnectionFactory(IOptions<WmcFirebirdOptions> options) : IWmcConnectionFactory
{
    public async Task<FbConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var builder = new FbConnectionStringBuilder
        {
            DataSource = settings.Host,
            Port = settings.Port,
            Database = settings.Database,
            UserID = settings.User,
            Password = settings.Password,
            Charset = settings.Charset,
            ConnectionTimeout = settings.ConnectionTimeoutSeconds
        };

        var connection = new FbConnection(builder.ToString());
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
