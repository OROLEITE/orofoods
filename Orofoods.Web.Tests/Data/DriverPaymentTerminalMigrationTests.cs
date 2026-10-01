using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Orofoods.Web.Data.MigrationsPostgreSql;

namespace Orofoods.Web.Tests.Data;

public sealed class DriverPaymentTerminalMigrationTests
{
    [Fact]
    public void UpCreatesThePostgreSqlDriverTerminalHistorySchemaAndPaymentReference()
    {
        Migration migration = new AddDriverPaymentTerminalArchitecture();
        var operations = migration.UpOperations.ToArray();

        var driver = Assert.Single(operations.OfType<CreateTableOperation>(), x => x.Name == "Drivers");
        Assert.Contains(driver.Columns, x => x.Name == "Name" && !x.IsNullable);
        Assert.Contains(driver.Columns, x => x.Name == "IsActive" && !x.IsNullable);

        var terminal = Assert.Single(operations.OfType<CreateTableOperation>(), x => x.Name == "PaymentTerminals");
        Assert.Contains(terminal.Columns, x => x.Name == "Provider" && !x.IsNullable);
        Assert.Contains(terminal.Columns, x => x.Name == "DeviceId");
        Assert.Contains(terminal.Columns, x => x.Name == "StoreId");
        Assert.Contains(terminal.Columns, x => x.Name == "PosId");
        Assert.DoesNotContain(terminal.Columns, x => x.Name.Contains("Token", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase));

        var assignments = Assert.Single(operations.OfType<CreateTableOperation>(), x => x.Name == "DriverPaymentTerminalAssignments");
        Assert.Contains(assignments.Columns, x => x.Name == "StartedAt" && !x.IsNullable);
        Assert.Contains(assignments.Columns, x => x.Name == "EndedAt" && x.IsNullable);
        Assert.Contains(assignments.ForeignKeys, x => x.Name == "FK_DriverPaymentTerminalAssignments_Drivers_DriverId" && x.PrincipalTable == "Drivers");
        Assert.Contains(assignments.ForeignKeys, x => x.Name == "FK_DriverPaymentTerminalAssignments_PaymentTerminals_PaymentTe~" && x.PrincipalTable == "PaymentTerminals");

        var assignmentIndex = Assert.Single(operations.OfType<CreateIndexOperation>(), x => x.Table == "DriverPaymentTerminalAssignments" && x.IsUnique);
        Assert.Equal("IX_DriverPaymentTerminalAssignments_PaymentTerminalId", assignmentIndex.Name);
        Assert.Equal(new[] { "PaymentTerminalId" }, assignmentIndex.Columns);
        Assert.Contains("EndedAt\" IS NULL", assignmentIndex.Filter, StringComparison.Ordinal);

        var terminalIdentityIndex = Assert.Single(operations.OfType<CreateIndexOperation>(), x => x.Table == "PaymentTerminals" && x.IsUnique);
        Assert.Equal("IX_PaymentTerminals_Provider_DeviceId", terminalIdentityIndex.Name);
        Assert.Equal(new[] { "Provider", "DeviceId" }, terminalIdentityIndex.Columns);
        Assert.Contains("DeviceId\" IS NOT NULL", terminalIdentityIndex.Filter, StringComparison.Ordinal);

        Assert.Contains(operations.OfType<CreateIndexOperation>(), x => x.Table == "Drivers" && x.Name == "IX_Drivers_Name");
        Assert.Contains(operations.OfType<CreateIndexOperation>(), x => x.Table == "DriverPaymentTerminalAssignments" && x.Name == "IX_DriverPaymentTerminalAssignments_DriverId");
        Assert.Contains(operations.OfType<CreateIndexOperation>(), x => x.Table == "Payments" && x.Name == "IX_Payments_DriverPaymentTerminalAssignmentId");

        Assert.Contains(operations.OfType<AddColumnOperation>(), x => x.Table == "Payments" && x.Name == "DriverPaymentTerminalAssignmentId" && x.IsNullable);
        Assert.Contains(operations.OfType<AddForeignKeyOperation>(), x => x.Table == "Payments" && x.Name == "FK_Payments_DriverPaymentTerminalAssignments_DriverPaymentTerm~" && x.PrincipalTable == "DriverPaymentTerminalAssignments");
    }

    [Fact]
    public void DownRemovesOnlyTheNewPaymentReferenceAndTables()
    {
        Migration migration = new AddDriverPaymentTerminalArchitecture();
        var operations = migration.DownOperations.ToArray();

        Assert.Contains(operations.OfType<DropColumnOperation>(), x => x.Table == "Payments" && x.Name == "DriverPaymentTerminalAssignmentId");
        Assert.Contains(operations.OfType<DropTableOperation>(), x => x.Name == "DriverPaymentTerminalAssignments");
        Assert.Contains(operations.OfType<DropTableOperation>(), x => x.Name == "PaymentTerminals");
        Assert.Contains(operations.OfType<DropTableOperation>(), x => x.Name == "Drivers");
    }
}
