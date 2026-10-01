using System.ComponentModel.DataAnnotations;
using Orofoods.Web.Models.Delivery;

namespace Orofoods.Web.Models.Payments;

public sealed class PaymentTerminal
{
    public int Id { get; set; }

    public PaymentTerminalProvider Provider { get; set; }

    [MaxLength(120)]
    public string? DeviceId { get; set; }

    [MaxLength(120)]
    public string? StoreId { get; set; }

    [MaxLength(120)]
    public string? PosId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<DriverPaymentTerminalAssignment> DriverAssignments { get; set; } = [];
}
