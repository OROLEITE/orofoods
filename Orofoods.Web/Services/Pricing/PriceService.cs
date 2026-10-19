using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;

namespace Orofoods.Web.Services.Pricing;

public class PriceService(ApplicationDbContext db)
{
    public async Task<ResolvedPrice?> GetPriceAsync(int customerId, int productId, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == customerId, cancellationToken);
        var product = await db.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == productId, cancellationToken);

        if (customer is null || product is null)
        {
            return null;
        }

        var priceTableItem = customer.PriceTableId is null
            ? null
            : await db.PriceTableItems
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.PriceTableId == customer.PriceTableId.Value && x.ProductId == productId,
                    cancellationToken);

        var isProvisionalBimboPrice = priceTableItem is null
            && string.Equals(product.Brand, "BIMBO", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(product.WmcCode)
            && product.BasePrice == 1.00m
            && product.PromotionalPrice is null;
        var effectivePrice = isProvisionalBimboPrice ? 0m : priceTableItem?.PromotionalPrice
            ?? priceTableItem?.Price
            ?? product.PromotionalPrice
            ?? product.BasePrice;

        return new ResolvedPrice(product.BasePrice, product.PromotionalPrice, effectivePrice, !isProvisionalBimboPrice && effectivePrice > 0m);
    }

    public async Task<Dictionary<int, decimal>> GetPricesAsync(
        int customerId,
        IEnumerable<int> productIds,
        CancellationToken cancellationToken = default)
    {
        var prices = new Dictionary<int, decimal>();

        foreach (var productId in productIds.Distinct())
        {
            var resolved = await GetPriceAsync(customerId, productId, cancellationToken);
            if (resolved is not null)
            {
                prices[productId] = resolved.EffectivePrice;
            }
        }

        return prices;
    }
}

public sealed record ResolvedPrice(decimal BasePrice, decimal? PromotionalPrice, decimal EffectivePrice, bool IsCommerciallyValid = true);
