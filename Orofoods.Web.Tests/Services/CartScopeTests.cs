using Microsoft.AspNetCore.Http;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class CartScopeTests
{
    [Fact]
    public void Seller_scope_is_stable_for_the_same_seller_and_customer()
    {
        Assert.Equal(CartScope.ForSeller(7, 11), CartScope.ForSeller(7, 11));
        Assert.NotEqual(CartScope.ForSeller(7, 11), CartScope.ForSeller(7, 12));
        Assert.NotEqual(CartScope.ForSeller(7, 11), CartScope.ForSeller(8, 11));
    }

    [Fact]
    public async Task Seller_carts_are_isolated_by_seller_and_customer()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customerA = new Customer { LegalName = "Cliente A", TradeName = "Cliente A", Cnpj = "11.111.111/0001-11" };
        var customerB = new Customer { LegalName = "Cliente B", TradeName = "Cliente B", Cnpj = "22.222.222/0001-22" };
        var product = new Product
        {
            Sku = "SCOPE-001",
            Name = "Produto de escopo",
            Brand = "Orofoods",
            BasePrice = 10m,
            MinimumCases = 1,
            IsActive = true,
            IsAvailable = true,
            ProductCategory = new ProductCategory { Name = "Categoria", Slug = "categoria" }
        };
        db.AddRange(customerA, customerB, product);
        await db.SaveChangesAsync();
        db.ProductInventories.Add(new ProductInventory { ProductId = product.Id, QuantityOnHand = 20 });
        await db.SaveChangesAsync();

        var session = new TestSession();
        var service = new CartService(db, new PriceService(db));
        await service.AddAsync(customerA.Id, product.Id, 1, session, CartScope.ForSeller(7, customerA.Id));

        Assert.Single((await service.GetAsync(customerA.Id, session, CartScope.ForSeller(7, customerA.Id))).Items);
        Assert.Empty((await service.GetAsync(customerB.Id, session, CartScope.ForSeller(7, customerB.Id))).Items);
        Assert.Empty((await service.GetAsync(customerA.Id, session, CartScope.ForSeller(8, customerA.Id))).Items);
        Assert.Empty((await service.GetAsync(customerA.Id, session, CartScope.CustomerSelfService)).Items);
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> values = [];
        public IEnumerable<string> Keys => values.Keys;
        public string Id => "cart-scope-test";
        public bool IsAvailable => true;
        public void Clear() => values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
    }
}