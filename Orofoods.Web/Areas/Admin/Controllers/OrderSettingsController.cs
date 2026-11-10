using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Commercial;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Administrador")]
public sealed class OrderSettingsController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var settings = await db.OrderMinimumSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? new OrderMinimumSettings();
        return View(settings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(OrderMinimumSettings input, CancellationToken cancellationToken)
    {
        if (input.MinimumAmount <= 0)
            ModelState.AddModelError(nameof(input.MinimumAmount), "O pedido mínimo deve ser maior que zero.");
        if (!ModelState.IsValid) return View(input);

        var settings = await db.OrderMinimumSettings.SingleOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = new OrderMinimumSettings();
            db.OrderMinimumSettings.Add(settings);
        }
        settings.MinimumAmount = input.MinimumAmount;
        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        await db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Pedido mínimo atualizado.";
        return RedirectToAction(nameof(Index));
    }
}
