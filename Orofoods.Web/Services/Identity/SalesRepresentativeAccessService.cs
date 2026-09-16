using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;

namespace Orofoods.Web.Services.Identity;

public sealed record SalesRepresentativeScope(bool IsRestricted, int? SalesRepresentativeId, string? Region);

public class SalesRepresentativeAccessService(ApplicationDbContext db)
{
    public async Task<SalesRepresentativeScope> GetScopeAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        if (user.IsInRole("Administrador"))
        {
            return new SalesRepresentativeScope(false, null, null);
        }

        if (!user.IsInRole("Vendedor"))
        {
            return new SalesRepresentativeScope(true, null, null);
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var representative = await db.Users
            .Where(x => x.Id == userId)
            .Select(x => x.SalesRepresentative)
            .SingleOrDefaultAsync(cancellationToken);

        return new SalesRepresentativeScope(true, representative?.Id, representative?.Region);
    }

    public IQueryable<Customer> ApplyCustomerScope(IQueryable<Customer> query, SalesRepresentativeScope scope)
    {
        if (!scope.IsRestricted)
        {
            return query;
        }

        if (!scope.SalesRepresentativeId.HasValue)
        {
            return query.Where(_ => false);
        }

        query = query.Where(x => x.SalesRepresentativeId == scope.SalesRepresentativeId);
        if (!string.IsNullOrWhiteSpace(scope.Region))
        {
            query = query.Where(x => x.Addresses.Any(address => address.IsActive && (address.City + "/" + address.State) == scope.Region));
        }

        return query;
    }
}