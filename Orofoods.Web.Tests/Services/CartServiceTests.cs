using Microsoft.AspNetCore.Http;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class CartServiceTests
{
    [Fact]
    public async Task AddAsync_rejects_product_without_available_inventory()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var product = await AddProductAsync(db, 0);
        var service = new CartService(db, new PriceService(db));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddAsync(1, product.Id, 1, new TestSession()));

        Assert.Contains("Produto indispon", exception.Message);
    }

    [Fact]
    public async Task GetAsync_marks_cart_line_unavailable_when_reserved_inventory_uses_all_stock()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var product = await AddProductAsync(db, 2, 2);
        var session = new TestSession();
        session.SetString("orofoods-cart-product-ids", $"{{\"{product.Id}\":1}}");

        var cart = await new CartService(db, new PriceService(db)).GetAsync(customer.Id, session);

        Assert.False(Assert.Single(cart.Items).IsAvailable);
        Assert.Equal(0m, cart.Subtotal);
    }

    private static async Task<Product> AddProductAsync(Orofoods.Web.Data.ApplicationDbContext db, int quantityOnHand, int quantityReserved = 0)
    {
        var product = new Product
        {
            Sku = Guid.NewGuid().ToString(),
            Name = "Pao",
            Brand = "Bimbo",
            BasePrice = 10m,
            ProductCategory = new ProductCategory { Name = "Congelados", Slug = Guid.NewGuid().ToString() }
        };
        db.Add(product);
        await db.SaveChangesAsync();
        db.ProductInventories.Add(new ProductInventory { ProductId = product.Id, QuantityOnHand = quantityOnHand, QuantityReserved = quantityReserved });
        await db.SaveChangesAsync();
        return product;
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> values = [];
        public IEnumerable<string> Keys => values.Keys;
        public string Id => "test";
        public bool IsAvailable => true;
        public void Clear() => values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
    }
}
