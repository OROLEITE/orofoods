using System.ComponentModel.DataAnnotations;
using Orofoods.Web.Models.Identity;

namespace Orofoods.Web.Models.Commercial;

public enum WhatsAppConversationStatus { Open, Pending, Closed }
public enum WhatsAppMessageDirection { Inbound, Outbound }
public enum WhatsAppMessageType { Text, Image, Audio, Document, Video, Location, Contact, Sticker, Unsupported }
public enum WhatsAppMessageStatus { Pending, Sent, Delivered, Read, Failed }

public class WhatsAppConversation
{
    public long Id { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    [MaxLength(30)] public string PhoneNumber { get; set; } = "";
    public string? AssignedUserId { get; set; }
    public ApplicationUser? AssignedUser { get; set; }
    public WhatsAppConversationStatus Status { get; set; } = WhatsAppConversationStatus.Open;
    public DateTime? LastMessageAt { get; set; }
    public DateTime? LastInboundAt { get; set; }
    public DateTime? LastOutboundAt { get; set; }
    public int UnreadCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<WhatsAppMessage> Messages { get; set; } = [];
}

public class WhatsAppMessage
{
    public long Id { get; set; }
    public long ConversationId { get; set; }
    public WhatsAppConversation Conversation { get; set; } = null!;
    [MaxLength(255)] public string? ExternalMessageId { get; set; }
    public WhatsAppMessageDirection Direction { get; set; }
    public WhatsAppMessageType Type { get; set; }
    [MaxLength(4096)] public string? TextBody { get; set; }
    public WhatsAppMessageStatus Status { get; set; } = WhatsAppMessageStatus.Pending;
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? FailedAt { get; set; }
    [MaxLength(80)] public string? ErrorCode { get; set; }
    [MaxLength(500)] public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}