using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Services.Orders;

namespace Orofoods.Web.Services.Identity;

public sealed record SalesRepresentativeScope(bool IsRestricted, int? SalesRepresentativeId, string? Region, string? UserId = null);

public class SalesRepresentativeAccessService(ApplicationDbContext db)
{
    public async Task<bool> HasActiveSellerAccessAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default) =>
        user.IsInRole("Vendedor") && await GetActiveRepresentativeAsync(user, cancellationToken) is not null;

    public Task<SalesRepresentative?> GetActiveRepresentativeAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (!user.IsInRole("Vendedor"))
        {
            return Task.FromResult<SalesRepresentative?>(null);
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult<SalesRepresentative?>(null);
        }

        return db.Users
            .Where(x => x.Id == userId && x.IsActive && x.SalesRepresentative != null && x.SalesRepresentative.IsActive)
            .Select(x => x.SalesRepresentative)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<CartScope?> GetSellerCartScopeAsync(
        ClaimsPrincipal user,
        int customerId,
        CancellationToken cancellationToken = default)
    {
        if (!user.IsInRole("Vendedor") || customerId <= 0)
        {
            return null;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var representative = await GetActiveRepresentativeAsync(user, cancellationToken);
        if (representative is null)
        {
            return null;
        }

        var customerIsInPortfolio = await db.Customers.AnyAsync(x =>
            x.Id == customerId &&
            x.SalesRepresentativeId == representative.Id &&
            x.IsActive &&
            x.Status == CustomerStatus.Approved,
            cancellationToken);

        return customerIsInPortfolio
            ? CartScope.ForSeller(representative.Id, customerId)
            : null;
    }

    public async Task<SalesRepresentativeScope> GetScopeAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        if (user.IsInRole("Administrador") || user.IsInRole("GerenteComercial"))
        {
            return new SalesRepresentativeScope(false, null, null, user.FindFirstValue(ClaimTypes.NameIdentifier));
        }

        if (!user.IsInRole("Vendedor"))
        {
            return new SalesRepresentativeScope(true, null, null, user.FindFirstValue(ClaimTypes.NameIdentifier));
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var representative = await db.Users
            .Where(x => x.Id == userId)
            .Select(x => x.SalesRepresentative)
            .SingleOrDefaultAsync(cancellationToken);

        return new SalesRepresentativeScope(true, representative?.Id, representative?.Region, userId);
    }

    public IQueryable<Customer> ApplyCustomerScope(IQueryable<Customer> query, SalesRepresentativeScope scope)
    {
        if (!scope.IsRestricted)
        {
            return query;
        }

        if (!scope.SalesRepresentativeId.HasValue && string.IsNullOrWhiteSpace(scope.UserId))
        {
            return query.Where(_ => false);
        }

        var userId = scope.UserId;
        query = query.Where(x =>
            (scope.SalesRepresentativeId.HasValue && x.SalesRepresentativeId == scope.SalesRepresentativeId) ||
            (!string.IsNullOrWhiteSpace(userId) && x.InternalSalesUserId == userId));
        if (!string.IsNullOrWhiteSpace(scope.Region))
        {
            query = query.Where(x => x.Addresses.Any(address => address.IsActive && (address.City + "/" + address.State) == scope.Region));
        }

        return query;
    }
}