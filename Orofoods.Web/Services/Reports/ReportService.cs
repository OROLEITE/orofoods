using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;

namespace Orofoods.Web.Services.Reports;

public sealed record ReportFilter(DateTime? DateFrom, DateTime? DateTo, int? CustomerId, int? ProductId, int? CategoryId, int? SalesRepresentativeId);
public sealed record ReportRankingRow(string Name, int Quantity, decimal Total);
public sealed record ReportStatusRow(OrderStatus Status, int Orders, decimal Total);

public sealed class SalesReport
{
    public int OrderCount { get; init; }
    public decimal Revenue { get; init; }
    public decimal AverageTicket { get; init; }
    public int BuyerCount { get; init; }
    public int SoldItemCount { get; init; }
    public decimal ProductRankingTotal { get; init; }
    public decimal CategoryRankingTotal { get; init; }
    public int CategoryCount { get; init; }
    public decimal CustomerRankingTotal { get; init; }
    public decimal SalesRepresentativeRankingTotal { get; init; }
    public decimal CityRankingTotal { get; init; }
    public List<ReportStatusRow> ByStatus { get; init; } = [];
    public List<ReportRankingRow> TopProducts { get; init; } = [];
    public List<ReportRankingRow> TopCustomers { get; init; } = [];
    public List<ReportRankingRow> ByCategory { get; init; } = [];
    public List<ReportRankingRow> BySalesRepresentative { get; init; } = [];
    public List<ReportRankingRow> ByCity { get; init; } = [];
}

public class ReportService(ApplicationDbContext db)
{
    public async Task<SalesReport> BuildAsync(ReportFilter filter, CancellationToken cancellationToken = default)
    {
        var query = db.Orders.AsNoTracking()
            .Include(x => x.Customer).ThenInclude(x => x!.SalesRepresentative)
            .Include(x => x.DeliveryAddress)
            .Include(x => x.Items).ThenInclude(x => x.Product).ThenInclude(x => x!.ProductCategory)
            .AsQueryable();
        if (filter.DateFrom is not null) query = query.Where(x => x.CreatedAt >= filter.DateFrom.Value.Date);
        if (filter.DateTo is not null) query = query.Where(x => x.CreatedAt < filter.DateTo.Value.Date.AddDays(1));
        if (filter.CustomerId is not null) query = query.Where(x => x.CustomerId == filter.CustomerId);
        if (filter.ProductId is not null) query = query.Where(x => x.Items.Any(i => i.ProductId == filter.ProductId));
        if (filter.CategoryId is not null) query = query.Where(x => x.Items.Any(i => i.Product!.ProductCategoryId == filter.CategoryId));
        if (filter.SalesRepresentativeId is not null) query = query.Where(x => x.Customer!.SalesRepresentativeId == filter.SalesRepresentativeId);

        var allOrders = await query.ToListAsync(cancellationToken);
        var orders = allOrders.Where(x => x.Status != OrderStatus.Cancelled).ToList();
        var items = orders.SelectMany(x => x.Items).ToList();
        var productRows = items.GroupBy(x => x.ProductNameSnapshot).Select(x => new ReportRankingRow(x.Key, x.Sum(i => i.Quantity), x.Sum(i => i.Subtotal))).ToList();
        var customerRows = orders.GroupBy(x => x.Customer?.TradeName ?? "Sem cliente").Select(x => new ReportRankingRow(x.Key, x.Count(), x.Sum(o => o.Total))).ToList();
        var categoryRows = items.GroupBy(x => x.Product?.ProductCategory?.Name ?? "Sem categoria").Select(x => new ReportRankingRow(x.Key, x.Sum(i => i.Quantity), x.Sum(i => i.Subtotal))).ToList();
        var salesRepresentativeRows = orders.GroupBy(x => x.Customer?.SalesRepresentative?.Name ?? "Sem vendedor").Select(x => new ReportRankingRow(x.Key, x.Count(), x.Sum(o => o.Total))).ToList();
        var cityRows = orders.GroupBy(x => x.DeliveryAddress?.City ?? "Sem cidade").Select(x => new ReportRankingRow(x.Key, x.Count(), x.Sum(o => o.Total))).ToList();
        var revenue = orders.Sum(x => x.Total);
        return new SalesReport
        {
            OrderCount = orders.Count,
            Revenue = revenue,
            AverageTicket = orders.Count == 0 ? 0 : revenue / orders.Count,
            BuyerCount = orders.Select(x => x.CustomerId).Distinct().Count(),
            SoldItemCount = items.Sum(x => x.Quantity),
            ProductRankingTotal = productRows.Sum(x => x.Total),
            CategoryRankingTotal = categoryRows.Sum(x => x.Total),
            CategoryCount = categoryRows.Count,
            CustomerRankingTotal = customerRows.Sum(x => x.Total),
            SalesRepresentativeRankingTotal = salesRepresentativeRows.Sum(x => x.Total),
            CityRankingTotal = cityRows.Sum(x => x.Total),
            ByStatus = allOrders.GroupBy(x => x.Status).Select(x => new ReportStatusRow(x.Key, x.Count(), x.Sum(o => o.Total))).OrderBy(x => x.Status).ToList(),
            TopProducts = Rank(productRows),
            TopCustomers = Rank(customerRows),
            ByCategory = Rank(categoryRows),
            BySalesRepresentative = Rank(salesRepresentativeRows),
            ByCity = Rank(cityRows)
        };
    }

    private static List<ReportRankingRow> Rank(IEnumerable<ReportRankingRow> rows) => rows.OrderByDescending(x => x.Total).ThenBy(x => x.Name).Take(10).ToList();
}
