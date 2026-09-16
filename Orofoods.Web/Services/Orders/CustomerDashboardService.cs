using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;
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
        var nextMonthStart = monthStart.AddMonths(1);

        return new DashboardVm
        {
            Customer = customer,
            LastOrder = await db.Orders
                .Include(x => x.Items)
                .ThenInclude(x => x.Product)
                .Where(x => x.CustomerId == customer.Id)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken),
            FrequentProducts = await frequentProductService.GetAsync(customer.Id, cancellationToken: cancellationToken),
            OpenOrders = await db.Orders.CountAsync(x =>
                x.CustomerId == customer.Id &&
                x.Status != OrderStatus.Delivered &&
                x.Status != OrderStatus.Cancelled,
                cancellationToken),
            MonthTotal = await db.Orders
                .Where(x =>
                    x.CustomerId == customer.Id &&
                    x.CreatedAt >= monthStart &&
                    x.CreatedAt < nextMonthStart)
                .SumAsync(x => (decimal?)x.Total, cancellationToken) ?? 0
        };
    }
}
