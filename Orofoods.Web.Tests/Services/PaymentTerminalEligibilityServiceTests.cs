using Orofoods.Web.Models.Delivery;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Services.Payments;

namespace Orofoods.Web.Tests.Services;

public sealed class PaymentTerminalEligibilityServiceTests
{
    [Fact]
    public void CanBeUsedForPointPayment_RequiresActiveDriverTerminalAssignmentAndDeviceId()
    {
        var sut = new PaymentTerminalEligibilityService();
        var driver = new Driver { Name = "Motorista" };
        var terminal = new PaymentTerminal { Provider = PaymentTerminalProvider.MercadoPago, DeviceId = "device-1" };
        var assignment = new DriverPaymentTerminalAssignment
        {
            Driver = driver,
            PaymentTerminal = terminal,
            StartedAt = DateTime.UtcNow
        };

        Assert.True(sut.CanBeUsedForPointPayment(assignment));

        driver.IsActive = false;
        Assert.False(sut.CanBeUsedForPointPayment(assignment));
        driver.IsActive = true;
        terminal.IsActive = false;
        Assert.False(sut.CanBeUsedForPointPayment(assignment));
        terminal.IsActive = true;
        terminal.DeviceId = "  ";
        Assert.False(sut.CanBeUsedForPointPayment(assignment));
        terminal.DeviceId = "device-1";
        assignment.EndedAt = DateTime.UtcNow;
        Assert.False(sut.CanBeUsedForPointPayment(assignment));
    }

    [Fact]
    public void CanBeUsedForPointPayment_RequiresMercadoPagoProvider()
    {
        var sut = new PaymentTerminalEligibilityService();
        var assignment = new DriverPaymentTerminalAssignment
        {
            Driver = new Driver { Name = "Motorista" },
            PaymentTerminal = new PaymentTerminal
            {
                Provider = (PaymentTerminalProvider)100,
                DeviceId = "device-1"
            },
            StartedAt = DateTime.UtcNow
        };

        Assert.False(sut.CanBeUsedForPointPayment(assignment));
    }
}
