using System.Security.Claims;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
    IOptions<WhatsAppBusinessOptions> whatsAppOptions,
    IWhatsAppMediaStorage? mediaStorage = null,
    TimeProvider? timeProvider = null) : Controller
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!whatsAppOptions.Value.Enabled)
        {
            context.Result = NotFound();
            return;
        }

        base.OnActionExecuting(context);
    }

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
        var transferCandidates = CanTransferConversations()
            ? await LoadTransferCandidatesAsync(cancellationToken)
            : [];
        var serviceWindowOpen = selected is not null && WhatsAppServiceWindow.IsOpen(selected.LastInboundAt, CurrentUtcNow());
        return View(new WhatsAppInboxViewModel(conversations, selected, messages, latestMessages, serviceWindowOpen, transferCandidates));
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
        WhatsAppConversation? selected = null;
        if (conversationId.HasValue)
        {
            selected = conversations.FirstOrDefault(item => item.Id == conversationId.Value)
                ?? await query.SingleOrDefaultAsync(item => item.Id == conversationId.Value, cancellationToken);
            if (selected is not null && await accessService.CanAccessConversationAsync(User, selected.Id, cancellationToken))
            {
                if (conversations.All(item => item.Id != selected.Id))
                    conversations.Add(selected);
            }
            else
            {
                selected = null;
            }
        }

        var latest = await LoadLatestMessagesAsync(conversations.Select(x => x.Id), cancellationToken);
        IReadOnlyList<WhatsAppMessage> messages = [];
        if (selected is not null)
        {
            messages = await db.WhatsAppMessages.AsNoTracking()
                .Where(x => x.ConversationId == selected.Id)
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
            messages = messages.Select(MessagePayload),
            serviceWindowOpen = selected is null ? (bool?)null : WhatsAppServiceWindow.IsOpen(selected.LastInboundAt, CurrentUtcNow())
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
        var searchDigits = new string(term.Where(char.IsAsciiDigit).ToArray());
        var isFormattedNumberSearch = searchDigits.Length >= 2 && term.All(character =>
            char.IsAsciiDigit(character) || " +().-/".Contains(character));
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
                || (isFormattedNumberSearch && (
                    x.Cnpj.Replace(".", "").Replace("/", "").Replace("-", "").Contains(searchDigits)
                    || x.Phone.Replace("+", "").Replace("(", "").Replace(")", "").Replace("-", "").Replace(".", "").Replace(" ", "").Contains(searchDigits)
                    || x.WhatsApp.Replace("+", "").Replace("(", "").Replace(")", "").Replace("-", "").Replace(".", "").Replace(" ", "").Contains(searchDigits)))
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

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> StartConversation(int? customerId, string? phoneNumber, CancellationToken cancellationToken)
    {
        if (User.FindFirstValue(ClaimTypes.NameIdentifier) is null)
            return Forbid();

        int? linkedCustomerId = null;
        if (customerId.HasValue)
        {
            var scope = await accessService.GetScopeAsync(User, cancellationToken);
            var customerQuery = User.IsInRole(ApplicationRoles.Operator)
                ? db.Customers.AsNoTracking()
                : accessService.ApplyCustomerScope(db.Customers.AsNoTracking(), scope);
            var customer = await customerQuery
                .Where(item => item.Id == customerId.Value && item.IsActive)
                .Select(item => new { item.Id, item.Phone, item.WhatsApp })
                .SingleOrDefaultAsync(cancellationToken);
            if (customer is null)
                return Forbid();

            linkedCustomerId = customer.Id;
            phoneNumber = WhatsAppConversationService.TryNormalizePhone(customer.WhatsApp)
                ?? WhatsAppConversationService.TryNormalizePhone(customer.Phone);
        }

        var normalizedPhone = WhatsAppConversationService.TryNormalizePhone(phoneNumber);
        if (normalizedPhone is null)
        {
            TempData["WhatsAppError"] = "Informe um telefone brasileiro válido com DDD.";
            return RedirectToAction(nameof(Index));
        }

        var existing = await db.WhatsAppConversations
            .SingleOrDefaultAsync(item => item.PhoneNumber == normalizedPhone, cancellationToken);
        if (existing is not null)
        {
            if (!await accessService.CanAccessConversationAsync(User, existing.Id, cancellationToken))
                return Forbid();

            if (existing.CustomerId is null && linkedCustomerId.HasValue)
            {
                existing.CustomerId = linkedCustomerId;
                existing.UpdatedAt = CurrentUtcNow();
                await db.SaveChangesAsync(cancellationToken);
            }
            return RedirectToAction(nameof(Index), new { id = existing.Id });
        }

        var sellerOwnsConversation = User.IsInRole(ApplicationRoles.Seller);
        var now = CurrentUtcNow();
        var conversation = new WhatsAppConversation
        {
            CustomerId = linkedCustomerId,
            AssignedUserId = sellerOwnsConversation ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null,
            PhoneNumber = normalizedPhone,
            Status = sellerOwnsConversation ? WhatsAppConversationStatus.Open : WhatsAppConversationStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.WhatsAppConversations.Add(conversation);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.Entry(conversation).State = EntityState.Detached;
            existing = await db.WhatsAppConversations
                .SingleOrDefaultAsync(item => item.PhoneNumber == normalizedPhone, cancellationToken);
            if (existing is null)
                throw;
            if (!await accessService.CanAccessConversationAsync(User, existing.Id, cancellationToken))
                return Forbid();
            return RedirectToAction(nameof(Index), new { id = existing.Id });
        }

        return RedirectToAction(nameof(Index), new { id = conversation.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Templates(long conversationId, CancellationToken cancellationToken)
    {
        if (!await accessService.CanAccessConversationAsync(User, conversationId, cancellationToken))
            return Forbid();

        var conversation = await db.WhatsAppConversations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == conversationId, cancellationToken);
        if (conversation is null)
            return NotFound();
        if (conversation.Status == WhatsAppConversationStatus.Closed ||
            WhatsAppServiceWindow.IsOpen(conversation.LastInboundAt, CurrentUtcNow()))
            return BadRequest(new { error = "Modelos de retomada são oferecidos fora da janela de atendimento." });

        try
        {
            return Json(new { results = await gateway.GetApprovedTemplatesAsync(cancellationToken) });
        }
        catch (InvalidOperationException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Não foi possível consultar os modelos aprovados." });
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Não foi possível consultar os modelos aprovados." });
        }
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
    public async Task<IActionResult> Send(long conversationId, string text, CancellationToken cancellationToken, string? clientRequestId = null)
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
        if (string.IsNullOrWhiteSpace(cleanedText) || cleanedText.Length > 4096)
        {
            TempData["WhatsAppError"] = "Informe uma mensagem válida de até 4.096 caracteres.";
            return RedirectToAction(nameof(Index), new { id = conversationId });
        }

        if (!WhatsAppServiceWindow.IsOpen(conversation.LastInboundAt, CurrentUtcNow()))
        {
            TempData["WhatsAppError"] = "A janela de atendimento de 24 horas encerrou. Consulte um modelo aprovado para retomar a conversa.";
            return RedirectToAction(nameof(Index), new { id = conversationId });
        }

        if (!TryNormalizeClientRequestId(clientRequestId, out var normalizedRequestId))
        {
            TempData["WhatsAppError"] = "Não foi possível validar o identificador desta mensagem. Atualize a conversa e tente novamente.";
            return RedirectToAction(nameof(Index), new { id = conversationId });
        }

        return await PersistAndSendAsync(
            conversation,
            normalizedRequestId,
            cleanedText,
            token => gateway.SendTextAsync(conversation.PhoneNumber, cleanedText, token),
            cancellationToken);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTemplate(
        long conversationId,
        string templateName,
        string language,
        string[]? bodyParameters,
        string[]? headerParameters,
        string? clientRequestId,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var conversation = await db.WhatsAppConversations.SingleOrDefaultAsync(x => x.Id == conversationId, cancellationToken);
        if (conversation is null || userId is null || !await accessService.CanAccessConversationAsync(User, conversationId, cancellationToken))
            return Forbid();
        if (conversation.Status == WhatsAppConversationStatus.Closed)
        {
            TempData["WhatsAppError"] = "Conversa fechada. Reabra o atendimento antes de enviar.";
            return RedirectToAction(nameof(Index), new { id = conversationId });
        }
        if (WhatsAppServiceWindow.IsOpen(conversation.LastInboundAt, CurrentUtcNow()))
        {
            TempData["WhatsAppError"] = "A janela de atendimento está aberta. Envie uma mensagem livre nesta conversa.";
            return RedirectToAction(nameof(Index), new { id = conversationId });
        }
        if (!TryNormalizeClientRequestId(clientRequestId, out var normalizedRequestId))
        {
            TempData["WhatsAppError"] = "Não foi possível validar o identificador desta mensagem.";
            return RedirectToAction(nameof(Index), new { id = conversationId });
        }

        var body = NormalizeTemplateParameters(bodyParameters);
        var header = NormalizeTemplateParameters(headerParameters);
        IReadOnlyList<WhatsAppApprovedTemplate> approvedTemplates;
        try
        {
            approvedTemplates = await gateway.GetApprovedTemplatesAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            TempData["WhatsAppError"] = "Não foi possível consultar os modelos aprovados. Tente novamente.";
            return RedirectToAction(nameof(Index), new { id = conversationId });
        }
        catch (HttpRequestException)
        {
            TempData["WhatsAppError"] = "Não foi possível consultar os modelos aprovados. Tente novamente.";
            return RedirectToAction(nameof(Index), new { id = conversationId });
        }

        var template = approvedTemplates.SingleOrDefault(item =>
            string.Equals(item.Name, templateName, StringComparison.Ordinal) &&
            string.Equals(item.Language, language, StringComparison.Ordinal));
        if (template is null || body.Length != template.BodyParameterCount || header.Length != template.HeaderParameterCount ||
            body.Any(string.IsNullOrWhiteSpace) || header.Any(string.IsNullOrWhiteSpace))
        {
            TempData["WhatsAppError"] = "Selecione um modelo aprovado e preencha todos os parâmetros solicitados.";
            return RedirectToAction(nameof(Index), new { id = conversationId });
        }

        var renderedHeader = RenderTemplateText(template.HeaderText, header, template.HeaderParameterNames);
        var renderedBody = RenderTemplateText(template.BodyText, body, template.BodyParameterNames);
        var historyText = string.IsNullOrWhiteSpace(renderedHeader) ? renderedBody : $"{renderedHeader}\n{renderedBody}";
        if (historyText.Length > 4096)
        {
            TempData["WhatsAppError"] = "O conteúdo deste modelo excede o limite aceito para o histórico.";
            return RedirectToAction(nameof(Index), new { id = conversationId });
        }

        return await PersistAndSendAsync(
            conversation,
            normalizedRequestId,
            historyText,
            token => gateway.SendTemplateAsync(
                conversation.PhoneNumber,
                template.Name,
                template.Language,
                body,
                header,
                token,
                template.BodyParameterNames,
                template.HeaderParameterNames),
            cancellationToken);
    }

    private async Task<IActionResult> PersistAndSendAsync(
        WhatsAppConversation conversation,
        string clientRequestId,
        string historyText,
        Func<CancellationToken, Task<WhatsAppSendResult>> send,
        CancellationToken cancellationToken)
    {
        var existing = await db.WhatsAppMessages.AsNoTracking()
            .SingleOrDefaultAsync(message => message.ClientRequestId == clientRequestId, cancellationToken);
        if (existing is not null)
        {
            ReportDuplicateSendOutcome(existing, conversation.Id);
            return RedirectToAction(nameof(Index), new { id = conversation.Id });
        }

        var now = CurrentUtcNow();
        var message = new WhatsAppMessage
        {
            ConversationId = conversation.Id,
            ClientRequestId = clientRequestId,
            Direction = WhatsAppMessageDirection.Outbound,
            Type = WhatsAppMessageType.Text,
            TextBody = historyText,
            Status = WhatsAppMessageStatus.Pending,
            CreatedAt = now
        };
        db.WhatsAppMessages.Add(message);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.Entry(message).State = EntityState.Detached;
            existing = await db.WhatsAppMessages.AsNoTracking()
                .SingleOrDefaultAsync(item => item.ClientRequestId == clientRequestId, cancellationToken);
            if (existing is null)
                throw;
            ReportDuplicateSendOutcome(existing, conversation.Id);
            return RedirectToAction(nameof(Index), new { id = conversation.Id });
        }

        WhatsAppSendResult result;
        try
        {
            result = await send(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or TimeoutException)
        {
            message.ErrorCode = "UNKNOWN";
            message.ErrorMessage = "O resultado do envio ainda não foi confirmado.";
            await db.SaveChangesAsync(cancellationToken);
            TempData["WhatsAppError"] = "Não foi possível confirmar o envio. A mensagem permanece pendente para evitar duplicidade; atualize o histórico antes de tentar novamente.";
            return RedirectToAction(nameof(Index), new { id = conversation.Id });
        }

        if (string.Equals(result.ErrorCode, "UNKNOWN", StringComparison.Ordinal) ||
            result.Succeeded && string.IsNullOrWhiteSpace(result.ExternalMessageId))
        {
            message.ErrorCode = "UNKNOWN";
            message.ErrorMessage = "O resultado do envio ainda não foi confirmado.";
            await db.SaveChangesAsync(cancellationToken);
            TempData["WhatsAppError"] = "Não foi possível confirmar o envio. A mensagem permanece pendente para evitar duplicidade; atualize o histórico antes de tentar novamente.";
            return RedirectToAction(nameof(Index), new { id = conversation.Id });
        }

        if (!result.Succeeded)
        {
            message.Status = WhatsAppMessageStatus.Failed;
            message.FailedAt = CurrentUtcNow();
            message.ErrorCode = SafeErrorCode(result.ErrorCode);
            message.ErrorMessage = "Não foi possível confirmar o envio da mensagem.";
            await db.SaveChangesAsync(cancellationToken);
            TempData["WhatsAppError"] = message.ErrorMessage;
            return RedirectToAction(nameof(Index), new { id = conversation.Id });
        }

        var sentAt = CurrentUtcNow();
        message.ExternalMessageId = result.ExternalMessageId;
        message.Status = WhatsAppMessageStatus.Sent;
        message.SentAt = sentAt;
        conversation.LastOutboundAt = sentAt;
        conversation.LastMessageAt = sentAt;
        conversation.UpdatedAt = sentAt;
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index), new { id = conversation.Id });
    }

    private void ReportDuplicateSendOutcome(WhatsAppMessage existing, long conversationId)
    {
        if (existing.ConversationId != conversationId)
        {
            TempData["WhatsAppError"] = "Este envio já foi associado a outra conversa.";
            return;
        }

        if (existing.Status == WhatsAppMessageStatus.Failed)
            TempData["WhatsAppError"] = "A tentativa anterior foi rejeitada. Você pode tentar novamente com a chave renovada.";
        else if (existing.Status == WhatsAppMessageStatus.Pending)
            TempData["WhatsAppError"] = "A confirmação da tentativa anterior ainda está pendente. Atualize o histórico antes de tentar novamente.";
    }

    private static string[] NormalizeTemplateParameters(string[]? values) =>
        values?.Select(value => value?.Trim() ?? string.Empty).ToArray() ?? [];

    private static string RenderTemplateText(
        string? templateText,
        IReadOnlyList<string> parameters,
        IReadOnlyList<string>? parameterNames = null)
    {
        if (string.IsNullOrWhiteSpace(templateText))
            return string.Empty;

        if (parameterNames is { Count: > 0 })
        {
            var namedValues = parameterNames
                .Select((name, index) => new { name, index })
                .Where(item => item.index < parameters.Count)
                .ToDictionary(item => item.name, item => parameters[item.index], StringComparer.Ordinal);
            return Regex.Replace(templateText, @"\{\{\s*([A-Za-z_][A-Za-z0-9_]*)\s*\}\}", match =>
                namedValues.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);
        }

        var positionalIndex = 0;
        return Regex.Replace(templateText, @"\{\{\s*(\d+|[A-Za-z_][A-Za-z0-9_]*)\s*\}\}", match =>
        {
            var index = int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                ? number - 1
                : positionalIndex++;
            return index >= 0 && index < parameters.Count ? parameters[index] : match.Value;
        });
    }

    private static bool TryNormalizeClientRequestId(string? value, out string normalized)
    {
        if (Guid.TryParse(value, out var requestId) && requestId != Guid.Empty)
        {
            normalized = requestId.ToString("N");
            return true;
        }
        normalized = string.Empty;
        return false;
    }

    private static string SafeErrorCode(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 80 && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-')
            ? value
            : "SEND_FAILED";

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
    public async Task<IActionResult> Transfer(long conversationId, string targetUserId, CancellationToken cancellationToken)
    {
        if (!CanTransferConversations() || !await accessService.CanAccessConversationAsync(User, conversationId, cancellationToken))
            return Forbid();

        var conversation = await db.WhatsAppConversations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == conversationId, cancellationToken);
        if (conversation is null)
            return NotFound();
        if (conversation.Status == WhatsAppConversationStatus.Closed)
        {
            TempData["WhatsAppError"] = "Reabra o atendimento antes de transferi-lo.";
            return RedirectToAction(nameof(Index), new { id = conversationId });
        }

        var eligibleRoles = new[] { ApplicationRoles.Administrator, ApplicationRoles.Seller, ApplicationRoles.CommercialManager, ApplicationRoles.Operator };
        var targetIsEligible = await (
            from user in db.Users.AsNoTracking()
            join userRole in db.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where user.Id == targetUserId && user.IsActive && role.Name != null && eligibleRoles.Contains(role.Name)
            select user.Id).AnyAsync(cancellationToken);
        if (!targetIsEligible)
        {
            TempData["WhatsAppError"] = "Selecione um atendente ativo.";
            return RedirectToAction(nameof(Index), new { id = conversationId });
        }

        await db.WhatsAppConversations
            .Where(item => item.Id == conversationId && item.Status != WhatsAppConversationStatus.Closed)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.AssignedUserId, targetUserId)
                .SetProperty(item => item.Status, WhatsAppConversationStatus.Open)
                .SetProperty(item => item.UpdatedAt, CurrentUtcNow()), cancellationToken);
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

    private DateTime CurrentUtcNow() => DateTime.SpecifyKind(
        timeProvider?.GetUtcNow().UtcDateTime ?? DateTime.UtcNow,
        DateTimeKind.Utc);

    private bool CanTransferConversations() =>
        User.IsInRole(ApplicationRoles.Administrator) ||
        User.IsInRole(ApplicationRoles.CommercialManager) ||
        User.IsInRole(ApplicationRoles.Operator);

    private async Task<IReadOnlyList<WhatsAppTransferCandidate>> LoadTransferCandidatesAsync(CancellationToken cancellationToken)
    {
        var eligibleRoles = new[] { ApplicationRoles.Administrator, ApplicationRoles.Seller, ApplicationRoles.CommercialManager, ApplicationRoles.Operator };
        var users = await (
            from user in db.Users.AsNoTracking()
            join userRole in db.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where user.IsActive && role.Name != null && eligibleRoles.Contains(role.Name)
            select new { user.Id, user.FullName, user.Email, user.UserName })
            .Distinct()
            .OrderBy(user => user.FullName)
            .ToListAsync(cancellationToken);

        return users.Select(user => new WhatsAppTransferCandidate(
                user.Id,
                !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : user.Email ?? user.UserName ?? user.Id))
            .ToArray();
    }

    private static string Initials(string? name) => string.Concat((name ?? string.Empty)
        .Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(part => char.ToUpperInvariant(part[0])));
}

public sealed record WhatsAppInboxViewModel(
    IReadOnlyList<WhatsAppConversation> Conversations,
    WhatsAppConversation? Selected,
    IReadOnlyList<WhatsAppMessage> Messages,
    IReadOnlyDictionary<long, WhatsAppMessage> LatestMessages,
    bool ServiceWindowOpen = false,
    IReadOnlyList<WhatsAppTransferCandidate>? TransferCandidates = null);

public sealed record WhatsAppTransferCandidate(string UserId, string DisplayName);
