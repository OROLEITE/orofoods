using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Services.Commercial;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Administrador,Vendedor,GerenteComercial,Operador")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class NotificationsController(UserNotificationService service) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        DisableCaching();
        var userId = service.CurrentUserId(User);
        if (userId is null) return Forbid();
        if (WantsJson) return Json(await SnapshotAsync(userId, cancellationToken));
        ViewBag.UnreadCount = await service.UnreadCountAsync(userId, cancellationToken);
        return View(await service.RecentAsync(userId, cancellationToken));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Read(long id, CancellationToken cancellationToken, bool navigate = false)
    {
        DisableCaching();
        var userId = service.CurrentUserId(User);
        if (userId is null) return Forbid();
        if (!await service.MarkReadAsync(userId, id, cancellationToken)) return NotFound();
        var target = SafeActionUrl(await service.NavigationTargetAsync(userId, id, cancellationToken));
        if (WantsJson) return Json(await SnapshotAsync(userId, cancellationToken, target));
        if (navigate) return LocalRedirect(target);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReadAll(CancellationToken cancellationToken)
    {
        DisableCaching();
        var userId = service.CurrentUserId(User);
        if (userId is null) return Forbid();
        await service.MarkAllReadAsync(userId, cancellationToken);
        if (WantsJson) return Json(await SnapshotAsync(userId, cancellationToken));
        return RedirectToAction(nameof(Index));
    }

    private bool WantsJson => Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);

    private void DisableCaching()
    {
        Response.Headers.CacheControl = "no-store, no-cache";
        Response.Headers.Pragma = "no-cache";
    }

    private string SafeActionUrl(string? actionUrl)
    {
        var fallback = Url.Action(nameof(Index), "Notifications", new { area = "Admin" }) ?? $"{Request.PathBase}/Admin/Notifications";
        if (string.IsNullOrWhiteSpace(actionUrl) || !Url.IsLocalUrl(actionUrl)
            || actionUrl.Any(character => character == '\\' || char.IsControl(character) || char.IsWhiteSpace(character)))
            return fallback;
        return actionUrl.StartsWith("~/", StringComparison.Ordinal) ? Url.Content(actionUrl) : actionUrl;
    }

    private async Task<object> SnapshotAsync(string userId, CancellationToken cancellationToken, string? actionUrl = null)
    {
        var unreadCount = await service.UnreadCountAsync(userId, cancellationToken);
        var notifications = await service.RecentAsync(userId, cancellationToken);
        return new
        {
            unreadCount,
            items = notifications.Select(notification => new
            {
                id = notification.Id.ToString(CultureInfo.InvariantCulture),
                title = notification.Title,
                message = notification.Message,
                createdAt = DateTime.SpecifyKind(notification.CreatedAt, DateTimeKind.Utc),
                readAt = notification.ReadAt.HasValue ? DateTime.SpecifyKind(notification.ReadAt.Value, DateTimeKind.Utc) : (DateTime?)null,
                actionUrl = SafeActionUrl(notification.ActionUrl)
            }).ToArray(),
            actionUrl
        };
    }
}
