using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Services.Pricing;

namespace Orofoods.Web.Services.Orders;

public sealed record FrequentProduct(Product Product, int OrderedQuantity, decimal CurrentPrice);

public class FrequentProductService(ApplicationDbContext db, PriceService priceService)
{
    public async Task<List<FrequentProduct>> GetAsync(int customerId, int take = 4, CancellationToken cancellationToken = default)
    {
        var quantities = await db.OrderItems
            .Where(x => x.Order!.CustomerId == customerId && x.Product!.IsActive)
            .GroupBy(x => x.ProductId)
            .Select(x => new { ProductId = x.Key, Quantity = x.Sum(item => item.Quantity) })
            .OrderByDescending(x => x.Quantity)
            .ThenBy(x => x.ProductId)
            .Take(take)
            .ToListAsync(cancellationToken);

        var products = await db.Products
            .Where(x => quantities.Select(item => item.ProductId).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var prices = await priceService.GetPricesAsync(customerId, products.Keys, cancellationToken);

        return quantities
            .Where(x => products.ContainsKey(x.ProductId))
            .Select(x => new FrequentProduct(
                products[x.ProductId],
                x.Quantity,
                prices.GetValueOrDefault(x.ProductId, products[x.ProductId].PromotionalPrice ?? products[x.ProductId].BasePrice)))
            .ToList();
    }
}
