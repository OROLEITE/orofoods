using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Pricing;

namespace Orofoods.Web.Services.Sellers;

public sealed class SellerCatalogService(
    ApplicationDbContext db,
    SalesRepresentativeAccessService accessService,
    PriceService priceService)
{
    public const int MaximumPageSize = 20;

    public async Task<SellerCatalogViewModel?> GetCatalogAsync(
        ClaimsPrincipal user,
        int customerId,
        string? query,
        CancellationToken cancellationToken = default,
        int? page = null,
        int pageSize = MaximumPageSize)
    {
        var scope = await accessService.GetSellerCartScopeAsync(user, customerId, cancellationToken);
        if (scope is null)
        {
            return null;
        }

        var customer = await db.Customers.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == customerId && x.IsActive && x.Status == Models.Customers.CustomerStatus.Approved, cancellationToken);
        if (customer is null)
        {
            return null;
        }

        var isPagedSearch = page.HasValue;
        var currentPage = Math.Max(page ?? 1, 1);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
        IQueryable<Models.Catalog.Product> productsQuery = BuildSelectableProductsQuery();

        if (!string.IsNullOrWhiteSpace(query))
        {
            query = query.Trim();
            productsQuery = productsQuery.Where(x =>
                x.Name.Contains(query) ||
                x.Sku.Contains(query) ||
                x.Description.Contains(query) ||
                (isPagedSearch && x.Ean != null && x.Ean.Contains(query)));
        }

        var totalCount = await productsQuery.CountAsync(cancellationToken);
        IQueryable<Models.Catalog.Product> orderedProducts = productsQuery
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Sku);
        if (isPagedSearch)
        {
            var skip = (int)Math.Min((long)(currentPage - 1) * pageSize, int.MaxValue);
            orderedProducts = orderedProducts.Skip(skip).Take(pageSize);
        }
        var products = await orderedProducts.Include(x => x.Images).ToListAsync(cancellationToken);
        var prices = await priceService.GetPricesAsync(customerId, products.Select(x => x.Id), cancellationToken);

        return new SellerCatalogViewModel
        {
            CustomerId = customerId,
            CustomerName = customer.TradeName,
            Query = query,
            Page = currentPage,
            PageSize = isPagedSearch ? pageSize : Math.Max(1, totalCount),
            TotalCount = totalCount,
            TotalPages = !isPagedSearch ? (totalCount == 0 ? 0 : 1)
                : totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize),
            Products = ToRows(products, prices)
        };
    }

    public async Task<IReadOnlyList<SellerCatalogProductRow>?> GetSelectableProductsByIdsAsync(
        ClaimsPrincipal user,
        int customerId,
        IEnumerable<int> productIds,
        CancellationToken cancellationToken = default)
    {
        var scope = await accessService.GetSellerCartScopeAsync(user, customerId, cancellationToken);
        if (scope is null)
        {
            return null;
        }

        var customerIsValid = await db.Customers.AsNoTracking().AnyAsync(x =>
            x.Id == customerId && x.IsActive && x.Status == Models.Customers.CustomerStatus.Approved,
            cancellationToken);
        if (!customerIsValid)
        {
            return null;
        }

        var requestedIds = productIds.Where(id => id > 0).Distinct().Take(200).ToArray();
        if (requestedIds.Length == 0)
        {
            return [];
        }

        var products = await BuildSelectableProductsQuery()
            .Where(product => requestedIds.Contains(product.Id))
            .Include(product => product.Images)
            .ToListAsync(cancellationToken);
        var prices = await priceService.GetPricesAsync(customerId, products.Select(x => x.Id), cancellationToken);
        var rows = ToRows(products, prices).ToDictionary(row => row.Id);
        return requestedIds.Where(rows.ContainsKey).Select(id => rows[id]).ToArray();
    }

    public async Task<IReadOnlySet<int>?> GetSelectableProductIdsAsync(
        ClaimsPrincipal user,
        int customerId,
        IEnumerable<int> productIds,
        CancellationToken cancellationToken = default)
    {
        var scope = await accessService.GetSellerCartScopeAsync(user, customerId, cancellationToken);
        if (scope is null)
        {
            return null;
        }

        var customerIsValid = await db.Customers.AsNoTracking().AnyAsync(x =>
            x.Id == customerId && x.IsActive && x.Status == Models.Customers.CustomerStatus.Approved,
            cancellationToken);
        if (!customerIsValid)
        {
            return null;
        }

        var requestedIds = productIds.Where(id => id > 0).Distinct().ToArray();
        if (requestedIds.Length == 0)
        {
            return new HashSet<int>();
        }

        var selectableIds = await BuildSelectableProductsQuery()
            .Where(product => requestedIds.Contains(product.Id))
            .Select(product => product.Id)
            .ToListAsync(cancellationToken);
        return selectableIds.ToHashSet();
    }

    private IQueryable<Models.Catalog.Product> BuildSelectableProductsQuery() => db.Products.AsNoTracking()
        .Where(x =>
            x.IsActive && x.IsAvailable && (x.WmcCode == null || (x.IsWmcActive && x.WmcStockAvailable && x.WmcInitialLoadReady)) &&
            db.ProductInventories.Any(inventory =>
                inventory.ProductId == x.Id &&
                inventory.QuantityOnHand - inventory.QuantityReserved >= x.MinimumCases));

    private static IReadOnlyList<SellerCatalogProductRow> ToRows(
        IEnumerable<Models.Catalog.Product> products,
        IReadOnlyDictionary<int, decimal> prices) => products.Select(product => new SellerCatalogProductRow(
            product.Id,
            product.Sku,
            product.Name,
            product.Description,
            product.UnitDescription,
            prices.GetValueOrDefault(product.Id, product.BasePrice),
            product.MinimumCases,
            product.Images.OrderByDescending(image => image.IsPrimary).ThenBy(image => image.SortOrder).Select(image => (int?)image.Id).FirstOrDefault(),
            product.Ean)).ToArray();
}
