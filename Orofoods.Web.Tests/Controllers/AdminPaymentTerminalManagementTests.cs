using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Services.Payments;
using Orofoods.Web.Tests.Infrastructure;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Tests.Controllers;

public sealed class AdminPaymentTerminalManagementTests
{
    [Fact]
    public void DriverTerminalAndAssignmentControllers_RequireAdministrator()
    {
        AssertAdministratorOnly(typeof(DriversController));
        AssertAdministratorOnly(typeof(PaymentTerminalsController));
        AssertAdministratorOnly(typeof(DriverPaymentTerminalAssignmentsController));
    }

    [Fact]
    public void DriverAndTerminalForms_ContainOnlyApprovedFields()
    {
        Assert.Equal(new[] { "Id", "Name", "IsActive" }, Names(typeof(DriverEditViewModel)));
        Assert.Equal(new[] { "Id", "Provider", "DeviceId", "StoreId", "PosId", "IsActive" }, Names(typeof(PaymentTerminalEditViewModel)));
        Assert.Equal(new[] { "DriverId", "PaymentTerminalId" }, Names(typeof(DriverPaymentTerminalAssignmentCreateViewModel)));
    }

    [Fact]
    public async Task DriverController_CreatesAndDeactivatesDriver()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var assignments = new DriverPaymentTerminalService(db, TimeProvider.System);
        var controller = new DriversController(db, assignments, TimeProvider.System);

        var created = await controller.Edit(new DriverEditViewModel { Name = "Entrega 1", IsActive = true });
        var driver = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.Drivers);
        var edited = await controller.Edit(new DriverEditViewModel { Id = driver.Id, Name = "Entrega 2", IsActive = true });
        var deactivated = await controller.Deactivate(driver.Id);

        Assert.IsType<RedirectToActionResult>(created);
        Assert.IsType<RedirectToActionResult>(edited);
        Assert.IsType<RedirectToActionResult>(deactivated);
        Assert.Equal("Entrega 2", driver.Name);
        Assert.False(driver.IsActive);
    }

    [Fact]
    public async Task TerminalController_CreatesAndDeactivatesTerminalWithoutExternalLookup()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var assignments = new DriverPaymentTerminalService(db, TimeProvider.System);
        var controller = new PaymentTerminalsController(db, assignments);

        var created = await controller.Edit(new PaymentTerminalEditViewModel
        {
            Provider = PaymentTerminalProvider.MercadoPago,
            DeviceId = "device-test",
            StoreId = "store-test",
            PosId = "pos-test",
            IsActive = true
        });
        var terminal = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.PaymentTerminals);
        var edited = await controller.Edit(new PaymentTerminalEditViewModel
        {
            Id = terminal.Id,
            Provider = PaymentTerminalProvider.MercadoPago,
            DeviceId = "device-test-edited",
            StoreId = "store-test-edited",
            PosId = "pos-test-edited",
            IsActive = true
        });
        var driver = new Orofoods.Web.Models.Delivery.Driver { Name = "Entrega 1" };
        db.Drivers.Add(driver);
        await db.SaveChangesAsync();
        Assert.True((await assignments.AssignAsync(driver.Id, terminal.Id)).Succeeded);
        var historicalEdit = await controller.Edit(new PaymentTerminalEditViewModel
        {
            Id = terminal.Id,
            Provider = PaymentTerminalProvider.MercadoPago,
            DeviceId = "device-test-replaced",
            StoreId = "store-test-replaced",
            PosId = "pos-test-replaced",
            IsActive = true
        });
        var deactivated = await controller.Deactivate(terminal.Id);

        Assert.IsType<RedirectToActionResult>(created);
        Assert.IsType<RedirectToActionResult>(edited);
        Assert.IsType<ViewResult>(historicalEdit);
        Assert.IsType<RedirectToActionResult>(deactivated);
        Assert.False(terminal.IsActive);
        Assert.Equal("device-test-edited", terminal.DeviceId);
        Assert.Equal("store-test-edited", terminal.StoreId);
        Assert.Equal("pos-test-edited", terminal.PosId);
    }

    [Fact]
    public async Task AssignmentController_CreatesEndsAndListsHistory()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var driver = new Orofoods.Web.Models.Delivery.Driver { Name = "Entrega 1" };
        var terminal = new PaymentTerminal { Provider = PaymentTerminalProvider.MercadoPago, DeviceId = "device-test" };
        db.AddRange(driver, terminal);
        await db.SaveChangesAsync();
        var assignmentService = new DriverPaymentTerminalService(db, TimeProvider.System);
        var controller = new DriverPaymentTerminalAssignmentsController(db, assignmentService);

        var created = await controller.Create(new DriverPaymentTerminalAssignmentCreateViewModel
        {
            DriverId = driver.Id,
            PaymentTerminalId = terminal.Id
        });
        var assignment = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.DriverPaymentTerminalAssignments);
        var ended = await controller.End(assignment.Id);
        var history = await controller.Index(driver.Id, terminal.Id);

        Assert.IsType<RedirectToActionResult>(created);
        Assert.IsType<RedirectToActionResult>(ended);
        Assert.IsType<ViewResult>(history);
        Assert.NotNull(assignment.EndedAt);
        Assert.Single(db.DriverPaymentTerminalAssignments);
    }

    private static void AssertAdministratorOnly(Type controllerType)
    {
        var area = controllerType.GetCustomAttribute<AreaAttribute>();
        var authorization = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal("Admin", area?.RouteValue);
        Assert.Equal("Administrador", authorization?.Roles);
    }

    private static string[] Names(Type type) => type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(x => x.Name).ToArray();
}
