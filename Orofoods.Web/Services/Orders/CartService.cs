using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Services.Orders;

public class CartService(ApplicationDbContext db, PriceService priceService)
{
    private const string BaseSessionKey = "orofoods-cart-product-ids";

    public async Task<CartViewModel> GetAsync(int customerId, ISession session, CartScope? scope = null)
    {
        var quantities = Read(session, scope);
        var customer = await db.Customers.AsNoTracking().SingleAsync(x => x.Id == customerId);
        var products = await db.Products.AsNoTracking().Where(x => quantities.Keys.Contains(x.Id) && x.IsActive).ToListAsync();
        var inventories = await db.ProductInventories.AsNoTracking()
            .Where(x => quantities.Keys.Contains(x.ProductId))
            .ToDictionaryAsync(x => x.ProductId);
        var prices = await priceService.GetPricesAsync(customerId, products.Select(x => x.Id));
        return new CartViewModel
        {
            MinimumOrder = customer.MinimumOrder,
            Items = products.OrderBy(x => x.Name).Select(product =>
            {
                var quantity = Math.Max(quantities[product.Id], product.MinimumCases);
                var isAvailable = product.IsAvailable && inventories.TryGetValue(product.Id, out var inventory) && inventory.AvailableQuantity >= quantity;
                return new CartLineViewModel(product.Id, product.Sku, product.Name, product.UnitDescription,
                    quantity, product.MinimumCases, prices.GetValueOrDefault(product.Id, product.BasePrice), isAvailable);
            }).ToList()
        };
    }

    public async Task AddAsync(int customerId, int productId, int quantity, ISession session, CartScope? scope = null)
    {
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == productId && x.IsActive && x.IsAvailable)
            ?? throw new InvalidOperationException("Produto indisponível.");
        var quantities = Read(session, scope);
        var inventory = await db.ProductInventories.AsNoTracking().SingleOrDefaultAsync(x => x.ProductId == productId);
        if (inventory is null || inventory.AvailableQuantity < Math.Max(quantity, product.MinimumCases))
        {
            throw new InvalidOperationException("Produto indispon\u00edvel no estoque atual.");
        }

        quantities[productId] = Math.Max(quantity, product.MinimumCases);
        Write(session, quantities, scope);
    }

    public void Update(int productId, int quantity, ISession session, CartScope? scope = null)
    {
        var quantities = Read(session, scope);
        if (quantity <= 0) quantities.Remove(productId); else quantities[productId] = quantity;
        Write(session, quantities, scope);
    }

    public void Remove(int productId, ISession session, CartScope? scope = null)
    {
        var quantities = Read(session, scope);
        quantities.Remove(productId);
        Write(session, quantities, scope);
    }

    public void Clear(ISession session, CartScope? scope = null) => session.Remove(GetSessionKey(scope));

    public async Task ReplaceAsync(int customerId, IEnumerable<SavedOrderLine> lines, ISession session, CartScope? scope = null)
    {
        var productIds = lines.Select(x => x.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(x => productIds.Contains(x.Id) && x.IsActive && x.IsAvailable).ToDictionaryAsync(x => x.Id);
        var inventories = await db.ProductInventories.AsNoTracking()
            .Where(x => productIds.Contains(x.ProductId))
            .ToDictionaryAsync(x => x.ProductId);
        var quantities = lines.Where(x => products.ContainsKey(x.ProductId) && x.Quantity > 0)
            .GroupBy(x => x.ProductId)
            .Select(group => new { ProductId = group.Key, Quantity = Math.Max(group.Sum(item => item.Quantity), products[group.Key].MinimumCases) })
            .Where(item => inventories.TryGetValue(item.ProductId, out var inventory) && inventory.AvailableQuantity >= item.Quantity)
            .ToDictionary(item => item.ProductId, item => item.Quantity);
        Write(session, quantities, scope);
    }

    private static Dictionary<int, int> Read(ISession session, CartScope? scope) => session.GetString(GetSessionKey(scope)) is { Length: > 0 } value
        ? JsonSerializer.Deserialize<Dictionary<int, int>>(value) ?? [] : [];
    private static void Write(ISession session, Dictionary<int, int> quantities, CartScope? scope) => session.SetString(GetSessionKey(scope), JsonSerializer.Serialize(quantities));
    private static string GetSessionKey(CartScope? scope) => string.IsNullOrEmpty(scope?.SessionSuffix)
        ? BaseSessionKey
        : $"{BaseSessionKey}:{scope.SessionSuffix}";
}
