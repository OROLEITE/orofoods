using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Services.Identity;

namespace Orofoods.Web.Services.Sellers;

public sealed class SellerWorkspaceService(
    ApplicationDbContext db,
    SalesRepresentativeAccessService accessService)
{
    public async Task<SellerDashboardViewModel?> GetDashboardAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var representative = await accessService.GetActiveRepresentativeAsync(user, cancellationToken);
        if (representative is null)
        {
            return null;
        }

        var customers = await GetPortfolioQuery(representative.Id)
            .OrderBy(x => x.TradeName)
            .Select(ToRowExpression())
            .ToListAsync(cancellationToken);

        return new SellerDashboardViewModel
        {
            SellerName = representative.Name,
            ActiveCustomerCount = customers.Count,
            Customers = customers
        };
    }

    public async Task<SellerCustomerListViewModel?> SearchCustomersAsync(
        ClaimsPrincipal user,
        string? query,
        CancellationToken cancellationToken = default)
    {
        var representative = await accessService.GetActiveRepresentativeAsync(user, cancellationToken);
        if (representative is null)
        {
            return null;
        }

        var customers = GetPortfolioQuery(representative.Id);
        if (!string.IsNullOrWhiteSpace(query))
        {
            query = query.Trim();
            customers = customers.Where(x =>
                x.TradeName.Contains(query) ||
                x.LegalName.Contains(query) ||
                (x.WmcCode != null && x.WmcCode.Contains(query)));
        }

        return new SellerCustomerListViewModel
        {
            Query = query,
            Customers = await customers
                .OrderBy(x => x.TradeName)
                .Select(ToRowExpression())
                .ToListAsync(cancellationToken)
        };
    }

    public async Task<SellerCustomerWorkspaceViewModel?> GetCustomerWorkspaceAsync(
        ClaimsPrincipal user,
        int customerId,
        CancellationToken cancellationToken = default)
    {
        var scope = await accessService.GetSellerCartScopeAsync(user, customerId, cancellationToken);
        if (scope is null)
        {
            return null;
        }

        var customer = await GetPortfolioQuery(scope.SalesRepresentativeId!.Value)
            .Include(x => x.SalesRepresentative)
            .Include(x => x.CustomerPaymentTerms)
            .ThenInclude(x => x.PaymentTerm)
            .SingleOrDefaultAsync(x => x.Id == customerId, cancellationToken);
        if (customer is null)
        {
            return null;
        }

        return new SellerCustomerWorkspaceViewModel
        {
            Customer = ToRow(customer),
            SalesRepresentativeName = customer.SalesRepresentative?.Name ?? "Vendedor",
            PaymentTerms = customer.CustomerPaymentTerms
                .Where(x => x.IsActive && x.PaymentTerm?.IsActive == true)
                .OrderBy(x => x.PaymentTerm!.SortOrder)
                .Select(x => x.PaymentTerm!.Name)
                .ToList()
        };
    }

    private IQueryable<Customer> GetPortfolioQuery(int representativeId) => db.Customers
        .AsNoTracking()
        .Where(x =>
            x.SalesRepresentativeId == representativeId &&
            x.IsActive &&
            x.Status == CustomerStatus.Approved)
        .Include(x => x.Addresses);

    private static SellerCustomerRow ToRow(Customer customer) => new(
        customer.Id,
        customer.WmcCode ?? customer.Cnpj,
        customer.TradeName,
        customer.Addresses.Where(address => address.IsActive).Select(address => address.City + "/" + address.State).FirstOrDefault() ?? "-",
        customer.Status);

    private static System.Linq.Expressions.Expression<Func<Customer, SellerCustomerRow>> ToRowExpression() => customer => new SellerCustomerRow(
        customer.Id,
        customer.WmcCode ?? customer.Cnpj,
        customer.TradeName,
        customer.Addresses.Where(address => address.IsActive).Select(address => address.City + "/" + address.State).FirstOrDefault() ?? "-",
        customer.Status);
}