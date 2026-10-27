using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Identity;

namespace Orofoods.Web.Services.Orders;

public sealed class WmcOrderImportAccessService(
    ApplicationDbContext db,
    SalesRepresentativeAccessService salesRepresentativeAccessService)
{
    public async Task<bool> CanAccessCustomerAsync(
        ClaimsPrincipal user,
        int customerId,
        CancellationToken cancellationToken = default)
    {
        if (customerId <= 0)
        {
            return false;
        }

        if (user.IsInRole(ApplicationRoles.Administrator) || user.IsInRole(ApplicationRoles.Operator))
        {
            return true;
        }

        if (!user.IsInRole(ApplicationRoles.Seller)
            || !await salesRepresentativeAccessService.HasActiveSellerAccessAsync(user, cancellationToken))
        {
            return false;
        }

        var scope = await salesRepresentativeAccessService.GetScopeAsync(user, cancellationToken);
        return await salesRepresentativeAccessService
            .ApplyCustomerScope(db.Customers.AsNoTracking(), scope)
            .AnyAsync(customer => customer.Id == customerId, cancellationToken);
    }

    public async Task<IQueryable<Order>?> ApplyOrderScopeAsync(
        ClaimsPrincipal user,
        IQueryable<Order> orders,
        CancellationToken cancellationToken = default)
    {
        if (user.IsInRole(ApplicationRoles.Administrator) || user.IsInRole(ApplicationRoles.Operator))
        {
            return orders;
        }

        if (!user.IsInRole(ApplicationRoles.Seller)
            || !await salesRepresentativeAccessService.HasActiveSellerAccessAsync(user, cancellationToken))
        {
            return null;
        }

        var scope = await salesRepresentativeAccessService.GetScopeAsync(user, cancellationToken);
        var customerIds = salesRepresentativeAccessService
            .ApplyCustomerScope(db.Customers.AsNoTracking(), scope)
            .Select(customer => customer.Id);
        return orders.Where(order => customerIds.Contains(order.CustomerId));
    }
}
