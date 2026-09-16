using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Services;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Administrador")]
public class DashboardController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var today = DateTime.UtcNow.Date;
        var monthStart = UtcDateRange.MonthStart(today);
        var completedOrders = db.Orders.Where(x => x.Status != OrderStatus.Cancelled);
        return View(new AdminDashboardViewModel
        {
            OrdersToday = await db.Orders.CountAsync(x => x.CreatedAt >= today),
            PendingOrders = await db.Orders.CountAsync(x => x.Status == OrderStatus.Received || x.Status == OrderStatus.UnderReview),
            ActiveCustomers = await db.Customers.CountAsync(x => x.IsActive && x.Status == CustomerStatus.Approved),
            PendingCustomers = await db.Customers.CountAsync(x => x.Status == CustomerStatus.Pending),
            MonthRevenue = await completedOrders.Where(x => x.CreatedAt >= monthStart).SumAsync(x => (decimal?)x.Total) ?? 0,
            AverageTicket = await completedOrders.AnyAsync() ? await completedOrders.AverageAsync(x => x.Total) : 0,
            FailedIntegrations = await db.Orders.CountAsync(x => x.IntegrationStatus == IntegrationStatus.Failed),
            FailedWmcExports = await db.Orders.CountAsync(order =>
                order.WmcExportAudits.Any() &&
                !order.WmcExportAudits
                    .OrderByDescending(audit => audit.ExportedAt)
                    .ThenByDescending(audit => audit.Id)
                    .Select(audit => audit.Succeeded)
                    .First()),
            LowStockProducts = await db.ProductInventories.CountAsync(x => x.QuantityOnHand - x.QuantityReserved < 10),
            RecentOrders = await db.Orders.AsNoTracking().Include(x => x.Customer).OrderByDescending(x => x.CreatedAt).Take(8).ToListAsync()
        });
    }
}
