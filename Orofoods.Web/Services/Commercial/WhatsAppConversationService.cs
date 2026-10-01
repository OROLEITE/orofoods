using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Services.Storage;

namespace Orofoods.Web.Services.Commercial;

public sealed class WhatsAppConversationService(
    ApplicationDbContext db,
    IOptions<WhatsAppBusinessOptions> options,
    UserNotificationService notifications,
    IWhatsAppMediaClient? mediaClient = null,
    IWhatsAppMediaStorage? mediaStorage = null)
{
    private static readonly SemaphoreSlim[] MessageProcessingLocks =
        Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public bool Enabled => options.Value.Enabled;

    public bool IsValidSignature(ReadOnlySpan<byte> body, string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(options.Value.AppSecret)) return false;
        var expected = Encoding.ASCII.GetBytes("sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.Value.AppSecret), body)).ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(expected, Encoding.ASCII.GetBytes(signature));
    }

    public bool IsValidSignature(string body, string? signature) =>
        IsValidSignature(Encoding.UTF8.GetBytes(body), signature);

    public async Task ProcessAsync(JsonDocument document, CancellationToken cancellationToken = default)
    {
        var batch = ValidateAndReadEvents(document.RootElement);

        foreach (var message in batch.Messages)
        {
            var stripe = (StringComparer.Ordinal.GetHashCode(message.ExternalId) & int.MaxValue) % MessageProcessingLocks.Length;
            var gate = MessageProcessingLocks[stripe];
            await gate.WaitAsync(cancellationToken);
            try
            {
                await ProcessMessageAsync(message, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }

        foreach (var status in batch.Statuses)
            await ProcessStatusAsync(status, cancellationToken);
    }

    private WebhookBatch ValidateAndReadEvents(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new WhatsAppWebhookPayloadException();

        if (root.TryGetProperty("object", out var objectNode))
        {
            if (objectNode.ValueKind != JsonValueKind.String)
                throw new WhatsAppWebhookPayloadException();
            if (!string.Equals(objectNode.GetString(), "whatsapp_business_account", StringComparison.Ordinal))
                return WebhookBatch.Empty;
        }

        if (!root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
            throw new WhatsAppWebhookPayloadException();

        var messages = new List<InboundMessage>();
        var statuses = new List<InboundStatus>();
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
                throw new WhatsAppWebhookPayloadException();

            var entryId = ReadOptionalString(entry, "id");
            foreach (var change in changes.EnumerateArray())
            {
                if (change.ValueKind != JsonValueKind.Object || !change.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Object)
                    throw new WhatsAppWebhookPayloadException();

                var hasMessages = value.TryGetProperty("messages", out var messageNodes);
                var hasStatuses = value.TryGetProperty("statuses", out var statusNodes);
                if (!hasMessages && !hasStatuses) continue;

                ValidateSource(entryId, value);

                if (hasMessages)
                {
                    if (messageNodes.ValueKind != JsonValueKind.Array)
                        throw new WhatsAppWebhookPayloadException();
                    foreach (var node in messageNodes.EnumerateArray())
                        messages.Add(ParseMessage(node));
                }

                if (hasStatuses)
                {
                    if (statusNodes.ValueKind != JsonValueKind.Array)
                        throw new WhatsAppWebhookPayloadException();
                    foreach (var node in statusNodes.EnumerateArray())
                    {
                        var parsed = ParseStatus(node);
                        if (parsed is not null) statuses.Add(parsed);
                    }
                }
            }
        }

        return new WebhookBatch(messages, statuses);
    }

    private void ValidateSource(string? entryId, JsonElement value)
    {
        var configuredPhoneNumberId = options.Value.PhoneNumberId;
        if (!string.IsNullOrWhiteSpace(configuredPhoneNumberId))
        {
            var metadataPhoneNumberId = value.TryGetProperty("metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object
                ? ReadOptionalString(metadata, "phone_number_id")
                : null;
            if (!string.Equals(metadataPhoneNumberId, configuredPhoneNumberId, StringComparison.Ordinal))
                throw new WhatsAppWebhookSourceMismatchException();
        }

        var configuredBusinessAccountId = options.Value.BusinessAccountId;
        if (!string.IsNullOrWhiteSpace(configuredBusinessAccountId) &&
            !string.Equals(entryId, configuredBusinessAccountId, StringComparison.Ordinal))
            throw new WhatsAppWebhookSourceMismatchException();
    }

    private static InboundMessage ParseMessage(JsonElement message)
    {
        if (message.ValueKind != JsonValueKind.Object)
            throw new WhatsAppWebhookPayloadException();

        var id = ReadRequiredString(message, "id");
        var from = ReadRequiredString(message, "from");
        var type = ReadRequiredString(message, "type");
        string? text = null;
        InboundMedia? media = null;
        if (string.Equals(type, "text", StringComparison.OrdinalIgnoreCase))
        {
            if (!message.TryGetProperty("text", out var textNode) || textNode.ValueKind != JsonValueKind.Object)
                throw new WhatsAppWebhookPayloadException();
            text = ReadRequiredString(textNode, "body");
            if (text.Length > 4096) throw new WhatsAppWebhookPayloadException();
        }
        else if (type is "image" or "audio" or "video" or "document" or "sticker")
        {
            if (!message.TryGetProperty(type, out var mediaNode) || mediaNode.ValueKind != JsonValueKind.Object)
                throw new WhatsAppWebhookPayloadException();
            var mediaId = ReadRequiredString(mediaNode, "id");
            var mimeType = ReadRequiredString(mediaNode, "mime_type");
            var fileName = type == "document" ? ReadOptionalString(mediaNode, "filename") : null;
            var caption = type is "image" or "video" or "document" ? ReadOptionalString(mediaNode, "caption") : null;
            if (caption?.Length > 4096 || fileName?.Length > 255 || mediaId.Length > 255 || mimeType.Length > 127)
                throw new WhatsAppWebhookPayloadException();
            var isVoice = mediaNode.TryGetProperty("voice", out var voiceNode) && voiceNode.ValueKind == JsonValueKind.True;
            media = new InboundMedia(mediaId, mimeType, fileName, caption, isVoice);
        }

        return new InboundMessage(id, from, type, text, media);
    }

    private static InboundStatus? ParseStatus(JsonElement status)
    {
        if (status.ValueKind != JsonValueKind.Object)
            throw new WhatsAppWebhookPayloadException();

        var id = ReadRequiredString(status, "id");
        var statusName = ReadRequiredString(status, "status");
        var mappedStatus = statusName switch
        {
            "sent" => WhatsAppMessageStatus.Sent,
            "delivered" => WhatsAppMessageStatus.Delivered,
            "read" => WhatsAppMessageStatus.Read,
            "failed" => WhatsAppMessageStatus.Failed,
            _ => (WhatsAppMessageStatus?)null
        };
        if (mappedStatus is null) return null;

        DateTime? occurredAt = null;
        if (status.TryGetProperty("timestamp", out var timestampNode) && timestampNode.ValueKind == JsonValueKind.String &&
            long.TryParse(timestampNode.GetString(), out var seconds))
        {
            try { occurredAt = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime; }
            catch (ArgumentOutOfRangeException) { throw new WhatsAppWebhookPayloadException(); }
        }

        return new InboundStatus(id, mappedStatus.Value, occurredAt ?? DateTime.UtcNow);
    }

    private static string ReadRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
            throw new WhatsAppWebhookPayloadException();
        return property.GetString()!;
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)) return null;
        if (property.ValueKind != JsonValueKind.String) throw new WhatsAppWebhookPayloadException();
        return property.GetString();
    }

    private async Task ProcessMessageAsync(InboundMessage message, CancellationToken cancellationToken)
    {
        if (await db.WhatsAppMessages.AsNoTracking().AnyAsync(x => x.ExternalMessageId == message.ExternalId, cancellationToken)) return;

        var normalizedPhone = TryNormalizePhone(message.From);
        var conversationPhone = normalizedPhone ?? ExtractAsciiDigits(message.From);
        if (conversationPhone.Length is 0 or > 30) return;

        var candidateCustomers = await db.Customers.AsNoTracking()
            .Select(x => new CustomerPhoneCandidate(x.Id, x.WhatsApp, x.Phone, x.InternalSalesUserId))
            .ToListAsync(cancellationToken);
        List<CustomerPhoneCandidate> matches = normalizedPhone is null
            ? []
            : candidateCustomers
                .Where(x => TryNormalizePhone(x.WhatsApp) == normalizedPhone || TryNormalizePhone(x.Phone) == normalizedPhone)
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .Take(2)
                .ToList();
        var customer = matches.Count == 1 ? matches[0] : null;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (await db.WhatsAppMessages.AsNoTracking().AnyAsync(x => x.ExternalMessageId == message.ExternalId, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return;
            }

            var conversation = await db.WhatsAppConversations.SingleOrDefaultAsync(x => x.PhoneNumber == conversationPhone, cancellationToken);
            if (conversation is null)
            {
                conversation = new WhatsAppConversation
                {
                    PhoneNumber = conversationPhone,
                    CustomerId = customer?.Id,
                    AssignedUserId = customer?.InternalSalesUserId,
                    Status = WhatsAppConversationStatus.Pending,
                    LastMessageAt = DateTime.UtcNow,
                    LastInboundAt = DateTime.UtcNow,
                    UnreadCount = 1
                };
                db.WhatsAppConversations.Add(conversation);
            }
            else
            {
                if (conversation.Status == WhatsAppConversationStatus.Closed)
                    conversation.Status = WhatsAppConversationStatus.Pending;
                conversation.CustomerId ??= customer?.Id;
                conversation.AssignedUserId ??= customer?.InternalSalesUserId;
                conversation.LastMessageAt = DateTime.UtcNow;
                conversation.LastInboundAt = DateTime.UtcNow;
                conversation.UnreadCount++;
            }

            var messageType = Enum.TryParse<WhatsAppMessageType>(message.Type, true, out var parsed)
                ? parsed
                : WhatsAppMessageType.Unsupported;
            var mediaState = message.Media is null
                ? WhatsAppMediaState.None
                : WhatsAppMediaPolicy.IsAllowed(messageType, message.Media.MimeType)
                    ? WhatsAppMediaState.Pending
                    : WhatsAppMediaState.Rejected;
            var persistedMessage = new WhatsAppMessage
            {
                Conversation = conversation,
                ExternalMessageId = message.ExternalId,
                Direction = WhatsAppMessageDirection.Inbound,
                Type = messageType,
                TextBody = messageType == WhatsAppMessageType.Text ? message.Text : "Tipo de mensagem ainda não suportado.",
                MediaId = message.Media?.MediaId,
                MimeType = message.Media is null ? null : WhatsAppMediaPolicy.NormalizeMimeType(message.Media.MimeType),
                FileName = message.Media is null ? null : WhatsAppMediaPolicy.SafeDisplayFileName(message.Media.FileName, message.Media.MimeType),
                Caption = message.Media?.Caption,
                IsVoiceMessage = message.Media?.IsVoice ?? false,
                MediaState = mediaState,
                Status = WhatsAppMessageStatus.Sent,
                CreatedAt = DateTime.UtcNow
            };
            if (message.Media is not null)
                persistedMessage.TextBody = mediaState == WhatsAppMediaState.Rejected
                    ? "Mídia não suportada ou inválida."
                    : null;
            db.WhatsAppMessages.Add(persistedMessage);
            await db.SaveChangesAsync(cancellationToken);

            if (conversation.AssignedUserId is not null)
                await notifications.CreateOnceAsync(new UserNotification
                {
                    UserId = conversation.AssignedUserId,
                    CustomerId = conversation.CustomerId,
                    Type = UserNotificationType.WhatsAppMessageReceived,
                    Title = "Nova mensagem WhatsApp",
                    Message = messageType == WhatsAppMessageType.Text ? message.Text! : "Nova mensagem recebida.",
                    ActionUrl = "/Admin/WhatsApp",
                    DeduplicationKey = $"WHATSAPP_MESSAGE:{message.ExternalId}"
                }, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            if (persistedMessage.MediaState == WhatsAppMediaState.Pending)
                await DownloadMediaAsync(persistedMessage, cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            await transaction.DisposeAsync();
            db.ChangeTracker.Clear();

            // The unique database index is authoritative across application instances.
            if (await db.WhatsAppMessages.AsNoTracking().AnyAsync(x => x.ExternalMessageId == message.ExternalId, cancellationToken)) return;
            throw;
        }
    }

    private async Task DownloadMediaAsync(WhatsAppMessage message, CancellationToken cancellationToken)
    {
        try
        {
            if (mediaClient is null || mediaStorage is null || string.IsNullOrWhiteSpace(message.MediaId) || string.IsNullOrWhiteSpace(message.MimeType))
            {
                await SetMediaFailureAsync(message.Id, WhatsAppMediaState.Failed, cancellationToken);
                return;
            }

            using var download = await mediaClient.DownloadAsync(
                message.MediaId,
                message.MimeType,
                options.Value.MaxInboundMediaBytes,
                cancellationToken);
            if (!download.Succeeded || download.Content is null || string.IsNullOrWhiteSpace(download.MimeType) ||
                !WhatsAppMediaPolicy.IsAllowed(message.Type, download.MimeType))
            {
                var state = download.ErrorCode is "MIME_MISMATCH" or "SIZE_REJECTED" ? WhatsAppMediaState.Rejected : WhatsAppMediaState.Failed;
                await SetMediaFailureAsync(message.Id, state, cancellationToken);
                return;
            }

            var fileName = WhatsAppMediaPolicy.SafeDisplayFileName(message.FileName, download.MimeType);
            var reference = await mediaStorage.SaveAsync(download.Content, fileName, download.MimeType, cancellationToken);
            await db.WhatsAppMessages.Where(x => x.Id == message.Id && x.MediaState == WhatsAppMediaState.Pending)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.MediaStorageReference, reference)
                    .SetProperty(x => x.MimeType, download.MimeType)
                    .SetProperty(x => x.FileName, fileName)
                    .SetProperty(x => x.MediaSizeBytes, download.SizeBytes)
                    .SetProperty(x => x.MediaState, WhatsAppMediaState.Available), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            await SetMediaFailureAsync(message.Id, WhatsAppMediaState.Failed, cancellationToken);
        }
    }

    private Task SetMediaFailureAsync(long messageId, WhatsAppMediaState state, CancellationToken cancellationToken) =>
        db.WhatsAppMessages.Where(x => x.Id == messageId && x.MediaState == WhatsAppMediaState.Pending)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.MediaState, state), cancellationToken);

    private async Task ProcessStatusAsync(InboundStatus status, CancellationToken cancellationToken)
    {
        var messages = db.WhatsAppMessages.Where(x => x.ExternalMessageId == status.ExternalId);
        if (status.Status == WhatsAppMessageStatus.Failed)
        {
            await messages.Where(x => x.Status != WhatsAppMessageStatus.Failed)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, WhatsAppMessageStatus.Failed)
                    .SetProperty(x => x.FailedAt, status.OccurredAt), cancellationToken);
            return;
        }

        var update = messages.Where(x => x.Status < status.Status);
        switch (status.Status)
        {
            case WhatsAppMessageStatus.Sent:
                await update.ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, status.Status)
                    .SetProperty(x => x.SentAt, status.OccurredAt), cancellationToken);
                break;
            case WhatsAppMessageStatus.Delivered:
                await update.ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, status.Status)
                    .SetProperty(x => x.DeliveredAt, status.OccurredAt), cancellationToken);
                break;
            case WhatsAppMessageStatus.Read:
                await update.ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, status.Status)
                    .SetProperty(x => x.ReadAt, status.OccurredAt), cancellationToken);
                break;
        }
    }

    public static string? TryNormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var input = value.Trim();
        var hasInternationalPrefix = input.StartsWith('+');
        if (input.Count(character => character == '+') > (hasInternationalPrefix ? 1 : 0)) return null;
        if (input.Any(character => !char.IsAsciiDigit(character) && !char.IsWhiteSpace(character) && character is not '+' and not '(' and not ')' and not '-' and not '.'))
            return null;

        var digits = ExtractAsciiDigits(input);
        if (!hasInternationalPrefix && digits.StartsWith('0') && digits.Length is 11 or 12)
            digits = digits[1..];
        if (digits.Length is < 8 or > 15 || digits[0] == '0') return null;
        if (hasInternationalPrefix) return digits;
        if (digits.StartsWith("55", StringComparison.Ordinal) && digits.Length is 12 or 13) return digits;

        // In this Brazilian deployment, a national number is accepted only when it contains its two-digit DDD.
        return digits.Length is 10 or 11 ? "55" + digits : null;
    }

    private static string ExtractAsciiDigits(string value) => new(value.Where(char.IsAsciiDigit).ToArray());

    private sealed record CustomerPhoneCandidate(int Id, string WhatsApp, string Phone, string? InternalSalesUserId);
    private sealed record InboundMessage(string ExternalId, string From, string Type, string? Text, InboundMedia? Media);
    private sealed record InboundMedia(string MediaId, string MimeType, string? FileName, string? Caption, bool IsVoice);
    private sealed record InboundStatus(string ExternalId, WhatsAppMessageStatus Status, DateTime OccurredAt);
    private sealed record WebhookBatch(IReadOnlyList<InboundMessage> Messages, IReadOnlyList<InboundStatus> Statuses)
    {
        public static WebhookBatch Empty { get; } = new([], []);
    }
}

public sealed class WhatsAppWebhookPayloadException() : Exception("Webhook payload has an invalid structure.");
public sealed class WhatsAppWebhookSourceMismatchException() : Exception("Webhook source identifiers do not match configuration.");
