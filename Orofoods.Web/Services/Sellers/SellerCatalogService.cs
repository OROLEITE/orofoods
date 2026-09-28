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
    public async Task<SellerCatalogViewModel?> GetCatalogAsync(
        ClaimsPrincipal user,
        int customerId,
        string? query,
        CancellationToken cancellationToken = default)
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

        var productsQuery = db.Products.AsNoTracking()
            .Include(x => x.Images)
            .Where(x =>
                x.IsActive &&
                x.IsAvailable &&
                db.ProductInventories.Any(inventory =>
                    inventory.ProductId == x.Id &&
                    inventory.QuantityOnHand - inventory.QuantityReserved >= x.MinimumCases));

        if (!string.IsNullOrWhiteSpace(query))
        {
            query = query.Trim();
            productsQuery = productsQuery.Where(x =>
                x.Name.Contains(query) ||
                x.Sku.Contains(query) ||
                x.Description.Contains(query));
        }

        var products = await productsQuery.OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var prices = await priceService.GetPricesAsync(customerId, products.Select(x => x.Id), cancellationToken);

        return new SellerCatalogViewModel
        {
            CustomerId = customerId,
            CustomerName = customer.TradeName,
            Query = query,
            Products = products.Select(product => new SellerCatalogProductRow(
                product.Id,
                product.Sku,
                product.Name,
                product.Description,
                product.UnitDescription,
                prices.GetValueOrDefault(product.Id, product.BasePrice),
                product.MinimumCases,
                product.Images
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.SortOrder)
                    .Select(image => (int?)image.Id)
                    .FirstOrDefault())).ToList()
        };
    }
}