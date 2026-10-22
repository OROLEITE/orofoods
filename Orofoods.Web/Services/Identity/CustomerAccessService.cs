using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;

namespace Orofoods.Web.Services.Identity;

public class CustomerAccessService(ApplicationDbContext db)
{
    public async Task<IReadOnlyList<CustomerSearchSuggestion>> SearchApprovedCustomerSuggestionsAsync(
        string? query,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        query = query.Trim().ToLowerInvariant();
        var normalizedCnpjQuery = new string(query.Where(char.IsLetterOrDigit).ToArray());
        var limit = Math.Clamp(maxResults, 1, 20);

        return await db.Customers.AsNoTracking()
            .Where(customer => customer.IsActive && customer.Status == CustomerStatus.Approved)
            .Where(customer =>
                customer.TradeName.ToLower().Contains(query) ||
                customer.LegalName.ToLower().Contains(query) ||
                (normalizedCnpjQuery.Length > 0 && customer.Cnpj.Replace(".", "").Replace("/", "").Replace("-", "").Contains(normalizedCnpjQuery)) ||
                (customer.WmcCode != null && customer.WmcCode.ToLower().Contains(query)))
            .OrderBy(customer => customer.TradeName)
            .ThenBy(customer => customer.Id)
            .Take(limit)
            .Select(customer => new CustomerSearchSuggestion(
                customer.Id,
                customer.TradeName,
                customer.LegalName,
                customer.Cnpj,
                customer.WmcCode,
                customer.Addresses.Where(address => address.IsActive).OrderByDescending(address => address.IsPrimary)
                    .Select(address => address.City).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasApprovedCustomerAccessAsync(string? userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        return await db.Users
            .Where(user => user.Id == userId && user.IsActive && user.CustomerId.HasValue)
            .Join(
                db.Customers.Where(customer => customer.IsActive && customer.Status == CustomerStatus.Approved),
                user => user.CustomerId,
                customer => customer.Id,
                (_, _) => true)
            .AnyAsync(cancellationToken);
    }

    public Task<Customer?> GetApprovedCustomerAsync(string? userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult<Customer?>(null);
        }

        return db.Users
            .Where(user => user.Id == userId && user.IsActive && user.CustomerId.HasValue)
            .Select(user => user.CustomerId!.Value)
            .Join(
                db.Customers
                    .Include(customer => customer.Addresses)
                    .Include(customer => customer.PriceTable)
                    .Where(customer => customer.IsActive && customer.Status == CustomerStatus.Approved),
                customerId => customerId,
                customer => customer.Id,
                (_, customer) => customer)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<Customer?> GetApprovedCustomerByIdAsync(int customerId, CancellationToken cancellationToken = default)
    {
        return db.Customers
            .Include(customer => customer.Addresses)
            .Include(customer => customer.PriceTable)
            .SingleOrDefaultAsync(
                customer => customer.Id == customerId && customer.IsActive && customer.Status == CustomerStatus.Approved,
                cancellationToken);
    }
}
