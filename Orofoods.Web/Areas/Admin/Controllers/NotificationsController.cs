using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Services.Commercial;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Administrador,Vendedor,GerenteComercial")]
public class NotificationsController(UserNotificationService service) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = service.CurrentUserId(User);
        if (userId is null) return Forbid();
        ViewBag.UnreadCount = await service.UnreadCountAsync(userId, cancellationToken);
        return View(await service.RecentAsync(userId, cancellationToken));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Read(long id, CancellationToken cancellationToken)
    {
        var userId = service.CurrentUserId(User);
        if (userId is null) return Forbid();
        await service.MarkReadAsync(userId, id, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReadAll(CancellationToken cancellationToken)
    {
        var userId = service.CurrentUserId(User);
        if (userId is null) return Forbid();
        await service.MarkAllReadAsync(userId, cancellationToken);
        return RedirectToAction(nameof(Index));
    }
}
