using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Services;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Administrador")]
public class DashboardController(ApplicationDbContext db, TimeProvider timeProvider, WmcSyncCoordinator wmcSyncCoordinator) : Controller
{
    private static readonly TimeZoneInfo SaoPauloTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public async Task<IActionResult> Index()
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var today = nowUtc.Date;
        var localToday = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, SaoPauloTimeZone).Date;
        var operationDayStartUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localToday, DateTimeKind.Unspecified), SaoPauloTimeZone);
        var operationNextDayStartUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localToday.AddDays(1), DateTimeKind.Unspecified), SaoPauloTimeZone);
        var todayOrders = db.Orders.Where(x => x.CreatedAt >= operationDayStartUtc && x.CreatedAt < operationNextDayStartUtc);
        var lastWmcSync = wmcSyncCoordinator.LastRun;
        var wmcSyncHasFailed = lastWmcSync is not null && (
            lastWmcSync.Customers.ErrorMessage is not null ||
            lastWmcSync.Products.ErrorMessage is not null ||
            lastWmcSync.Sellers.ErrorMessage is not null ||
            lastWmcSync.Stock.ErrorMessage is not null);
        var monthStart = UtcDateRange.MonthStart(today);
        var previousMonthStart = monthStart.AddMonths(-1);
        var completedOrders = db.Orders.Where(x => x.Status != OrderStatus.Cancelled);
        var monthRevenue = await completedOrders.Where(x => x.CreatedAt >= monthStart).SumAsync(x => (decimal?)x.Total) ?? 0;
        var previousMonthRevenue = await completedOrders.Where(x => x.CreatedAt >= previousMonthStart && x.CreatedAt < monthStart).SumAsync(x => (decimal?)x.Total) ?? 0;
        return View(new AdminDashboardViewModel
        {
            OrdersToday = await db.Orders.CountAsync(x => x.CreatedAt >= today),
            OrdersTodayTotal = await todayOrders.CountAsync(),
            OrdersTodayAwaitingAction = await todayOrders.CountAsync(x => x.Status == OrderStatus.Received || x.Status == OrderStatus.UnderReview),
            OrdersTodayReceived = await todayOrders.CountAsync(x => x.Status == OrderStatus.Received),
            OrdersTodayInAnalysisOrSeparation = await todayOrders.CountAsync(x => x.Status == OrderStatus.UnderReview || x.Status == OrderStatus.Approved || x.Status == OrderStatus.Picking),
            OrdersTodayReadyOrInDelivery = await todayOrders.CountAsync(x => x.Status == OrderStatus.Invoiced || x.Status == OrderStatus.OutForDelivery),
            OrdersTodayDelivered = await todayOrders.CountAsync(x => x.Status == OrderStatus.Delivered),
            OrdersDeliveredToday = await db.OrderStatusHistories
                .Where(x => x.Status == OrderStatus.Delivered && x.ChangedAt >= operationDayStartUtc && x.ChangedAt < operationNextDayStartUtc)
                .Select(x => x.OrderId)
                .Distinct()
                .CountAsync(),
            PendingOrders = await db.Orders.CountAsync(x => x.Status == OrderStatus.Received || x.Status == OrderStatus.UnderReview),
            ActiveCustomers = await db.Customers.CountAsync(x => x.IsActive && x.Status == CustomerStatus.Approved),
            PendingCustomers = await db.Customers.CountAsync(x => x.Status == CustomerStatus.Pending),
            MonthRevenue = monthRevenue,
            MonthRevenueChangePercent = previousMonthRevenue == 0 ? null : (monthRevenue - previousMonthRevenue) / previousMonthRevenue * 100,
            AverageTicket = await completedOrders.AnyAsync() ? await completedOrders.AverageAsync(x => x.Total) : 0,
            FailedIntegrations = await db.Orders.CountAsync(x => x.IntegrationStatus == IntegrationStatus.Failed),
            PendingIntegrations = await db.Orders.CountAsync(x => x.IntegrationStatus == IntegrationStatus.Pending),
            ProcessingIntegrations = await db.Orders.CountAsync(x => x.IntegrationStatus == IntegrationStatus.Processing),
            HasIntegrationRecords = await db.Orders.AnyAsync(),
            FailedWmcExports = await db.Orders.CountAsync(order =>
                order.WmcExportAudits.Any() &&
                !order.WmcExportAudits
                    .OrderByDescending(audit => audit.ExportedAt)
                    .ThenByDescending(audit => audit.Id)
                    .Select(audit => audit.Succeeded)
                    .First()),
            WmcSyncIsRunning = wmcSyncCoordinator.IsRunning,
            WmcSyncHasRun = lastWmcSync is not null,
            WmcSyncHasFailed = wmcSyncHasFailed,
            ActiveMercadoPagoPointTerminals = await db.PaymentTerminals.CountAsync(x =>
                x.Provider == PaymentTerminalProvider.MercadoPago && x.IsActive),
            FailedWhatsAppMessages = await db.WhatsAppMessages.CountAsync(x => x.Status == WhatsAppMessageStatus.Failed),
            LowStockProducts = await db.ProductInventories.CountAsync(x => x.QuantityOnHand - x.QuantityReserved < 10),
            RecentOrders = await db.Orders.AsNoTracking().Include(x => x.Customer).OrderByDescending(x => x.CreatedAt).Take(8).ToListAsync()
        });
    }
}
