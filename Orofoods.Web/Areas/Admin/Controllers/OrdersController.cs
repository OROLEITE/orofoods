using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Orofoods.Web.Api;
using Orofoods.Web.Data;
using Orofoods.Web.Services.Orders;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Administrador")]
public class OrdersController(ApplicationDbContext db, AdminOrderService service, OrderIntegrationService integrationService) : Controller
{
    public async Task<IActionResult> Index(string? q, string? status, string sort = "date", string direction = "desc", int page = 1)
    {
        var query = db.Orders.AsNoTracking().Include(x => x.Customer).Include(x => x.Items).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Number.Contains(q) || x.Customer!.TradeName.Contains(q));
        if (Enum.TryParse<OrderStatus>(status, true, out var parsed)) query = query.Where(x => x.Status == parsed);
        var descending = !string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase);
        query = (sort.ToLowerInvariant(), descending) switch
        {
            ("number", false) => query.OrderBy(x => x.Number),
            ("number", true) => query.OrderByDescending(x => x.Number),
            ("customer", false) => query.OrderBy(x => x.Customer!.TradeName),
            ("customer", true) => query.OrderByDescending(x => x.Customer!.TradeName),
            ("status", false) => query.OrderBy(x => x.Status),
            ("status", true) => query.OrderByDescending(x => x.Status),
            ("total", false) => query.OrderBy(x => x.Total),
            ("total", true) => query.OrderByDescending(x => x.Total),
            ("date", false) => query.OrderBy(x => x.CreatedAt),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };

        var totalItems = await query.CountAsync();
        var request = new PageRequest(page, 25);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)request.PageSize));
        if (request.Page > totalPages) request = new PageRequest(totalPages, request.PageSize);
        var orders = await query.Skip(request.Skip).Take(request.PageSize).ToListAsync();

        ViewBag.Query = q;
        ViewBag.Status = status;
        ViewBag.Sort = sort;
        ViewBag.Direction = descending ? "desc" : "asc";
        ViewBag.PagedResult = new PagedResult<Order>(orders, request.Page, request.PageSize, totalItems);
        return View(orders);
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
