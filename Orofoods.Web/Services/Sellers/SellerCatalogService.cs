using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
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
    private static readonly MethodInfo StringReplaceMethod = typeof(string).GetMethod(nameof(string.Replace), [typeof(string), typeof(string)])!;
    private static readonly MethodInfo StringContainsMethod = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
    private static readonly MethodInfo StringToLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
    private static readonly MethodInfo TranslateSearchTextMethod = typeof(ApplicationDbContext).GetMethod(nameof(ApplicationDbContext.TranslateSearchText))!;
    private static readonly (string Characters, char Replacement)[] SearchCharacterReplacements =
    [
        ("àáâãäåÀÁÂÃÄÅ", 'a'),
        ("èéêëÈÉÊË", 'e'),
        ("ìíîïÌÍÎÏ", 'i'),
        ("òóôõöøÒÓÔÕÖØ", 'o'),
        ("ùúûüÙÚÛÜ", 'u'),
        ("çÇ", 'c'),
        ("ñÑ", 'n'),
        ("ýÿÝŸ", 'y'),
        ("Æ", 'æ'),
        ("Œ", 'œ'),
        ("ẞ", 'ß')
    ];
    private static readonly (string Character, string Replacement)[] SearchExpansionReplacements =
    [
        ("æ", "ae"), ("œ", "oe"), ("ß", "ss")
    ];
    private static readonly string SearchFromCharacters = string.Concat(SearchCharacterReplacements.Select(group => group.Characters));
    private static readonly string SearchToCharacters = string.Concat(SearchCharacterReplacements.SelectMany(group => group.Characters.Select(_ => group.Replacement)));

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

        query = query?.Trim();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var normalizedQuery = NormalizeSearchTerm(query);
            productsQuery = normalizedQuery.Length == 0
                ? productsQuery.Where(_ => false)
                : productsQuery.Where(CreateProductSearchPredicate(normalizedQuery));
        }

        var totalCount = await productsQuery.CountAsync(cancellationToken);
        IQueryable<Models.Catalog.Product> orderedProducts = productsQuery
            .OrderBy(product => product.Name)
            .ThenBy(product => product.Sku);
        if (isPagedSearch)
        {
            var skip = (int)Math.Min((long)(currentPage - 1) * pageSize, int.MaxValue);
            orderedProducts = orderedProducts.Skip(skip).Take(pageSize);
        }
        var products = await orderedProducts.Include(product => product.Images).ToListAsync(cancellationToken);
        var prices = await priceService.GetPricesAsync(customerId, products.Select(x => x.Id), cancellationToken);
        var availableQuantities = await GetAvailableQuantitiesAsync(products.Select(product => product.Id), cancellationToken);

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
            Products = ToRows(products, prices, availableQuantities)
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
        var availableQuantities = await GetAvailableQuantitiesAsync(products.Select(product => product.Id), cancellationToken);
        var rows = ToRows(products, prices, availableQuantities).ToDictionary(row => row.Id);
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
        .Where(x => x.IsActive && x.BasePrice > 0m);

    internal static Expression<Func<Models.Catalog.Product, bool>> CreateProductSearchPredicate(string searchTerm)
    {
        var product = Expression.Parameter(typeof(Models.Catalog.Product), "product");
        Expression? predicate = null;
        foreach (var propertyName in new[]
        {
            nameof(Models.Catalog.Product.Name),
            nameof(Models.Catalog.Product.Sku),
            nameof(Models.Catalog.Product.Brand),
            nameof(Models.Catalog.Product.WmcCode),
            nameof(Models.Catalog.Product.Ean),
            nameof(Models.Catalog.Product.Ean13),
            nameof(Models.Catalog.Product.Dun14),
            nameof(Models.Catalog.Product.Description)
        })
        {
            var field = Expression.Property(product, propertyName);
            var fieldIsNotNull = Expression.NotEqual(field, Expression.Constant(null, typeof(string)));
            var normalizedField = NormalizeDatabaseSearchText(field);
            var containsSearchTerm = Expression.Call(normalizedField, StringContainsMethod, Expression.Constant(searchTerm));
            var fieldMatches = Expression.AndAlso(fieldIsNotNull, containsSearchTerm);
            predicate = predicate is null ? fieldMatches : Expression.OrElse(predicate, fieldMatches);
        }

        return Expression.Lambda<Func<Models.Catalog.Product, bool>>(predicate!, product);
    }

    private static Expression NormalizeDatabaseSearchText(Expression field)
    {
        Expression normalized = Expression.Call(
            TranslateSearchTextMethod,
            field,
            Expression.Constant(SearchFromCharacters),
            Expression.Constant(SearchToCharacters));
        normalized = Expression.Call(normalized, StringToLowerMethod);
        foreach (var (character, replacement) in SearchExpansionReplacements)
        {
            normalized = Expression.Call(
                normalized,
                StringReplaceMethod,
                Expression.Constant(character),
                Expression.Constant(replacement));
        }

        return normalized;
    }

    private static string NormalizeSearchTerm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var decomposed = value.Trim().Normalize(System.Text.NormalizationForm.FormD);
        var withoutDiacritics = new string(decomposed
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        return withoutDiacritics.Normalize(System.Text.NormalizationForm.FormC)
            .ToLowerInvariant()
            .Replace("æ", "ae", StringComparison.Ordinal)
            .Replace("œ", "oe", StringComparison.Ordinal)
            .Replace("ß", "ss", StringComparison.Ordinal);
    }

    private async Task<IReadOnlyDictionary<int, int>> GetAvailableQuantitiesAsync(
        IEnumerable<int> productIds,
        CancellationToken cancellationToken)
    {
        var ids = productIds.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<int, int>();

        var quantities = await db.ProductInventories.AsNoTracking()
            .Where(inventory => ids.Contains(inventory.ProductId))
            .Select(inventory => new
            {
                inventory.ProductId,
                AvailableQuantity = inventory.QuantityOnHand - inventory.QuantityReserved
            })
            .ToListAsync(cancellationToken);
        return quantities.ToDictionary(
            inventory => inventory.ProductId,
            inventory => Math.Max(0, inventory.AvailableQuantity));
    }

    private static IReadOnlyList<SellerCatalogProductRow> ToRows(
        IEnumerable<Models.Catalog.Product> products,
        IReadOnlyDictionary<int, decimal> prices,
        IReadOnlyDictionary<int, int> availableQuantities) => products.Select(product => new SellerCatalogProductRow(
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
            product.WmcCode,
            product.Brand,
            product.EffectiveSalesUnitCode,
            availableQuantities.GetValueOrDefault(product.Id))).ToArray();
}
