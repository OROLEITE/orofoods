using System.ComponentModel.DataAnnotations;
using Orofoods.Web.Models.Payments;

namespace Orofoods.Web.ViewModels;

public sealed class DriverEditViewModel
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = "";

    public bool IsActive { get; set; } = true;
}

public sealed class PaymentTerminalEditViewModel
{
    public int Id { get; set; }

    [EnumDataType(typeof(PaymentTerminalProvider))]
    public PaymentTerminalProvider Provider { get; set; }

    [MaxLength(120)] public string? DeviceId { get; set; }
    [MaxLength(120)] public string? StoreId { get; set; }
    [MaxLength(120)] public string? PosId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class DriverPaymentTerminalAssignmentCreateViewModel
{
    [Range(1, int.MaxValue)] public int DriverId { get; set; }
    [Range(1, int.MaxValue)] public int PaymentTerminalId { get; set; }
}
