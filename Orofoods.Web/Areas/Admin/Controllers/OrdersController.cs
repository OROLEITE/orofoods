using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Orofoods.Web.Data;
using Orofoods.Web.Services.Orders;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Administrador")]
public class OrdersController(ApplicationDbContext db, AdminOrderService service, OrderIntegrationService integrationService) : Controller
{
    public async Task<IActionResult> Index(string? q, string? status)
    {
        var query = db.Orders.AsNoTracking().Include(x => x.Customer).Include(x => x.Items).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Number.Contains(q) || x.Customer!.TradeName.Contains(q));
        if (Enum.TryParse<OrderStatus>(status, true, out var parsed)) query = query.Where(x => x.Status == parsed);
        ViewBag.Query = q; ViewBag.Status = status;
        return View(await query.OrderByDescending(x => x.CreatedAt).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var order = await db.Orders.AsNoTracking().Include(x => x.Customer).Include(x => x.Items).Include(x => x.DeliveryAddress).Include(x => x.PaymentTerm).Include(x => x.StatusHistory).ThenInclude(x => x.ChangedByUser).Include(x => x.WmcExportAudits).AsSplitQuery().SingleOrDefaultAsync(x => x.Id == id);
        return order is null ? NotFound() : View(order);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatus status) { await service.UpdateStatusAsync(id, status, User.FindFirstValue(ClaimTypes.NameIdentifier)); return RedirectToAction(nameof(Details), new { id }); }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReprocessIntegration(int id)
    {
        await integrationService.SendAsync(id);
        return RedirectToAction(nameof(Details), new { id });
    }
}
