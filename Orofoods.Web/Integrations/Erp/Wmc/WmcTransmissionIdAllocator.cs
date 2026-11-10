using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcTransmissionIdAllocator(ApplicationDbContext db) : IWmcTransmissionIdAllocator
{
    private const string AssignSql = """
        UPDATE "Orders"
        SET "WmcTransmissionId" = nextval('"WmcTransmissionIdSequence"')::integer
        WHERE "Id" = @orderId AND "WmcTransmissionId" IS NULL
        RETURNING "WmcTransmissionId";
        """;

    private const string ReadSql = """
        SELECT "WmcTransmissionId"
        FROM "Orders"
        WHERE "Id" = @orderId;
        """;

    public async Task<int> GetOrAllocateAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (order.Id <= 0)
        {
            throw new InvalidOperationException("The order must be persisted before assigning a WMC transmission id.");
        }

        if (order.WmcTransmissionId is > 0)
        {
            return order.WmcTransmissionId.Value;
        }

        if (order.WmcTransmissionId is not null)
        {
            throw new InvalidOperationException("A persisted WMC transmission id must be positive.");
        }

        if (!db.Database.IsNpgsql())
        {
            throw new InvalidOperationException("WMC transmission ids are allocated by the PostgreSQL sequence.");
        }

        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            var allocated = await ExecuteScalarAsync(AssignSql, order.Id, cancellationToken);
            if (allocated is not null and not DBNull)
            {
                order.WmcTransmissionId = Convert.ToInt32(allocated, CultureInfo.InvariantCulture);
                return order.WmcTransmissionId.Value;
            }

            var existing = await ExecuteScalarAsync(ReadSql, order.Id, cancellationToken);
            if (existing is null or DBNull)
            {
                throw new InvalidOperationException($"Order.Id {order.Id} was not found while assigning its WMC transmission id.");
            }

            order.WmcTransmissionId = Convert.ToInt32(existing, CultureInfo.InvariantCulture);
            if (order.WmcTransmissionId <= 0)
            {
                throw new InvalidOperationException("A persisted WMC transmission id must be positive.");
            }

            return order.WmcTransmissionId.Value;
        }
        finally
        {
            if (openedHere)
            {
                await db.Database.CloseConnectionAsync();
            }
        }
    }

    private async Task<object?> ExecuteScalarAsync(string sql, int orderId, CancellationToken cancellationToken)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        if (db.Database.CurrentTransaction is { } currentTransaction)
        {
            command.Transaction = currentTransaction.GetDbTransaction();
        }

        var parameter = command.CreateParameter();
        parameter.ParameterName = "orderId";
        parameter.Value = orderId;
        command.Parameters.Add(parameter);
        return await command.ExecuteScalarAsync(cancellationToken);
    }
}
