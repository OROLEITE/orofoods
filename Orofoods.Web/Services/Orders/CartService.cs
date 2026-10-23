using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Services.Orders;

public class CartService(ApplicationDbContext db, PriceService priceService)
{
    private const string BaseSessionKey = "orofoods-cart-v2-product-ids";

    public async Task<CartViewModel> GetAsync(int customerId, ISession session, CartScope? scope = null)
    {
        await MigrateLegacyCartAsync(session, scope);
        var quantities = Read(session, scope);
        var customer = await db.Customers.AsNoTracking().SingleAsync(x => x.Id == customerId);
        var products = await db.Products.AsNoTracking().Where(x => quantities.Keys.Contains(x.Id) && x.IsActive).ToListAsync();
        var prices = await priceService.GetPricesAsync(customerId, products.Select(x => x.Id));
        return new CartViewModel
        {
            MinimumOrder = OrderMinimumPolicy.Resolve(
                await db.OrderMinimumSettings.AsNoTracking().Select(x => (decimal?)x.MinimumAmount).SingleOrDefaultAsync(),
                customer.MinimumOrder),
            Items = products.OrderBy(x => x.Name).Select(product =>
            {
                var minimumQuantity = product.MinimumSalesQuantity;
                var quantity = Math.Max(quantities[product.Id], minimumQuantity);
                var isAvailable = product.IsActive;
                return new CartLineViewModel(product.Id, product.Sku, product.Name, product.UnitDescription,
                    quantity, minimumQuantity, prices.GetValueOrDefault(product.Id, product.BasePrice), isAvailable)
                {
                    PriceUnitLabel = product.PriceUnitLabel
                };
            }).ToList()
        };
    }

    public Task AddAsync(int customerId, int productId, int quantity, ISession session, CartScope? scope = null) =>
        AddCoreAsync(customerId, productId, quantity, session, scope, accumulate: false);

    public Task AddToCartAsync(int customerId, int productId, int quantity, ISession session) =>
        AddCoreAsync(customerId, productId, quantity, session, scope: null, accumulate: true);

    private async Task AddCoreAsync(int customerId, int productId, int quantity, ISession session, CartScope? scope, bool accumulate)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Informe uma quantidade válida.");
        }

        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == productId && x.IsActive)
            ?? throw new InvalidOperationException("Produto indisponível.");
        var resolvedPrice = await priceService.GetPriceAsync(customerId, productId);
        if (resolvedPrice?.EffectivePrice is not > 0m)
        {
            throw new InvalidOperationException("Produto sem preço válido para este cliente.");
        }

        await MigrateLegacyCartAsync(session, scope);
        var quantities = Read(session, scope);
        var selectedQuantity = Math.Max(quantity, product.MinimumSalesQuantity);
        quantities[productId] = accumulate
            ? quantities.GetValueOrDefault(productId) + selectedQuantity
            : selectedQuantity;
        Write(session, quantities, scope);
    }

    public void Update(int productId, int quantity, ISession session, CartScope? scope = null)
    {
        var quantities = Read(session, scope);
        if (quantity <= 0) quantities.Remove(productId); else quantities[productId] = quantity;
        Write(session, quantities, scope);
    }

    public async Task UpdateAsync(int customerId, int productId, int quantity, ISession session, CartScope? scope = null)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Informe uma quantidade válida.");
        }

        var product = await db.Products.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == productId && x.IsActive)
            ?? throw new InvalidOperationException("Produto indisponível.");
        await MigrateLegacyCartAsync(session, scope);
        Update(productId, quantity, session, scope);
    }

    public void Remove(int productId, ISession session, CartScope? scope = null)
    {
        var quantities = Read(session, scope);
        quantities.Remove(productId);
        Write(session, quantities, scope);
    }

    public void Clear(ISession session, CartScope? scope = null)
    {
        session.Remove(GetSessionKey(scope));
        session.Remove(GetLegacySessionKey(scope));
    }

    public async Task ReplaceAsync(int customerId, IEnumerable<SavedOrderLine> lines, ISession session, CartScope? scope = null)
    {
        await MigrateLegacyCartAsync(session, scope);
        var productIds = lines.Select(x => x.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(x => productIds.Contains(x.Id)         && x.IsActive).ToDictionaryAsync(x => x.Id);
               var quantities = lines.Where(x => products.ContainsKey(x.ProductId) && x.Quantity > 0)
            .GroupBy(x => x.ProductId)
            .Select(group => new { ProductId = group.Key, Quantity = Math.Max(group.Sum(item => item.Quantity), products[group.Key].MinimumSalesQuantity) })
            .ToDictionary(item => item.ProductId, item => item.Quantity);
        Write(session, quantities, scope);
    }

    private static Dictionary<int, int> Read(ISession session, CartScope? scope) => session.GetString(GetSessionKey(scope)) is { Length: > 0 } value
        ? JsonSerializer.Deserialize<Dictionary<int, int>>(value) ?? [] : [];
    private static void Write(ISession session, Dictionary<int, int> quantities, CartScope? scope) => session.SetString(GetSessionKey(scope), JsonSerializer.Serialize(quantities));
    private static string GetSessionKey(CartScope? scope) => string.IsNullOrEmpty(scope?.SessionSuffix)
        ? BaseSessionKey
        : $"{BaseSessionKey}:{scope.SessionSuffix}";

    private async Task MigrateLegacyCartAsync(ISession session, CartScope? scope)
    {
        var currentKey = GetSessionKey(scope);
        if (session.GetString(currentKey) is not null) return;
        var legacyKey = GetLegacySessionKey(scope);
        if (session.GetString(legacyKey) is not { Length: > 0 } serialized) return;

        var quantities = JsonSerializer.Deserialize<Dictionary<int, int>>(serialized) ?? [];
        var packageUnitProductIds = await db.Products.AsNoTracking()
            .Where(product => quantities.Keys.Contains(product.Id) && product.CommercialUnitCode != null)
            .Select(product => product.Id)
            .ToListAsync();
        foreach (var productId in packageUnitProductIds) quantities.Remove(productId);

        Write(session, quantities, scope);
        session.Remove(legacyKey);
    }

    private static string GetLegacySessionKey(CartScope? scope) => string.IsNullOrEmpty(scope?.SessionSuffix)
        ? "orofoods-cart-product-ids"
        : $"orofoods-cart-product-ids:{scope.SessionSuffix}";
}
