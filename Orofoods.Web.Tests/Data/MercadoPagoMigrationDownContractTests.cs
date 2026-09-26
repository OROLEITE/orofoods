using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Orofoods.Web.Data.MigrationsPostgreSql;

namespace Orofoods.Web.Tests.Data;

public class MercadoPagoMigrationDownContractTests
{
    [Fact]
    public void Down_checks_for_multiple_payment_attempts_before_any_schema_change()
    {
        var operations = new AddMercadoPagoPaymentInfrastructure().DownOperations.ToArray();
        var guard = Assert.IsType<SqlOperation>(operations[0]);

        Assert.Contains("GROUP BY \"OrderId\"", guard.Sql, StringComparison.Ordinal);
        Assert.Contains("HAVING COUNT(*) > 1", guard.Sql, StringComparison.Ordinal);
        Assert.Contains("RAISE EXCEPTION", guard.Sql, StringComparison.Ordinal);
        Assert.Contains("multiple payment attempts", guard.Sql, StringComparison.OrdinalIgnoreCase);

        var dropIndexPosition = Array.FindIndex(operations, operation =>
            operation is DropIndexOperation { Name: "IX_Payments_OrderId" });
        Assert.True(dropIndexPosition > 0, "The duplicate guard must precede all destructive operations.");

        Assert.Contains(operations, operation =>
            operation is CreateIndexOperation
            {
                Name: "IX_Payments_OrderId",
                IsUnique: true
            });
    }
}
