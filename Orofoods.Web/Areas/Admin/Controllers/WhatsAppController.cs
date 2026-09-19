using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Identity;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Administrador,Vendedor,GerenteComercial")]
public class WhatsAppController(ApplicationDbContext db, SalesRepresentativeAccessService accessService, IWhatsAppBusinessGateway gateway) : Controller
{
    public async Task<IActionResult> Index(long? id, CancellationToken cancellationToken)
    {
        var scope = await accessService.GetScopeAsync(User, cancellationToken);
        var customerIds = accessService.ApplyCustomerScope(db.Customers.AsNoTracking(), scope).Select(x => x.Id);
        var conversations = await db.WhatsAppConversations.AsNoTracking().Include(x => x.Customer).Include(x => x.AssignedUser).Where(x => x.CustomerId == null || customerIds.Contains(x.CustomerId.Value)).OrderByDescending(x => x.LastMessageAt).Take(50).ToListAsync(cancellationToken);
        var selected = id.HasValue ? conversations.FirstOrDefault(x => x.Id == id) : conversations.FirstOrDefault();
        var messages = selected is null ? [] : await db.WhatsAppMessages.AsNoTracking().Where(x => x.ConversationId == selected.Id).OrderBy(x => x.CreatedAt).Take(100).ToListAsync(cancellationToken);
        return View(new WhatsAppInboxViewModel(conversations, selected, messages));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(long conversationId, string text, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var conversation = await db.WhatsAppConversations.SingleOrDefaultAsync(x => x.Id == conversationId, cancellationToken);
        if (conversation is null || userId is null) return Forbid();
        var result = await gateway.SendTextAsync(conversation.PhoneNumber, text.Trim(), cancellationToken);
        if (!result.Succeeded) { TempData["WhatsAppError"] = result.ErrorMessage; return RedirectToAction(nameof(Index), new { id = conversationId }); }
        db.WhatsAppMessages.Add(new WhatsAppMessage { ConversationId = conversationId, ExternalMessageId = result.ExternalMessageId, Direction = WhatsAppMessageDirection.Outbound, Type = WhatsAppMessageType.Text, TextBody = text.Trim(), Status = WhatsAppMessageStatus.Sent, SentAt = DateTime.UtcNow });
        conversation.LastOutboundAt = DateTime.UtcNow; conversation.LastMessageAt = DateTime.UtcNow; conversation.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index), new { id = conversationId });
    }
}

public sealed record WhatsAppInboxViewModel(IReadOnlyList<WhatsAppConversation> Conversations, WhatsAppConversation? Selected, IReadOnlyList<WhatsAppMessage> Messages);
