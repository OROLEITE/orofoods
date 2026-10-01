using Orofoods.Web.Models.Payments;

namespace Orofoods.Web.Services.Payments;

public interface IPaymentTerminalEligibilityService
{
    bool CanBeUsedForPointPayment(DriverPaymentTerminalAssignment assignment);
}
