using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Services.Orders;

public class CustomerDashboardService(
    ApplicationDbContext db,
    FrequentProductService frequentProductService,
    TimeProvider timeProvider)
{
    public async Task<DashboardVm> GetAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var chartStart = monthStart.AddMonths(-5);
        var nextMonthStart = monthStart.AddMonths(1);

        var purchaseGroups = await db.Orders
            .AsNoTracking()
            .Where(order =>
                order.CustomerId == customer.Id &&
                order.Status != OrderStatus.Cancelled &&
                order.Status != OrderStatus.Draft &&
                order.CreatedAt >= chartStart &&
                order.CreatedAt < nextMonthStart)
            .GroupBy(order => new { order.CreatedAt.Year, order.CreatedAt.Month })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                Total = group.Sum(order => order.Total),
                OrderCount = group.Count()
            })
            .ToListAsync(cancellationToken);
        var purchaseGroupsByMonth = purchaseGroups.ToDictionary(
            group => new DateTime(group.Year, group.Month, 1),
            group => new MonthlyPurchaseVm(new DateTime(group.Year, group.Month, 1), group.Total, group.OrderCount));
        var monthlyPurchases = Enumerable.Range(0, 6)
            .Select(offset => monthStart.AddMonths(offset - 5))
            .Select(month => purchaseGroupsByMonth.GetValueOrDefault(month) ?? new MonthlyPurchaseVm(month, 0m, 0))
            .ToList();
        var currentMonth = monthlyPurchases[^1];
        var recentOrders = await db.Orders
            .AsNoTracking()
            .Where(order => order.CustomerId == customer.Id)
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .Take(5)
            .ToListAsync(cancellationToken);

        return new DashboardVm
        {
            Customer = customer,
            LastOrder = await db.Orders
                .Include(order => order.Items)
                .ThenInclude(item => item.Product)
                .Where(order => order.CustomerId == customer.Id)
                .OrderByDescending(order => order.CreatedAt)
                .ThenByDescending(order => order.Id)
                .FirstOrDefaultAsync(cancellationToken),
            RecentOrders = recentOrders,
            MonthlyPurchases = monthlyPurchases,
            MonthOrderCount = currentMonth.OrderCount,
            AverageTicket = currentMonth.OrderCount == 0 ? 0m : currentMonth.Total / currentMonth.OrderCount,
            FrequentProducts = await frequentProductService.GetAsync(customer.Id, cancellationToken: cancellationToken),
            OpenOrders = await db.Orders.CountAsync(order =>
                order.CustomerId == customer.Id &&
                order.Status != OrderStatus.Delivered &&
                order.Status != OrderStatus.Cancelled,
                cancellationToken),
            MonthTotal = currentMonth.Total
        };
    }
}
