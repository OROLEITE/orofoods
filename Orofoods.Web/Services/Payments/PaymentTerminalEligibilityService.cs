using Orofoods.Web.Models.Payments;

namespace Orofoods.Web.Services.Payments;

public sealed class PaymentTerminalEligibilityService : IPaymentTerminalEligibilityService
{
    public bool CanBeUsedForPointPayment(DriverPaymentTerminalAssignment assignment) =>
        assignment.EndedAt is null
        && assignment.Driver?.IsActive == true
        && assignment.PaymentTerminal?.IsActive == true
        && assignment.PaymentTerminal.Provider == PaymentTerminalProvider.MercadoPago
        && !string.IsNullOrWhiteSpace(assignment.PaymentTerminal.DeviceId);
}
