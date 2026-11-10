using System.ComponentModel.DataAnnotations;

namespace Orofoods.Web.Models.Commercial;

public enum WhatsAppWebhookDeliveryStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Blocked
}

public sealed class WhatsAppWebhookDelivery
{
    public long Id { get; set; }

    [MaxLength(64)]
    public string PayloadHash { get; set; } = "";

    public byte[]? ProtectedPayload { get; set; }
    public WhatsAppWebhookDeliveryStatus Status { get; set; } = WhatsAppWebhookDeliveryStatus.Pending;
    public int Attempts { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
    public DateTime? ProcessedAt { get; set; }

    [MaxLength(80)]
    public string? LastErrorCode { get; set; }
}
