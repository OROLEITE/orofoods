using System.Security.Claims;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Storage;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = ApplicationRoles.Administrator + "," + ApplicationRoles.Seller + "," + ApplicationRoles.CommercialManager + "," + ApplicationRoles.Operator)]
public class WhatsAppController(
    ApplicationDbContext db,
    SalesRepresentativeAccessService accessService,
    IWhatsAppBusinessGateway gateway,
    IWhatsAppMediaStorage? mediaStorage = null) : Controller
{
    public async Task<IActionResult> Index(
        long? id,
        CancellationToken cancellationToken,
        bool markAsRead = true,
        bool selectConversation = true,
        int? customerId = null)
    {
        var scope = await accessService.GetScopeAsync(User, cancellationToken);
        var customerIds = accessService.ApplyCustomerScope(db.Customers.AsNoTracking(), scope).Select(x => x.Id);
        IQueryable<WhatsAppConversation> conversationsQuery = db.WhatsAppConversations.AsNoTracking()
            .Include(x => x.Customer).ThenInclude(x => x!.SalesRepresentative)
            .Include(x => x.AssignedUser);
        if (scope.IsRestricted && !User.IsInRole(ApplicationRoles.Operator))
        {
            conversationsQuery = conversationsQuery.Where(x => x.AssignedUserId == scope.UserId || (x.CustomerId.HasValue && customerIds.Contains(x.CustomerId.Value)));
        }

        var conversations = await conversationsQuery.OrderByDescending(x => x.LastMessageAt).Take(50).ToListAsync(cancellationToken);
        WhatsAppConversation? selected = null;
        if (id.HasValue)
        {
            selected = conversations.FirstOrDefault(x => x.Id == id.Value);
            if (selected is null)
            {
                selected = await conversationsQuery.SingleOrDefaultAsync(x => x.Id == id.Value, cancellationToken);
                if (selected is null && customerId.HasValue)
                {
                    var scopedCustomer = await accessService
                        .ApplyCustomerScope(db.Customers.AsNoTracking(), scope)
                        .Where(customer => customer.Id == customerId.Value)
                        .Select(customer => new { customer.Id, customer.WhatsApp, customer.Phone })
                        .SingleOrDefaultAsync(cancellationToken);
                    if (scopedCustomer is not null)
                    {
                        var normalizedPhones = new[]
                        {
                            WhatsAppConversationService.TryNormalizePhone(scopedCustomer.WhatsApp),
                            WhatsAppConversationService.TryNormalizePhone(scopedCustomer.Phone)
                        }
                        .Where(value => value is not null)
                        .Select(value => value!)
                        .Distinct(StringComparer.Ordinal)
                        .ToHashSet(StringComparer.Ordinal);
                        if (normalizedPhones.Count > 0)
                        {
                            selected = await db.WhatsAppConversations
                                .AsNoTracking()
                                .Where(conversation => conversation.Id == id.Value &&
                                    (conversation.CustomerId == scopedCustomer.Id ||
                                     (conversation.CustomerId == null && normalizedPhones.Contains(conversation.PhoneNumber))))
                                .Include(conversation => conversation.Customer)
                                .Include(conversation => conversation.AssignedUser)
                                .SingleOrDefaultAsync(cancellationToken);
                        }
                    }
                }
                if (selected is not null)
                {
                    conversations.Add(selected);
                }
            }
        }
        else if (selectConversation)
        {
            selected = conversations.FirstOrDefault();
        }

        if (markAsRead && selected is not null && selected.UnreadCount > 0 && await accessService.CanAccessConversationAsync(User, selected.Id, cancellationToken))
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
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        var scope = await accessService.GetScopeAsync(User, cancellationToken);
        var customerIds = accessService.ApplyCustomerScope(db.Customers.AsNoTracking(), scope).Select(x => x.Id);
        IQueryable<WhatsAppConversation> query = db.WhatsAppConversations.AsNoTracking().Include(x => x.Customer);
        if (scope.IsRestricted && !User.IsInRole(ApplicationRoles.Operator))
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
                    phoneNumber = item.PhoneNumber,
                    initials = Initials(item.Customer?.TradeName),
                    identified = item.CustomerId.HasValue,
                    preview = Preview(lastMessage),
                    messageType = lastMessage?.Type.ToString().ToLowerInvariant(),
                    lastMessageId = lastMessage?.Id,
                    lastMessageDirection = lastMessage?.Direction.ToString().ToLowerInvariant(),
                    lastMessageAt = item.LastMessageAt.HasValue ? ToUtcIso(item.LastMessageAt.Value) : null,
                    item.UnreadCount,
                    status = item.Status.ToString().ToLowerInvariant()
                };
            }),
            messages = messages.Select(MessagePayload)
        });
    }

    [HttpGet]
    public async Task<IActionResult> CustomerSearch(string? q, CancellationToken cancellationToken)
    {
        var term = q?.Trim();
        if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            return Json(new { results = Array.Empty<object>() });

        var scope = await accessService.GetScopeAsync(User, cancellationToken);
        var normalized = term.ToLowerInvariant();
        var customerQuery = User.IsInRole(ApplicationRoles.Operator)
            ? db.Customers.AsNoTracking()
            : accessService.ApplyCustomerScope(db.Customers.AsNoTracking(), scope);
        var customers = customerQuery
            .Where(x => x.IsActive)
            .Where(x => x.TradeName.ToLower().Contains(normalized)
                || x.LegalName.ToLower().Contains(normalized)
                || x.Cnpj.ToLower().Contains(normalized)
                || x.Phone.ToLower().Contains(normalized)
                || x.WhatsApp.ToLower().Contains(normalized)
                || (x.WmcCode != null && x.WmcCode.ToLower().Contains(normalized)))
            .OrderBy(x => x.TradeName)
            .ThenBy(x => x.LegalName)
            .Take(20);

        if (User.IsInRole(ApplicationRoles.Operator))
        {
            var results = await customers.Select(x => new
            {
                id = x.Id,
                name = string.IsNullOrWhiteSpace(x.TradeName) ? x.LegalName : x.TradeName,
                phone = string.IsNullOrWhiteSpace(x.WhatsApp) ? x.Phone : x.WhatsApp
            }).ToListAsync(cancellationToken);
            return Json(new { results });
        }

        var sellerResults = await customers.Select(x => new
        {
            id = x.Id,
            name = string.IsNullOrWhiteSpace(x.TradeName) ? x.LegalName : x.TradeName,
            code = x.WmcCode,
            cnpj = x.Cnpj,
            phone = string.IsNullOrWhiteSpace(x.WhatsApp) ? x.Phone : x.WhatsApp
        }).ToListAsync(cancellationToken);
        return Json(new { results = sellerResults });
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

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Claim(long conversationId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null || !await accessService.CanAccessConversationAsync(User, conversationId, cancellationToken)) return Forbid();
        var updated = await db.WhatsAppConversations
            .Where(x => x.Id == conversationId && x.AssignedUserId == null && x.Status == WhatsAppConversationStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.AssignedUserId, userId)
                .SetProperty(x => x.Status, WhatsAppConversationStatus.Open)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), cancellationToken);
        if (updated == 0) TempData["WhatsAppError"] = "Esta conversa já foi assumida por outro atendente.";
        return RedirectToAction(nameof(Index), new { id = conversationId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(long conversationId, CancellationToken cancellationToken)
    {
        if (!await accessService.CanAccessConversationAsync(User, conversationId, cancellationToken)) return Forbid();
        await db.WhatsAppConversations.Where(x => x.Id == conversationId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, WhatsAppConversationStatus.Closed)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), cancellationToken);
        return RedirectToAction(nameof(Index), new { id = conversationId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reopen(long conversationId, CancellationToken cancellationToken)
    {
        if (!await accessService.CanAccessConversationAsync(User, conversationId, cancellationToken)) return Forbid();
        await db.WhatsAppConversations.Where(x => x.Id == conversationId && x.Status == WhatsAppConversationStatus.Closed)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, WhatsAppConversationStatus.Pending)
                .SetProperty(x => x.AssignedUserId, (string?)null)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), cancellationToken);
        return RedirectToAction(nameof(Index), new { id = conversationId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkCustomer(long conversationId, int customerId, CancellationToken cancellationToken)
    {
        if (!await accessService.CanAccessConversationAsync(User, conversationId, cancellationToken)) return Forbid();
        var scope = await accessService.GetScopeAsync(User, cancellationToken);
        var isOperator = User.IsInRole(ApplicationRoles.Operator);
        var customerQuery = isOperator
            ? db.Customers.AsQueryable()
            : accessService.ApplyCustomerScope(db.Customers, scope);
        var customer = await customerQuery
            .Where(x => !isOperator || x.IsActive)
            .Where(x => x.Id == customerId).Select(x => new { x.Id, x.InternalSalesUserId }).SingleOrDefaultAsync(cancellationToken);
        if (customer is null) return NotFound();
        await db.WhatsAppConversations.Where(x => x.Id == conversationId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.CustomerId, customer.Id)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), cancellationToken);
        return RedirectToAction(nameof(Index), new { id = conversationId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UnlinkCustomer(long conversationId, CancellationToken cancellationToken)
    {
        if (!await accessService.CanAccessConversationAsync(User, conversationId, cancellationToken)) return Forbid();
        await db.WhatsAppConversations.Where(x => x.Id == conversationId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.CustomerId, (int?)null)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), cancellationToken);
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
        createdAt = ToUtcIso(message.CreatedAt)
    };

    private static string ToUtcIso(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);

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
