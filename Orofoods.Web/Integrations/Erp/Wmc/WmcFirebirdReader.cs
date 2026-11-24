using System.Diagnostics;
using System.Data;
using System.Data.Common;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Logging;
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

public sealed class WmcFirebirdReader(
    IWmcConnectionFactory connectionFactory,
    IOptions<WmcFirebirdOptions> options,
    ILogger<WmcFirebirdReader> logger) : IWmcFirebirdReader
{
    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        Func<DbDataReader, T> map,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var queryKind = GetQueryKind(sql);
        var totalTimer = Stopwatch.StartNew();
        var connectionTimer = Stopwatch.StartNew();
        Stopwatch? sqlTimer = null;
        Stopwatch? resultTimer = null;
        long? connectionMs = null;
        long? sqlExecutionMs = null;
        long? resultReadMs = null;
        var failurePhase = "Connection";
        var results = new List<T>();

        async Task ExecuteCommandAsync(DbConnection connection, DbTransaction? transaction)
        {
            failurePhase = "CommandSetup";
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = options.Value.CommandTimeoutSeconds;
            command.Transaction = transaction;
            if (parameters is not null)
            {
                foreach (var (name, value) in parameters)
                {
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = name;
                    parameter.Value = value ?? DBNull.Value;
                    command.Parameters.Add(parameter);
                }
            }

            failurePhase = "SqlExecution";
            sqlTimer = Stopwatch.StartNew();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            sqlTimer.Stop();
            sqlExecutionMs = sqlTimer.ElapsedMilliseconds;

            failurePhase = "ResultRead";
            resultTimer = Stopwatch.StartNew();
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(map(reader));
            }
            resultTimer.Stop();
            resultReadMs = resultTimer.ElapsedMilliseconds;

            failurePhase = "ResultClose";
        }

        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            connectionTimer.Stop();
            connectionMs = connectionTimer.ElapsedMilliseconds;

            DbTransaction? transaction = null;
            if (queryKind is null)
            {
                failurePhase = "Transaction";
                transaction = await connection.BeginTransactionAsync(IsolationLevel.Snapshot, cancellationToken);
            }

            await using var transactionScope = transaction;
            await ExecuteCommandAsync(connection, transaction);

            if (transaction is not null)
            {
                failurePhase = "Commit";
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            totalTimer.Stop();
            if (queryKind is not null)
            {
                logger.LogInformation(
                    "WMC Firebird read completed. QueryKind={QueryKind} TransactionMode={TransactionMode} ConnectionMs={ConnectionMs} SqlExecutionMs={SqlExecutionMs} ResultReadMs={ResultReadMs} ResultCount={ResultCount} TotalMs={TotalMs}",
                    queryKind,
                    transaction is null ? "Implicit" : "Explicit",
                    connectionMs,
                    sqlExecutionMs,
                    resultReadMs,
                    results.Count,
                    totalTimer.ElapsedMilliseconds);
            }
            return results;
        }
        catch (OperationCanceledException exception)
        {
            totalTimer.Stop();
            if (queryKind is not null)
            {
                logger.LogWarning(
                    "WMC Firebird read canceled. QueryKind={QueryKind} TransactionMode={TransactionMode} FailurePhase={FailurePhase} ConnectionMs={ConnectionMs} SqlExecutionMs={SqlExecutionMs} ResultReadMs={ResultReadMs} ResultCount={ResultCount} TotalMs={TotalMs} ExceptionType={ExceptionType}",
                    queryKind,
                    queryKind is null ? "Explicit" : "Implicit",
                    failurePhase,
                    connectionMs ?? connectionTimer.ElapsedMilliseconds,
                    sqlExecutionMs ?? sqlTimer?.ElapsedMilliseconds,
                    resultReadMs ?? resultTimer?.ElapsedMilliseconds,
                    results.Count,
                    totalTimer.ElapsedMilliseconds,
                    exception.GetType().Name);
            }
            throw;
        }
        catch (Exception exception)
        {
            totalTimer.Stop();
            if (queryKind is not null)
            {
                logger.LogError(
                    "WMC Firebird read failed. QueryKind={QueryKind} TransactionMode={TransactionMode} FailurePhase={FailurePhase} ConnectionMs={ConnectionMs} SqlExecutionMs={SqlExecutionMs} ResultReadMs={ResultReadMs} ResultCount={ResultCount} TotalMs={TotalMs} ExceptionType={ExceptionType}",
                    queryKind,
                    queryKind is null ? "Explicit" : "Implicit",
                    failurePhase,
                    connectionMs ?? connectionTimer.ElapsedMilliseconds,
                    sqlExecutionMs ?? sqlTimer?.ElapsedMilliseconds,
                    resultReadMs ?? resultTimer?.ElapsedMilliseconds,
                    results.Count,
                    totalTimer.ElapsedMilliseconds,
                    exception.GetType().Name);
            }
            throw;
        }
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

    private static string? GetQueryKind(string sql) =>
        string.Equals(NormalizeSql(sql), NormalizeSql(WmcBrandReader.BrandListSql), StringComparison.OrdinalIgnoreCase)
            ? "BrandList"
            : null;

    private static string NormalizeSql(string sql) =>
        string.Join(' ', sql.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
