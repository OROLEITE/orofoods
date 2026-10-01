using System.ComponentModel.DataAnnotations;

namespace Orofoods.Web.Models.Delivery;

public sealed class Driver
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<DriverPaymentTerminalAssignment> PaymentTerminalAssignments { get; set; } = [];
}
