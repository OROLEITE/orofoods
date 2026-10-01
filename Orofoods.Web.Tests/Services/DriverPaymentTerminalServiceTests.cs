using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Delivery;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Services.Payments;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public sealed class DriverPaymentTerminalServiceTests
{
    [Fact]
    public async Task AssignAsync_RequiresActiveDriverAndTerminal()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var inactiveDriver = new Driver { Name = "Motorista inativo", IsActive = false };
        var activeDriver = new Driver { Name = "Motorista ativo" };
        var inactiveTerminal = new PaymentTerminal { Provider = PaymentTerminalProvider.MercadoPago, IsActive = false };
        var activeTerminal = new PaymentTerminal { Provider = PaymentTerminalProvider.MercadoPago };
        db.AddRange(inactiveDriver, activeDriver, inactiveTerminal, activeTerminal);
        await db.SaveChangesAsync();
        var sut = new DriverPaymentTerminalService(db, TimeProvider.System);

        var inactiveDriverResult = await sut.AssignAsync(inactiveDriver.Id, activeTerminal.Id);
        var inactiveTerminalResult = await sut.AssignAsync(activeDriver.Id, inactiveTerminal.Id);
        var validResult = await sut.AssignAsync(activeDriver.Id, activeTerminal.Id);

        Assert.False(inactiveDriverResult.Succeeded);
        Assert.False(inactiveTerminalResult.Succeeded);
        Assert.True(validResult.Succeeded);
        Assert.Equal("Motorista ativo", validResult.Assignment!.Driver!.Name);
    }

    [Fact]
    public async Task AssignAsync_RejectsSecondActiveOwnerForTerminal()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var drivers = new[] { new Driver { Name = "A" }, new Driver { Name = "B" } };
        var terminal = new PaymentTerminal { Provider = PaymentTerminalProvider.MercadoPago, DeviceId = "device-1" };
        db.AddRange(drivers);
        db.Add(terminal);
        await db.SaveChangesAsync();
        var sut = new DriverPaymentTerminalService(db, TimeProvider.System);

        var first = await sut.AssignAsync(drivers[0].Id, terminal.Id);
        var conflicting = await sut.AssignAsync(drivers[1].Id, terminal.Id);

        Assert.True(first.Succeeded);
        Assert.False(conflicting.Succeeded);
        Assert.Equal(1, await db.DriverPaymentTerminalAssignments.CountAsync(x => x.EndedAt == null));
    }

    [Fact]
    public async Task CreateTerminalAsync_ReturnsControlledFailureForDuplicateDeviceId()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var sut = new DriverPaymentTerminalService(db, TimeProvider.System);

        var first = await sut.CreateTerminalAsync(PaymentTerminalProvider.MercadoPago, "device-1", "store-1", "pos-1", true);
        var duplicate = await sut.CreateTerminalAsync(PaymentTerminalProvider.MercadoPago, "device-1", "store-2", "pos-2", true);

        Assert.True(first.Succeeded);
        Assert.False(duplicate.Succeeded);
        Assert.Equal("Não foi possível salvar o terminal devido a um conflito com outro cadastro.", duplicate.ErrorMessage);
        Assert.Single(await db.PaymentTerminals.ToListAsync());
    }

    [Fact]
    public async Task UpdateTerminalAsync_DoesNotChangeIdentifiersAfterAssignmentHistoryExists()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var driver = new Driver { Name = "Motorista" };
        var terminal = new PaymentTerminal { Provider = PaymentTerminalProvider.MercadoPago, DeviceId = "device-before", StoreId = "store-before", PosId = "pos-before" };
        db.AddRange(driver, terminal);
        await db.SaveChangesAsync();
        var sut = new DriverPaymentTerminalService(db, TimeProvider.System);
        Assert.True((await sut.AssignAsync(driver.Id, terminal.Id)).Succeeded);

        var result = await sut.UpdateTerminalAsync(terminal.Id, PaymentTerminalProvider.MercadoPago, "device-after", "store-after", "pos-after", true);

        Assert.False(result.Succeeded);
        Assert.Equal("Os identificadores de um terminal com histórico não podem ser alterados. Cadastre outro terminal para um novo dispositivo.", result.ErrorMessage);
        Assert.Equal("device-before", terminal.DeviceId);
        Assert.Equal("store-before", terminal.StoreId);
        Assert.Equal("pos-before", terminal.PosId);
    }

    [Fact]
    public async Task EndAsync_PreservesHistoryAndAllowsReassignment()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var firstDriver = new Driver { Name = "A" };
        var secondDriver = new Driver { Name = "B" };
        var terminal = new PaymentTerminal { Provider = PaymentTerminalProvider.MercadoPago, DeviceId = "device-1" };
        db.AddRange(firstDriver, secondDriver, terminal);
        await db.SaveChangesAsync();
        var sut = new DriverPaymentTerminalService(db, TimeProvider.System);
        var first = await sut.AssignAsync(firstDriver.Id, terminal.Id);

        var ended = await sut.EndAsync(first.Assignment!.Id);
        var second = await sut.AssignAsync(secondDriver.Id, terminal.Id);
        var rows = await sut.GetHistoryAsync(terminalId: terminal.Id);

        Assert.True(ended.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(2, rows.Count);
        Assert.NotNull(rows.Single(x => x.Id == first.Assignment.Id).EndedAt);
        Assert.Null(rows.Single(x => x.Id == second.Assignment!.Id).EndedAt);
    }

    [Fact]
    public async Task DeactivateDriverAsync_EndsAssignmentsWithoutDeletingHistory()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var driver = new Driver { Name = "A" };
        var terminal = new PaymentTerminal { Provider = PaymentTerminalProvider.MercadoPago, DeviceId = "device-1" };
        db.AddRange(driver, terminal);
        await db.SaveChangesAsync();
        var sut = new DriverPaymentTerminalService(db, TimeProvider.System);
        var assignment = await sut.AssignAsync(driver.Id, terminal.Id);

        var result = await sut.DeactivateDriverAsync(driver.Id);

        Assert.True(result.Succeeded);
        Assert.False(driver.IsActive);
        Assert.Single(db.DriverPaymentTerminalAssignments);
        Assert.NotNull((await db.DriverPaymentTerminalAssignments.SingleAsync()).EndedAt);
    }

    [Fact]
    public async Task DeactivateTerminalAsync_EndsAssignmentsWithoutDeletingHistory()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var driver = new Driver { Name = "A" };
        var terminal = new PaymentTerminal { Provider = PaymentTerminalProvider.MercadoPago, DeviceId = "device-1" };
        db.AddRange(driver, terminal);
        await db.SaveChangesAsync();
        var sut = new DriverPaymentTerminalService(db, TimeProvider.System);
        await sut.AssignAsync(driver.Id, terminal.Id);

        var result = await sut.DeactivateTerminalAsync(terminal.Id);

        Assert.True(result.Succeeded);
        Assert.False(terminal.IsActive);
        Assert.Single(db.DriverPaymentTerminalAssignments);
        Assert.NotNull((await db.DriverPaymentTerminalAssignments.SingleAsync()).EndedAt);
    }

    [Fact]
    public async Task History_ReturnsEndedAndCurrentAssignments()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var firstDriver = new Driver { Name = "A" };
        var secondDriver = new Driver { Name = "B" };
        var terminal = new PaymentTerminal { Provider = PaymentTerminalProvider.MercadoPago, DeviceId = "device-1" };
        db.AddRange(firstDriver, secondDriver, terminal);
        await db.SaveChangesAsync();
        var sut = new DriverPaymentTerminalService(db, TimeProvider.System);
        var first = await sut.AssignAsync(firstDriver.Id, terminal.Id);
        await sut.EndAsync(first.Assignment!.Id);
        await sut.AssignAsync(secondDriver.Id, terminal.Id);

        var history = await sut.GetHistoryAsync(terminalId: terminal.Id);

        Assert.Equal(2, history.Count);
        Assert.Equal(new[] { "B", "A" }, history.Select(x => x.Driver!.Name));
    }

    [Fact]
    public async Task DatabaseEnforcesSingleActiveTerminalAssignment()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var driverA = new Driver { Name = "A" };
        var driverB = new Driver { Name = "B" };
        var terminal = new PaymentTerminal { Provider = PaymentTerminalProvider.MercadoPago, DeviceId = "device-1" };
        db.AddRange(driverA, driverB, terminal);
        await db.SaveChangesAsync();
        db.DriverPaymentTerminalAssignments.AddRange(
            new DriverPaymentTerminalAssignment { DriverId = driverA.Id, PaymentTerminalId = terminal.Id, StartedAt = DateTime.UtcNow },
            new DriverPaymentTerminalAssignment { DriverId = driverB.Id, PaymentTerminalId = terminal.Id, StartedAt = DateTime.UtcNow });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void Models_DoNotExposeCredentialFields()
    {
        var models = new[] { typeof(Driver), typeof(PaymentTerminal), typeof(DriverPaymentTerminalAssignment) };

        Assert.All(models.SelectMany(type => type.GetProperties()), property =>
        {
            Assert.DoesNotContain("Token", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Secret", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Credential", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Card", property.Name, StringComparison.OrdinalIgnoreCase);
        });
    }
}
