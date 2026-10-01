using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Storage;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Administrador,Vendedor,GerenteComercial")]
public class WhatsAppController(
    ApplicationDbContext db,
    SalesRepresentativeAccessService accessService,
    IWhatsAppBusinessGateway gateway,
    IWhatsAppMediaStorage? mediaStorage = null) : Controller
{
    public async Task<IActionResult> Index(long? id, CancellationToken cancellationToken)
    {
        var scope = await accessService.GetScopeAsync(User, cancellationToken);
        var customerIds = accessService.ApplyCustomerScope(db.Customers.AsNoTracking(), scope).Select(x => x.Id);
        IQueryable<WhatsAppConversation> conversationsQuery = db.WhatsAppConversations.AsNoTracking()
            .Include(x => x.Customer).ThenInclude(x => x!.SalesRepresentative)
            .Include(x => x.AssignedUser);
        if (scope.IsRestricted)
        {
            conversationsQuery = conversationsQuery.Where(x => x.AssignedUserId == scope.UserId || (x.CustomerId.HasValue && customerIds.Contains(x.CustomerId.Value)));
        }

        var conversations = await conversationsQuery.OrderByDescending(x => x.LastMessageAt).Take(50).ToListAsync(cancellationToken);
        var selected = id.HasValue ? conversations.FirstOrDefault(x => x.Id == id) : conversations.FirstOrDefault();
        if (selected is not null && selected.UnreadCount > 0 && await accessService.CanAccessConversationAsync(User, selected.Id, cancellationToken))
        {
            var trackedConversation = await db.WhatsAppConversations.SingleAsync(x => x.Id == selected.Id, cancellationToken);
            trackedConversation.UnreadCount = 0;
            await db.SaveChangesAsync(cancellationToken);
            selected.UnreadCount = 0;
        }

        var messages = selected is null ? [] : await db.WhatsAppMessages.AsNoTracking().Where(x => x.ConversationId == selected.Id).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(100).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var latestMessages = await LoadLatestMessagesAsync(conversations.Select(x => x.Id), cancellationToken);
        return View(new WhatsAppInboxViewModel(conversations, selected, messages, latestMessages));
    }

    [HttpGet]
    public async Task<IActionResult> Updates(long? conversationId, CancellationToken cancellationToken)
    {
        var scope = await accessService.GetScopeAsync(User, cancellationToken);
        var customerIds = accessService.ApplyCustomerScope(db.Customers.AsNoTracking(), scope).Select(x => x.Id);
        IQueryable<WhatsAppConversation> query = db.WhatsAppConversations.AsNoTracking().Include(x => x.Customer);
        if (scope.IsRestricted)
            query = query.Where(x => x.AssignedUserId == scope.UserId || (x.CustomerId.HasValue && customerIds.Contains(x.CustomerId.Value)));

        var conversations = await query.OrderByDescending(x => x.LastMessageAt).Take(50).ToListAsync(cancellationToken);
        var latest = await LoadLatestMessagesAsync(conversations.Select(x => x.Id), cancellationToken);
        IReadOnlyList<WhatsAppMessage> messages = [];
        if (conversationId.HasValue && conversations.Any(x => x.Id == conversationId.Value) &&
            await accessService.CanAccessConversationAsync(User, conversationId.Value, cancellationToken))
        {
            messages = await db.WhatsAppMessages.AsNoTracking()
                .Where(x => x.ConversationId == conversationId.Value)
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(100)
                .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);
        }

        return Json(new
        {
            conversations = conversations.Select(item =>
            {
                latest.TryGetValue(item.Id, out var lastMessage);
                return new
                {
                    item.Id,
                    name = item.Customer?.TradeName ?? item.PhoneNumber,
                    initials = Initials(item.Customer?.TradeName),
                    identified = item.CustomerId.HasValue,
                    preview = Preview(lastMessage),
                    messageType = lastMessage?.Type.ToString().ToLowerInvariant(),
                    lastMessageAt = item.LastMessageAt,
                    item.UnreadCount,
                    status = item.Status.ToString().ToLowerInvariant()
                };
            }),
            messages = messages.Select(MessagePayload)
        });
    }

    [HttpGet("/Admin/WhatsApp/media/{messageId:long}")]
    public async Task<IActionResult> Media(long messageId, CancellationToken cancellationToken)
    {
        if (mediaStorage is null) return NotFound();
        var message = await db.WhatsAppMessages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == messageId, cancellationToken);
        if (message is null || message.MediaState != WhatsAppMediaState.Available || string.IsNullOrWhiteSpace(message.MediaStorageReference) ||
            !await accessService.CanAccessConversationAsync(User, message.ConversationId, cancellationToken)) return NotFound();

        var media = await mediaStorage.OpenReadAsync(message.MediaStorageReference, cancellationToken);
        if (media is null || !string.Equals(WhatsAppMediaPolicy.NormalizeMimeType(media.ContentType), WhatsAppMediaPolicy.NormalizeMimeType(message.MimeType), StringComparison.OrdinalIgnoreCase))
        {
            media?.Content.Dispose();
            return NotFound();
        }

        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private, max-age=300, must-revalidate";
        var download = message.Type == WhatsAppMessageType.Document;
        return download
            ? File(media.Content, media.ContentType, message.FileName ?? media.FileName, enableRangeProcessing: true)
            : File(media.Content, media.ContentType, enableRangeProcessing: true);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(long conversationId, string text, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var conversation = await db.WhatsAppConversations.SingleOrDefaultAsync(x => x.Id == conversationId, cancellationToken);
        if (conversation is null || userId is null || !await accessService.CanAccessConversationAsync(User, conversationId, cancellationToken)) return Forbid();
        if (conversation.Status == WhatsAppConversationStatus.Closed)
        {
            TempData["WhatsAppError"] = "Conversa fechada. Reabra ou abra uma nova conversa antes de enviar.";
            return RedirectToAction(nameof(Index), new { id = conversationId });
        }

        var cleanedText = text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(cleanedText))
        {
            TempData["WhatsAppError"] = "Mensagem vazia.";
            return RedirectToAction(nameof(Index), new { id = conversationId });
        }

        var result = await gateway.SendTextAsync(conversation.PhoneNumber, cleanedText, cancellationToken);
        if (!result.Succeeded) { TempData["WhatsAppError"] = result.ErrorMessage; return RedirectToAction(nameof(Index), new { id = conversationId }); }
        db.WhatsAppMessages.Add(new WhatsAppMessage { ConversationId = conversationId, ExternalMessageId = result.ExternalMessageId, Direction = WhatsAppMessageDirection.Outbound, Type = WhatsAppMessageType.Text, TextBody = cleanedText, Status = WhatsAppMessageStatus.Sent, SentAt = DateTime.UtcNow });
        conversation.LastOutboundAt = DateTime.UtcNow; conversation.LastMessageAt = DateTime.UtcNow; conversation.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index), new { id = conversationId });
    }

    private async Task<IReadOnlyDictionary<long, WhatsAppMessage>> LoadLatestMessagesAsync(IEnumerable<long> conversationIds, CancellationToken cancellationToken)
    {
        var ids = conversationIds.ToArray();
        if (ids.Length == 0) return new Dictionary<long, WhatsAppMessage>();
        var messages = await db.WhatsAppMessages.AsNoTracking().Where(x => ids.Contains(x.ConversationId))
            .GroupBy(x => x.ConversationId)
            .Select(group => group.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).First())
            .ToListAsync(cancellationToken);
        return messages.ToDictionary(x => x.ConversationId);
    }

    private static object MessagePayload(WhatsAppMessage message) => new
    {
        message.Id,
        message.ConversationId,
        direction = message.Direction.ToString().ToLowerInvariant(),
        type = message.Type.ToString().ToLowerInvariant(),
        message.TextBody,
        message.Caption,
        message.FileName,
        message.MediaSizeBytes,
        message.IsVoiceMessage,
        mediaState = message.MediaState.ToString().ToLowerInvariant(),
        mediaUrl = message.MediaState == WhatsAppMediaState.Available ? $"/Admin/WhatsApp/media/{message.Id}" : null,
        status = message.Status.ToString().ToLowerInvariant(),
        statusLabel = StatusLabel(message.Status),
        message.CreatedAt
    };

    private static string Preview(WhatsAppMessage? message) => message switch
    {
        null => "Sem mensagens",
        { Type: WhatsAppMessageType.Text } => message.TextBody ?? "Mensagem",
        { Type: WhatsAppMessageType.Image } => "Imagem",
        { Type: WhatsAppMessageType.Audio, IsVoiceMessage: true } => "Mensagem de voz",
        { Type: WhatsAppMessageType.Audio } => "Áudio",
        { Type: WhatsAppMessageType.Video } => "Vídeo",
        { Type: WhatsAppMessageType.Document } => message.FileName ?? "Documento",
        { Type: WhatsAppMessageType.Sticker } => "Figurinha",
        _ => "Tipo de mensagem ainda não suportado"
    };

    private static string StatusLabel(WhatsAppMessageStatus status) => status switch
    {
        WhatsAppMessageStatus.Pending => "Pendente",
        WhatsAppMessageStatus.Sent => "Enviada",
        WhatsAppMessageStatus.Delivered => "Entregue",
        WhatsAppMessageStatus.Read => "Lida",
        WhatsAppMessageStatus.Failed => "Falha no envio",
        _ => "Recebida"
    };

    private static string Initials(string? name) => string.Concat((name ?? string.Empty)
        .Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(part => char.ToUpperInvariant(part[0])));
}

public sealed record WhatsAppInboxViewModel(
    IReadOnlyList<WhatsAppConversation> Conversations,
    WhatsAppConversation? Selected,
    IReadOnlyList<WhatsAppMessage> Messages,
    IReadOnlyDictionary<long, WhatsAppMessage> LatestMessages);
