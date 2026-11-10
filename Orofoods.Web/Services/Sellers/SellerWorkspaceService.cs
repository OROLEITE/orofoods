using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Customers;

namespace Orofoods.Web.Services.Sellers;

public sealed class SellerWorkspaceService(
    ApplicationDbContext db,
    SalesRepresentativeAccessService accessService,
    IPaymentEligibilityService? paymentEligibilityService = null)
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

        if (string.IsNullOrWhiteSpace(query))
        {
            return new SellerCustomerListViewModel { Query = null, Customers = [] };
        }

        query = query.Trim();
        var normalizedQuery = query.ToLowerInvariant();
        var normalizedCnpjQuery = new string(query.Where(char.IsLetterOrDigit).ToArray());
        var customers = GetPortfolioQuery(representative.Id);
        customers = customers.Where(x =>
            x.TradeName.ToLower().Contains(normalizedQuery) ||
            x.LegalName.ToLower().Contains(normalizedQuery) ||
            (normalizedCnpjQuery.Length > 0 && x.Cnpj.Replace(".", "").Replace("/", "").Replace("-", "").Contains(normalizedCnpjQuery)) ||
            (x.WmcCode != null && x.WmcCode.ToLower().Contains(normalizedQuery)));

        return new SellerCustomerListViewModel
        {
            Query = query,
            Customers = await customers
                .OrderBy(x => x.TradeName)
                .ThenBy(x => x.Id)
                .Take(20)
                .Select(ToRowExpression())
                .ToListAsync(cancellationToken)
        };
    }

    public async Task<SellerCustomerSearchPage?> SearchCustomersForOrderDraftAsync(
        ClaimsPrincipal user,
        string? query,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var representative = await accessService.GetActiveRepresentativeAsync(user, cancellationToken);
        if (representative is null)
        {
            return null;
        }

        page = Math.Clamp(page, 1, 100_000_000);
        const int pageSize = 20;
        if (string.IsNullOrWhiteSpace(query))
        {
            return new SellerCustomerSearchPage(null, 1, pageSize, 0, []);
        }

        var customers = GetPortfolioQuery(representative.Id);
        if (!string.IsNullOrWhiteSpace(query))
        {
            query = query.Trim();
            var normalizedCnpjQuery = new string(query.Where(char.IsLetterOrDigit).ToArray());
            customers = customers.Where(x =>
                x.TradeName.Contains(query) ||
                x.LegalName.Contains(query) ||
                (normalizedCnpjQuery.Length > 0 && x.Cnpj.Replace(".", "").Replace("/", "").Replace("-", "").Contains(normalizedCnpjQuery)) ||
                (x.WmcCode != null && x.WmcCode.Contains(query)));
        }

        var totalCount = await customers.CountAsync(cancellationToken);
        var results = await customers
            .OrderBy(x => x.TradeName)
            .ThenBy(x => x.Id)
            .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
            .Take(pageSize)
            .Select(ToRowExpression())
            .ToListAsync(cancellationToken);

        return new SellerCustomerSearchPage(query, page, pageSize, totalCount, results);
    }

    public async Task<IReadOnlyList<CustomerSearchSuggestion>?> SearchCustomerSuggestionsAsync(
        ClaimsPrincipal user,
        string? query,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        var representative = await accessService.GetActiveRepresentativeAsync(user, cancellationToken);
        if (representative is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        query = query.Trim().ToLowerInvariant();
        var normalizedCnpjQuery = new string(query.Where(char.IsLetterOrDigit).ToArray());
        var limit = Math.Clamp(maxResults, 1, 20);

        return await GetPortfolioQuery(representative.Id)
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
            .SingleOrDefaultAsync(x => x.Id == customerId, cancellationToken);
        if (customer is null)
        {
            return null;
        }

        var eligibility = paymentEligibilityService is null
            ? null
            : await paymentEligibilityService.GetAvailablePaymentOptionsAsync(customerId, cancellationToken);
        return new SellerCustomerWorkspaceViewModel
        {
            Customer = ToRow(customer),
            SalesRepresentativeName = customer.SalesRepresentative?.Name ?? "Vendedor",
            PaymentTerms = eligibility?.PaymentMethods.Select(term => term.Name).ToList() ?? []
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

public sealed record SellerCustomerSearchPage(
    string? Query,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<SellerCustomerRow> Customers)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
