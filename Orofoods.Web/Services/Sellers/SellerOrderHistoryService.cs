using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Services.Identity;

namespace Orofoods.Web.Services.Sellers;

public sealed class SellerOrderHistoryService(
    ApplicationDbContext db,
    SalesRepresentativeAccessService accessService)
{
    public async Task<SellerOrderHistoryViewModel?> GetHistoryAsync(
        ClaimsPrincipal user,
        int customerId,
        string? query = null,
        string? status = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var customer = await GetAuthorizedCustomerAsync(user, customerId, cancellationToken);
        if (customer is null) return null;

        pageSize = pageSize == 20 ? 20 : 10;
        page = Math.Max(1, page);
        var ordersQuery = db.Orders.AsNoTracking().Where(x => x.CustomerId == customerId);
        if (!string.IsNullOrWhiteSpace(query))
        {
            query = query.Trim();
            ordersQuery = ordersQuery.Where(x => x.Number.Contains(query) || x.PaymentMethod.Contains(query));
        }
        if (Enum.TryParse<Models.Orders.OrderStatus>(status, true, out var parsedStatus))
        {
            ordersQuery = ordersQuery.Where(x => x.Status == parsedStatus);
        }

        var totalItems = await ordersQuery.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Min(page, totalPages);
        var orders = await ordersQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToRow())
            .ToListAsync(cancellationToken);
        return new SellerOrderHistoryViewModel { CustomerId = customer.Id, CustomerName = customer.TradeName, CustomerCode = customer.WmcCode ?? customer.Cnpj, Query = query, Status = status, Page = page, PageSize = pageSize, TotalItems = totalItems, TotalPages = totalPages, Orders = orders };
    }

    public async Task<SellerOrderDetailViewModel?> GetDetailsAsync(ClaimsPrincipal user, int customerId, int orderId, CancellationToken cancellationToken = default)
    {
        var customer = await GetAuthorizedCustomerAsync(user, customerId, cancellationToken);
        if (customer is null) return null;

        var order = await db.Orders.AsNoTracking()
            .Include(x => x.Payments)
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == orderId && x.CustomerId == customerId, cancellationToken);
        if (order is null) return null;

        return new SellerOrderDetailViewModel
        {
            CustomerId = customer.Id,
            CustomerName = customer.TradeName,
            CustomerCode = customer.WmcCode ?? customer.Cnpj,
            Order = ToRow(order),
            Items = order.Items.Select(x => new SellerOrderItemRow(x.ProductNameSnapshot, x.SkuSnapshot, x.Quantity, x.UnitPrice, x.Subtotal)).ToList()
        };
    }

    private async Task<Customer?> GetAuthorizedCustomerAsync(ClaimsPrincipal user, int customerId, CancellationToken cancellationToken)
    {
        if (await accessService.GetSellerCartScopeAsync(user, customerId, cancellationToken) is null) return null;
        return await db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == customerId && x.IsActive && x.Status == CustomerStatus.Approved, cancellationToken);
    }

    private static SellerOrderRow ToRow(Models.Orders.Order order) => new(
        order.Id, order.Number, order.CreatedAt, order.Total, order.PaymentMethod, order.Status,
        order.Payments.OrderByDescending(payment => payment.CreatedAt).Select(payment => (Models.Payments.PaymentStatus?)payment.Status).FirstOrDefault());

    private static System.Linq.Expressions.Expression<Func<Models.Orders.Order, SellerOrderRow>> ToRow() => order => new SellerOrderRow(
        order.Id, order.Number, order.CreatedAt, order.Total, order.PaymentMethod, order.Status,
        order.Payments.OrderByDescending(payment => payment.CreatedAt).Select(payment => (Models.Payments.PaymentStatus?)payment.Status).FirstOrDefault());
}