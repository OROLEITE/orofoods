using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Orofoods.Web.Data;

namespace Orofoods.Web.Services.Payments;

/// <summary>Uses an existing Order row as a transaction-scoped lock across application instances.</summary>
public sealed class PointPaymentOrderConcurrencyLock(ApplicationDbContext db) : IPointPaymentOrderConcurrencyLock
{
    private const string PostgreSqlProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";
    private const string SqliteProvider = "Microsoft.EntityFrameworkCore.Sqlite";
    private const string LockOrderSql = "SELECT \"Id\" FROM \"Orders\" WHERE \"Id\" = @orderId FOR UPDATE";

    private bool IsPostgreSql => string.Equals(db.Database.ProviderName, PostgreSqlProvider, StringComparison.Ordinal);
    private bool IsSqlite => string.Equals(db.Database.ProviderName, SqliteProvider, StringComparison.Ordinal);

    public IsolationLevel TransactionIsolationLevel => IsPostgreSql
        ? IsolationLevel.ReadCommitted
        : IsSqlite
            ? IsolationLevel.Serializable
            : throw new NotSupportedException("O lock Point precisa de uma implementação específica para este provedor de banco de dados.");

    public async Task<bool> AcquireAsync(int orderId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(orderId);
        if (IsSqlite)
        {
            return true;
        }
        if (!IsPostgreSql)
        {
            throw new NotSupportedException("O lock Point precisa de uma implementação específica para este provedor de banco de dados.");
        }

        var currentTransaction = db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("O lock Point precisa de uma transação ativa.");
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = currentTransaction.GetDbTransaction();
        command.CommandText = LockOrderSql;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "orderId";
        parameter.DbType = DbType.Int32;
        parameter.Value = orderId;
        command.Parameters.Add(parameter);

        var lockedId = await command.ExecuteScalarAsync(cancellationToken);
        return lockedId is not null and not DBNull;
    }
}
