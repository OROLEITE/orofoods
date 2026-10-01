using Orofoods.Web.Models.Delivery;

namespace Orofoods.Web.Models.Payments;

public sealed class DriverPaymentTerminalAssignment
{
    public int Id { get; set; }

    public int DriverId { get; set; }
    public Driver? Driver { get; set; }

    public int PaymentTerminalId { get; set; }
    public PaymentTerminal? PaymentTerminal { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
