using Microsoft.AspNetCore.Http;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class CartServiceTests
{
    [Fact]
    public async Task AddAsync_allows_active_product_with_zero_stock()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var product = await AddProductAsync(db, 0);
        var session = new TestSession();
        var service = new CartService(db, new PriceService(db));

        await service.AddAsync(customer.Id, product.Id, 3, session);

        var line = Assert.Single((await service.GetAsync(customer.Id, session)).Items);
        Assert.True(line.IsAvailable);
        Assert.Equal(3, line.Quantity);
        Assert.Equal(30m, line.Subtotal);
    }

    [Fact]
    public async Task Cart_allows_active_product_without_inventory_record_or_with_pending_wmc_sync()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var withoutInventory = await AddProductAsync(db, 0);
        db.ProductInventories.RemoveRange(db.ProductInventories.Where(x => x.ProductId == withoutInventory.Id));
        var pendingSync = await AddProductAsync(db, 0);
        pendingSync.WmcCode = "503833";
        pendingSync.IsAvailable = false;
        pendingSync.IsWmcActive = false;
        pendingSync.WmcStockAvailable = false;
        pendingSync.WmcInitialLoadReady = false;
        await db.SaveChangesAsync();
        var session = new TestSession();
        var service = new CartService(db, new PriceService(db));

        await service.AddAsync(customer.Id, withoutInventory.Id, 2, session);
        await service.AddAsync(customer.Id, pendingSync.Id, 4, session);
        await service.UpdateAsync(customer.Id, pendingSync.Id, 5, session);

        var cart = await service.GetAsync(customer.Id, session);
        Assert.All(cart.Items, item => Assert.True(item.IsAvailable));
        Assert.Equal(2, cart.Items.Single(x => x.ProductId == withoutInventory.Id).Quantity);
        Assert.Equal(5, cart.Items.Single(x => x.ProductId == pendingSync.Id).Quantity);
        Assert.Equal(70m, cart.Subtotal);
    }

    [Fact]
    public async Task AddAsync_still_rejects_inactive_products()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var product = await AddProductAsync(db, 10);
        product.IsActive = false;
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service().AddAsync(customer.Id, product.Id, 1, new TestSession()));

        Assert.Contains("Produto indispon", exception.Message);
        CartService service() => new(db, new PriceService(db));
    }

    [Fact]
    public async Task AddAsync_rejects_a_product_without_a_positive_resolved_price()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var product = await AddProductAsync(db, 2);
        product.BasePrice = 0m;
        await db.SaveChangesAsync();
        var session = new TestSession();
        var service = new CartService(db, new PriceService(db));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddAsync(customer.Id, product.Id, 1, session));

        Assert.Contains("preço", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(session.TryGetValue("orofoods-cart-v2-product-ids", out _));
    }

    [Fact]
    public async Task GetAsync_keeps_cart_line_available_when_reserved_inventory_uses_all_stock()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var product = await AddProductAsync(db, 2, 2);
        var session = new TestSession();
        session.SetString("orofoods-cart-v2-product-ids", $"{{\"{product.Id}\":1}}");

        var cart = await new CartService(db, new PriceService(db)).GetAsync(customer.Id, session);

        Assert.True(Assert.Single(cart.Items).IsAvailable);
        Assert.Equal(10m, cart.Subtotal);
    }

    [Fact]
    public async Task GetAsync_preserves_legacy_cart_lines_but_drops_lines_that_would_be_reinterpreted_as_packs()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var legacyProduct = await AddProductAsync(db, 20);
        var packageProduct = await AddProductAsync(db, 20);
        packageProduct.CommercialUnitCode = "PCT";
        packageProduct.LogisticsUnitCode = "CX";
        packageProduct.PackagesPerCase = 12;
        await db.SaveChangesAsync();
        var session = new TestSession();
        session.SetString("orofoods-cart-product-ids", $"{{\"{legacyProduct.Id}\":2,\"{packageProduct.Id}\":3}}");

        var cart = await new CartService(db, new PriceService(db)).GetAsync(customer.Id, session);

        var line = Assert.Single(cart.Items);
        Assert.Equal(legacyProduct.Id, line.ProductId);
        Assert.Equal(2, line.Quantity);
        Assert.True(session.TryGetValue("orofoods-cart-v2-product-ids", out _));
        Assert.False(session.TryGetValue("orofoods-cart-product-ids", out _));
    }

    [Fact]
    public async Task AddAsync_keeps_existing_replacement_behavior_for_scoped_callers()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var product = await AddProductAsync(db, 10);
        var session = new TestSession();
        var service = new CartService(db, new PriceService(db));

        await service.AddAsync(customer.Id, product.Id, 1, session);
        await service.AddAsync(customer.Id, product.Id, 3, session);

        var cart = await service.GetAsync(customer.Id, session);
        var line = Assert.Single(cart.Items);
        Assert.Equal(product.Id, line.ProductId);
        Assert.Equal(3, line.Quantity);
    }

    [Fact]
    public async Task AddToCartAsync_sums_the_quantity_for_an_existing_product_without_creating_another_line()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var product = await AddProductAsync(db, 10);
        var session = new TestSession();
        var service = new CartService(db, new PriceService(db));

        await service.AddToCartAsync(customer.Id, product.Id, 1, session);
        await service.AddToCartAsync(customer.Id, product.Id, 3, session);

        var cart = await service.GetAsync(customer.Id, session);
        var line = Assert.Single(cart.Items);
        Assert.Equal(product.Id, line.ProductId);
        Assert.Equal(4, line.Quantity);
    }

    [Fact]
    public async Task GetAsync_applies_the_global_order_minimum_without_changing_customer_settings()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99", MinimumOrder = 75m };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var cart = await new CartService(db, new PriceService(db)).GetAsync(customer.Id, new TestSession());

        Assert.Equal(160m, cart.MinimumOrder);
        Assert.Equal(75m, customer.MinimumOrder);
    }

    [Fact]
    public async Task GetAsync_uses_an_administrator_configured_minimum()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99", MinimumOrder = 75m };
        db.Customers.Add(customer);
        db.OrderMinimumSettings.Add(new OrderMinimumSettings { MinimumAmount = 200m });
        await db.SaveChangesAsync();

        var cart = await new CartService(db, new PriceService(db)).GetAsync(customer.Id, new TestSession());

        Assert.Equal(200m, cart.MinimumOrder);
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
