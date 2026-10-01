using System.ComponentModel.DataAnnotations;
using Orofoods.Web.Models.Identity;

namespace Orofoods.Web.Models.Payments;

/// <summary>Sanitized operational history for a Card on Delivery Point attempt.</summary>
public sealed class PointPaymentAuditEvent
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public int PaymentId { get; set; }
    public int? DriverId { get; set; }
    public int? PaymentTerminalId { get; set; }
    public int? AssignmentId { get; set; }

    [MaxLength(450)]
    public string? AdminUserId { get; set; }
    public ApplicationUser? AdminUser { get; set; }

    public DateTime OccurredAt { get; set; }
    [MaxLength(100)] public string? ExternalOrderId { get; set; }
    public PaymentStatus? PreviousStatus { get; set; }
    public PaymentStatus NewStatus { get; set; }
    public int AttemptNumber { get; set; }
    [MaxLength(100)] public string ResultCode { get; set; } = "";
    [MaxLength(500)] public string ResultSummary { get; set; } = "";
}
