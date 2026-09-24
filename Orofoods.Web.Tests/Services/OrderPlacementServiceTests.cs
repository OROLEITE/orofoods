using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class OrderPlacementServiceTests
{
    [Fact]
    public async Task PlaceAsync_creates_a_received_order_with_current_price_and_history()
    {
        await using var fixture = await PlacementFixture.CreateAsync(availableQuantity: 10);
        var result = await fixture.Service.PlaceAsync(
            fixture.Customer.Id,
            fixture.SellerUser.Id,
            new(fixture.Address.Id, fixture.PaymentTerm.Id, DateTime.UtcNow.AddDays(1), "Observacao"),
            fixture.Session,
            fixture.Scope);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        Assert.Equal(OrderStatus.Received, result.Order!.Status);
        Assert.Equal(fixture.SellerUser.Id, result.Order.CreatedByUserId);
        Assert.Equal(fixture.CurrentPrice, Assert.Single(result.Order.Items).UnitPrice);
        Assert.Equal(OrderStatus.Received, Assert.Single(result.Order.StatusHistory).Status);
        Assert.False(fixture.Session.TryGetValue("orofoods-cart-product-ids:seller:7:customer:1", out _));
    }

    [Fact]
    public async Task PlaceAsync_rejects_a_scope_for_a_different_customer_without_persisting()
    {
        await using var fixture = await PlacementFixture.CreateAsync(availableQuantity: 10);

        var result = await fixture.Service.PlaceAsync(
            fixture.Customer.Id,
            fixture.SellerUser.Id,
            new(fixture.Address.Id, fixture.PaymentTerm.Id, DateTime.UtcNow.AddDays(1), null),
            fixture.Session,
            CartScope.ForSeller(7, fixture.Customer.Id + 1));

        Assert.False(result.Succeeded);
        Assert.Empty(await fixture.Db.Orders.ToListAsync());
    }

    [Fact]
    public async Task PlaceAsync_rolls_back_when_inventory_reservation_fails()
    {
        await using var fixture = await PlacementFixture.CreateAsync(availableQuantity: 0);
        fixture.Session.SetString("orofoods-cart-product-ids:seller:7:customer:1", JsonSerializer.Serialize(new Dictionary<int, int> { [fixture.Product.Id] = 1 }));

        var result = await fixture.Service.PlaceAsync(
            fixture.Customer.Id,
            fixture.SellerUser.Id,
            new(fixture.Address.Id, fixture.PaymentTerm.Id, DateTime.UtcNow.AddDays(1), null),
            fixture.Session,
            fixture.Scope);

        Assert.False(result.Succeeded);
        Assert.Empty(await fixture.Db.Orders.ToListAsync());
        Assert.Empty(await fixture.Db.InventoryReservations.ToListAsync());
    }

    [Fact]
    public async Task PlaceAsync_rejects_an_unapproved_customer()
    {
        await using var fixture = await PlacementFixture.CreateAsync(availableQuantity: 10);
        fixture.Customer.Status = CustomerStatus.Pending;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.PlaceAsync(
            fixture.Customer.Id,
            fixture.SellerUser.Id,
            new(fixture.Address.Id, fixture.PaymentTerm.Id, DateTime.UtcNow.AddDays(1), null),
            fixture.Session,
            fixture.Scope);

        Assert.False(result.Succeeded);
        Assert.Empty(await fixture.Db.Orders.ToListAsync());
    }

    private sealed class PlacementFixture : IAsyncDisposable
    {
        public required Orofoods.Web.Data.ApplicationDbContext Db { get; init; }
        public required OrderPlacementService Service { get; init; }
        public required Customer Customer { get; init; }
        public required ApplicationUser SellerUser { get; init; }
        public required CustomerAddress Address { get; init; }
        public required PaymentTerm PaymentTerm { get; init; }
        public required Product Product { get; init; }
        public required decimal CurrentPrice { get; init; }
        public required TestSession Session { get; init; }
        public required CartScope Scope { get; init; }

        public static async Task<PlacementFixture> CreateAsync(int availableQuantity)
        {
            var db = await TestDbContextFactory.CreateAsync();
            var representative = new SalesRepresentative { Id = 7, Name = "Vendedor", IsActive = true };
            var customer = new Customer { Id = 1, LegalName = "Cliente", TradeName = "Cliente", Cnpj = "11.111.111/0001-11", Status = CustomerStatus.Approved, IsActive = true, MinimumOrder = 1m, CreditLimit = 1000m, SalesRepresentative = representative };
            var address = new CustomerAddress { Label = "Principal", Street = "Rua A", Number = "1", District = "Centro", City = "Campinas", State = "SP", ZipCode = "13000-000", IsActive = true, IsPrimary = true, Customer = customer };
            var term = new PaymentTerm { Code = "PIX", Name = "PIX", DaysUntilDue = 0, IsActive = true };
            var product = new Product { Sku = "PLACE-001", Name = "Produto", Brand = "Orofoods", Unit = "caixa", BasePrice = 99m, MinimumCases = 1, IsActive = true, IsAvailable = true, ProductCategory = new ProductCategory { Name = "Categoria", Slug = "categoria" } };
            var seller = new ApplicationUser { Id = "seller", UserName = "seller", Email = "seller@test.local", IsActive = true, SalesRepresentative = representative };
            db.AddRange(representative, customer, address, term, product, seller, new ProductInventory { Product = product, QuantityOnHand = availableQuantity });
            await db.SaveChangesAsync();

            var session = new TestSession();
            var scope = CartScope.ForSeller(representative.Id, customer.Id);
            session.SetString("orofoods-cart-product-ids:seller:7:customer:1", JsonSerializer.Serialize(new Dictionary<int, int> { [product.Id] = 1 }));
            return new PlacementFixture
            {
                Db = db,
                Service = new OrderPlacementService(db, new CartService(db, new Orofoods.Web.Services.Pricing.PriceService(db)), new Orofoods.Web.Services.Pricing.PriceService(db), new PaymentEligibilityService(db, Options.Create(new PaymentEligibilityOptions())), new OrderReservationService(db)),
                Customer = customer,
                SellerUser = seller,
                Address = address,
                PaymentTerm = term,
                Product = product,
                CurrentPrice = product.BasePrice,
                Session = session,
                Scope = scope
            };
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> values = [];
        public IEnumerable<string> Keys => values.Keys;
        public string Id => "placement-test";
        public bool IsAvailable => true;
        public void Clear() => values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
    }
}