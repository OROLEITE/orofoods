using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Services.Catalog;
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

        int totalCount;
        List<Models.Catalog.Product> products;
        if (!string.IsNullOrWhiteSpace(query))
        {
            query = query.Trim();
            var normalizedQuery = CatalogSearchText.Normalize(query);
            var candidates = await productsQuery
                .Select(product => new ProductSearchCandidate(
                    product.Id,
                    product.Name,
                    product.Sku,
                    product.Description,
                    product.WmcCode,
                    product.Ean,
                    product.Ean13,
                    product.Dun14))
                .ToListAsync(cancellationToken);
            var matchingIds = candidates
                .Where(candidate => MatchesSearch(candidate, normalizedQuery))
                .OrderBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.Sku, StringComparer.OrdinalIgnoreCase)
                .Select(candidate => candidate.Id)
                .ToArray();

            totalCount = matchingIds.Length;
            var skip = isPagedSearch ? (int)Math.Min((long)(currentPage - 1) * pageSize, int.MaxValue) : 0;
            var pageIds = isPagedSearch ? matchingIds.Skip(skip).Take(pageSize).ToArray() : matchingIds;
            if (pageIds.Length == 0)
            {
                products = [];
            }
            else
            {
                var orderById = pageIds.Select((id, index) => (id, index)).ToDictionary(item => item.id, item => item.index);
                products = await productsQuery
                    .Where(product => pageIds.Contains(product.Id))
                    .Include(product => product.Images)
                    .ToListAsync(cancellationToken);
                products = products
                    .OrderBy(product => orderById.GetValueOrDefault(product.Id, int.MaxValue))
                    .ToList();
            }
        }
        else
        {
            totalCount = await productsQuery.CountAsync(cancellationToken);
            IQueryable<Models.Catalog.Product> orderedProducts = productsQuery
                .OrderBy(product => product.Name)
                .ThenBy(product => product.Sku);
            if (isPagedSearch)
            {
                var skip = (int)Math.Min((long)(currentPage - 1) * pageSize, int.MaxValue);
                orderedProducts = orderedProducts.Skip(skip).Take(pageSize);
            }
            products = await orderedProducts.Include(product => product.Images).ToListAsync(cancellationToken);
        }
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
                inventory.QuantityOnHand - inventory.QuantityReserved >= (x.CommercialUnitCode == "PCT" ? 1 : x.MinimumCases)));

    private static bool MatchesSearch(ProductSearchCandidate product, string normalizedQuery) =>
        CatalogSearchText.Normalize(product.Name).Contains(normalizedQuery, StringComparison.Ordinal) ||
        CatalogSearchText.Normalize(product.Sku).Contains(normalizedQuery, StringComparison.Ordinal) ||
        CatalogSearchText.Normalize(product.Description).Contains(normalizedQuery, StringComparison.Ordinal) ||
        (product.WmcCode is not null && CatalogSearchText.Normalize(product.WmcCode).Contains(normalizedQuery, StringComparison.Ordinal)) ||
        (product.Ean is not null && CatalogSearchText.Normalize(product.Ean).Contains(normalizedQuery, StringComparison.Ordinal)) ||
        (product.Ean13 is not null && CatalogSearchText.Normalize(product.Ean13).Contains(normalizedQuery, StringComparison.Ordinal)) ||
        (product.Dun14 is not null && CatalogSearchText.Normalize(product.Dun14).Contains(normalizedQuery, StringComparison.Ordinal));

    private static IReadOnlyList<SellerCatalogProductRow> ToRows(
        IEnumerable<Models.Catalog.Product> products,
        IReadOnlyDictionary<int, decimal> prices) => products.Select(product => new SellerCatalogProductRow(
            product.Id,
            product.Sku,
            product.Name,
            product.Description,
            product.UnitDescription,
            prices.GetValueOrDefault(product.Id, product.BasePrice),
            product.MinimumSalesQuantity,
            product.Images.OrderByDescending(image => image.IsPrimary).ThenBy(image => image.SortOrder).Select(image => (int?)image.Id).FirstOrDefault(),
            product.Ean,
            product.PriceUnitLabel,
            product.Ean13,
            product.Dun14,
            product.WmcCode)).ToArray();

    private sealed record ProductSearchCandidate(
        int Id,
        string Name,
        string Sku,
        string Description,
        string? WmcCode,
        string? Ean,
        string? Ean13,
        string? Dun14);
}
