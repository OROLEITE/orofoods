using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Commercial;

namespace Orofoods.Web.Services.Commercial;

public sealed class WhatsAppConversationService(ApplicationDbContext db, IOptions<WhatsAppBusinessOptions> options, UserNotificationService notifications)
{
    public bool Enabled => options.Value.Enabled;

    public bool IsValidSignature(string body, string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(options.Value.AppSecret)) return false;
        var expected = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.Value.AppSecret), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature));
    }

    public async Task ProcessAsync(JsonDocument document, CancellationToken cancellationToken = default)
    {
        foreach (var entry in document.RootElement.GetProperty("entry").EnumerateArray())
        foreach (var change in entry.GetProperty("changes").EnumerateArray())
        {
            var value = change.GetProperty("value");
            if (value.TryGetProperty("messages", out var messages))
                foreach (var message in messages.EnumerateArray()) await ProcessMessageAsync(value, message, cancellationToken);
            if (value.TryGetProperty("statuses", out var statuses))
                foreach (var status in statuses.EnumerateArray()) await ProcessStatusAsync(status, cancellationToken);
        }
    }

    private async Task ProcessMessageAsync(JsonElement value, JsonElement message, CancellationToken cancellationToken)
    {
        var externalId = message.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(externalId) || await db.WhatsAppMessages.AnyAsync(x => x.ExternalMessageId == externalId, cancellationToken)) return;
        var phone = Normalize(message.GetProperty("from").GetString() ?? "");
        var matches = await db.Customers.AsNoTracking().Where(x => x.WhatsApp == phone || x.Phone == phone).Take(2).ToListAsync(cancellationToken);
        var customer = matches.Count == 1 ? matches[0] : null;
        var conversation = await db.WhatsAppConversations.SingleOrDefaultAsync(x => x.PhoneNumber == phone, cancellationToken);
        if (conversation is null)
        {
            conversation = new WhatsAppConversation { PhoneNumber = phone, CustomerId = customer?.Id, AssignedUserId = customer?.InternalSalesUserId, LastMessageAt = DateTime.UtcNow, LastInboundAt = DateTime.UtcNow, UnreadCount = 1 };
            db.WhatsAppConversations.Add(conversation);
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            conversation.Status = WhatsAppConversationStatus.Open;
            conversation.CustomerId ??= customer?.Id;
            conversation.AssignedUserId ??= customer?.InternalSalesUserId;
            conversation.LastMessageAt = DateTime.UtcNow;
            conversation.LastInboundAt = DateTime.UtcNow;
            conversation.UnreadCount++;
        }
        var type = message.GetProperty("type").GetString() ?? "unsupported";
        var text = type == "text" && message.TryGetProperty("text", out var textNode) ? textNode.GetProperty("body").GetString() : "Tipo de mensagem ainda não suportado.";
        db.WhatsAppMessages.Add(new WhatsAppMessage { ConversationId = conversation.Id, ExternalMessageId = externalId, Direction = WhatsAppMessageDirection.Inbound, Type = Enum.TryParse<WhatsAppMessageType>(type, true, out var parsed) ? parsed : WhatsAppMessageType.Unsupported, TextBody = text, Status = WhatsAppMessageStatus.Sent, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(cancellationToken);
        if (conversation.AssignedUserId is not null)
            await notifications.CreateOnceAsync(new UserNotification { UserId = conversation.AssignedUserId, CustomerId = conversation.CustomerId, Type = UserNotificationType.WhatsAppMessageReceived, Title = "Nova mensagem WhatsApp", Message = text ?? "Nova mensagem recebida.", ActionUrl = "/Admin/WhatsApp", DeduplicationKey = $"WHATSAPP_MESSAGE:{externalId}" }, cancellationToken);
    }

    private async Task ProcessStatusAsync(JsonElement status, CancellationToken cancellationToken)
    {
        var externalId = status.GetProperty("id").GetString();
        var message = await db.WhatsAppMessages.SingleOrDefaultAsync(x => x.ExternalMessageId == externalId, cancellationToken);
        if (message is null) return;
        var next = status.GetProperty("status").GetString() switch { "sent" => WhatsAppMessageStatus.Sent, "delivered" => WhatsAppMessageStatus.Delivered, "read" => WhatsAppMessageStatus.Read, "failed" => WhatsAppMessageStatus.Failed, _ => message.Status };
        if (next < message.Status && next != WhatsAppMessageStatus.Failed) return;
        message.Status = next;
        var timestamp = status.TryGetProperty("timestamp", out var node) && long.TryParse(node.GetString(), out var seconds) ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime : DateTime.UtcNow;
        if (next == WhatsAppMessageStatus.Sent) message.SentAt = timestamp;
        if (next == WhatsAppMessageStatus.Delivered) message.DeliveredAt = timestamp;
        if (next == WhatsAppMessageStatus.Read) message.ReadAt = timestamp;
        if (next == WhatsAppMessageStatus.Failed) message.FailedAt = timestamp;
        await db.SaveChangesAsync(cancellationToken);
    }

    public static string Normalize(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray()).TrimStart('0');
        return digits.StartsWith("55", StringComparison.Ordinal) ? digits : "55" + digits;
    }
}
