using System.Data.Common;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Options;

namespace Orofoods.Web.Integrations.Erp.Wmc;

/// <summary>
/// The only way application code may talk to the WMC Firebird mirror. Deliberately exposes just a
/// SELECT-shaped query method and a connectivity probe - there is no ExecuteNonQuery, ExecuteSqlRaw, or
/// raw DbCommand exposed here, so accidental writes are a compile-time impossibility, not a convention.
/// </summary>
public interface IWmcFirebirdReader
{
    Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        Func<DbDataReader, T> map,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default);

    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
}

public sealed class WmcFirebirdReader(IWmcConnectionFactory connectionFactory, IOptions<WmcFirebirdOptions> options) : IWmcFirebirdReader
{
    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        Func<DbDataReader, T> map,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = new FbCommand(sql, connection) { CommandTimeout = options.Value.CommandTimeoutSeconds };
        if (parameters is not null)
        {
            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value ?? DBNull.Value);
            }
        }

        var results = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(map(reader));
        }

        return results;
    }

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await QueryAsync("SELECT 1 FROM RDB$DATABASE", reader => reader.GetInt32(0), cancellationToken: cancellationToken);
            return rows.Count == 1;
        }
        catch (FbException)
        {
            return false;
        }
    }
}
