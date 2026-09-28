using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Administrador,Vendedor,GerenteComercial")]
public class CommercialController(ApplicationDbContext db, SalesRepresentativeAccessService accessService, CommercialAttentionService attentionService) : Controller
{
    public async Task<IActionResult> Index(DateTime? date)
    {
        var scope = await accessService.GetScopeAsync(User);
        var scopedCustomerIds = accessService.ApplyCustomerScope(db.Customers.AsNoTracking(), scope).Select(x => x.Id);
        var referenceDate = (date ?? DateTime.Today).Date;
        var nextDate = referenceDate.AddDays(1);
        var activities = await db.CommercialActivities
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.SalesRepresentative)
            .Include(x => x.AssignedUser)
            .Where(x => scopedCustomerIds.Contains(x.CustomerId) && x.ScheduledAt >= referenceDate && x.ScheduledAt < nextDate)
            .OrderBy(x => x.ScheduledAt)
            .ToListAsync();
        var completedOrders = db.Orders.Where(x => scopedCustomerIds.Contains(x.CustomerId) && x.CreatedAt >= referenceDate && x.CreatedAt < nextDate && x.Status != OrderStatus.Cancelled);

        return View(new CommercialDashboardViewModel
        {
            ReferenceDate = referenceDate,
            CustomersToday = activities.Select(x => x.CustomerId).Distinct().Count(),
            PendingReturns = activities.Count(x => x.Type == CommercialActivityType.Return && x.Status != CommercialActivityStatus.Completed),
            OrdersToday = await completedOrders.CountAsync(),
            TodayRevenue = await completedOrders.SumAsync(x => (decimal?)x.Total) ?? 0,
            Activities = activities,
            UpcomingActivities = await db.CommercialActivities
                .AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.AssignedUser)
                .Where(x => scopedCustomerIds.Contains(x.CustomerId) && x.ScheduledAt >= referenceDate && x.ScheduledAt < referenceDate.AddDays(7) && x.Status != CommercialActivityStatus.Cancelled && x.Status != CommercialActivityStatus.Completed)
                .OrderBy(x => x.ScheduledAt)
                .Take(8)
                .ToListAsync(),
            Attention = await attentionService.GetAsync(User, DateTime.Now)
        });
    }

    public async Task<IActionResult> Create()
    {
        var scope = await accessService.GetScopeAsync(User);
        return View(await BuildFormAsync(new CommercialActivityFormViewModel(), scope));
    }

    public async Task<IActionResult> Calendar(DateTime? weekStart, int? salesRepresentativeId, CommercialActivityType? type, string? city, CommercialActivityStatus? status)
    {
        var scope = await accessService.GetScopeAsync(User);
        var scopedCustomerIds = accessService.ApplyCustomerScope(db.Customers.AsNoTracking(), scope).Select(x => x.Id);
        var selectedDate = (weekStart ?? DateTime.Today).Date;
        var mondayOffset = ((int)selectedDate.DayOfWeek + 6) % 7;
        var start = selectedDate.AddDays(-mondayOffset);

        var activitiesQuery = db.CommercialActivities
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.SalesRepresentative)
            .Where(x => scopedCustomerIds.Contains(x.CustomerId) && x.ScheduledAt >= start && x.ScheduledAt < start.AddDays(7));
        if (scope.IsRestricted)
        {
            activitiesQuery = activitiesQuery.Where(x => x.SalesRepresentativeId == scope.SalesRepresentativeId || x.AssignedUserId == scope.UserId);
        }
        else if (salesRepresentativeId.HasValue)
        {
            activitiesQuery = activitiesQuery.Where(x => x.SalesRepresentativeId == salesRepresentativeId);
        }
        if (type.HasValue)
        {
            activitiesQuery = activitiesQuery.Where(x => x.Type == type);
        }
        if (!string.IsNullOrWhiteSpace(city))
        {
            activitiesQuery = activitiesQuery.Where(x => x.Customer.Addresses.Any(address => address.City == city));
        }
        if (status.HasValue)
        {
            activitiesQuery = activitiesQuery.Where(x => x.Status == status);
        }

        return View(new CommercialCalendarViewModel
        {
            WeekStart = start,
            SalesRepresentativeId = salesRepresentativeId,
            Type = type,
            City = city,
            Status = status,
            Activities = await activitiesQuery.OrderBy(x => x.ScheduledAt).ToListAsync(),
            SalesRepresentatives = await db.SalesRepresentatives
                .AsNoTracking()
                .Where(x => x.IsActive && (!scope.IsRestricted || x.Id == scope.SalesRepresentativeId))
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == salesRepresentativeId))
                .ToListAsync(),
            Cities = await db.CustomerAddresses
                .AsNoTracking()
                .Where(x => scopedCustomerIds.Contains(x.CustomerId) && x.IsActive && x.City != "")
                .Select(x => x.City)
                .Distinct()
                .OrderBy(x => x)
                .Select(x => new SelectListItem(x, x, x == city))
                .ToListAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CommercialActivityFormViewModel input)
    {
        var scope = await accessService.GetScopeAsync(User);
        if (!await accessService.ApplyCustomerScope(db.Customers.AsNoTracking(), scope).AnyAsync(x => x.Id == input.CustomerId))
        {
            return Forbid();
        }
        if (!ModelState.IsValid)
        {
            return View(await BuildFormAsync(input, scope));
        }

        db.CommercialActivities.Add(new CommercialActivity
        {
            CustomerId = input.CustomerId,
            SalesRepresentativeId = scope.SalesRepresentativeId ?? input.SalesRepresentativeId,
            AssignedUserId = input.AssignedUserId,
            Type = input.Type,
            Status = input.Status,
            ScheduledAt = input.ScheduledAt,
            Title = input.Title,
            Notes = input.Notes,
            IsPriority = input.IsPriority
        });
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { date = input.ScheduledAt.Date });
    }

    private async Task<CommercialActivityFormViewModel> BuildFormAsync(CommercialActivityFormViewModel input, SalesRepresentativeScope scope)
    {
        input.Customers = await accessService.ApplyCustomerScope(db.Customers.AsNoTracking(), scope)
            .Where(x => x.IsActive)
            .OrderBy(x => x.TradeName)
            .Select(x => new SelectListItem(x.TradeName, x.Id.ToString()))
            .ToListAsync();
        input.SalesRepresentatives = await db.SalesRepresentatives
            .AsNoTracking()
            .Where(x => x.IsActive && (!scope.IsRestricted || x.Id == scope.SalesRepresentativeId))
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync();
        input.InternalSalesUsers = await db.Users
            .Where(x => x.IsActive && x.CustomerId == null)
            .OrderBy(x => x.Email)
            .Select(x => new SelectListItem(x.Email ?? x.UserName!, x.Id))
            .ToListAsync();
        return input;
    }
}
