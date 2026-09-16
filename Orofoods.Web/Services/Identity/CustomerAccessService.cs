using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;

namespace Orofoods.Web.Services.Identity;

public class CustomerAccessService(ApplicationDbContext db)
{
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
                    .Include(customer => customer.CustomerPaymentTerms)
                    .ThenInclude(link => link.PaymentTerm)
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
            .Include(customer => customer.CustomerPaymentTerms)
            .ThenInclude(link => link.PaymentTerm)
            .SingleOrDefaultAsync(
                customer => customer.Id == customerId && customer.IsActive && customer.Status == CustomerStatus.Approved,
                cancellationToken);
    }
}
