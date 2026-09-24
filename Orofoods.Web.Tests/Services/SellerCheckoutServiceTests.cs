using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Services.Sellers;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class SellerCheckoutServiceTests
{
    [Fact]
    public async Task Checkout_returns_customer_and_eligible_payment_terms_for_portfolio_customer()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var fixture = await CheckoutFixture.CreateAsync(db, withCart: true);
        var result = await fixture.Service.GetAsync(SellerPrincipal(fixture.Seller.Id), fixture.Customer.Id, fixture.Session);

        Assert.NotNull(result);
        Assert.Equal(fixture.Customer.TradeName, result!.CustomerName);
        Assert.NotEmpty(result.PaymentTerms);
        Assert.NotEmpty(result.Cart.Items);
    }

    [Fact]
    public async Task Checkout_rejects_customer_outside_portfolio_and_empty_cart()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var fixture = await CheckoutFixture.CreateAsync(db, withCart: false);
        var otherRepresentative = new SalesRepresentative { Name = "Outro", IsActive = true };
        var other = new Customer { LegalName = "Outro", TradeName = "Outro", Cnpj = "22.222.222/0001-22", Status = CustomerStatus.Approved, IsActive = true, SalesRepresentative = otherRepresentative };
        db.AddRange(otherRepresentative, other);
        await db.SaveChangesAsync();

        Assert.Null(await fixture.Service.GetAsync(SellerPrincipal(fixture.Seller.Id), other.Id, fixture.Session));
        var empty = await fixture.Service.GetAsync(SellerPrincipal(fixture.Seller.Id), fixture.Customer.Id, fixture.Session);
        Assert.Empty(empty!.Cart.Items);
    }

    private static ClaimsPrincipal SellerPrincipal(string userId) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, "Vendedor")], "test"));

    private sealed class CheckoutFixture
    {
        public required SellerCheckoutService Service { get; init; }
        public required ApplicationUser Seller { get; init; }
        public required Customer Customer { get; init; }
        public required TestSession Session { get; init; }

        public static async Task<CheckoutFixture> CreateAsync(Orofoods.Web.Data.ApplicationDbContext db, bool withCart)
        {
            var representative = new SalesRepresentative { Name = "Vendedor", IsActive = true };
            var seller = new ApplicationUser { Id = "seller", UserName = "seller", IsActive = true, SalesRepresentative = representative };
            var customer = new Customer { LegalName = "Cliente", TradeName = "Cliente", Cnpj = "11.111.111/0001-11", Status = CustomerStatus.Approved, IsActive = true, SalesRepresentative = representative, MinimumOrder = 1m };
            customer.Addresses.Add(new CustomerAddress { Label = "Principal", City = "Campinas", State = "SP", IsActive = true });
            var term = new PaymentTerm { Code = "PIX", Name = "PIX", IsActive = true, DaysUntilDue = 0 };
            var product = new Product { Sku = "CHECKOUT", Name = "Produto", Brand = "Orofoods", BasePrice = 20m, MinimumCases = 1, IsActive = true, IsAvailable = true, ProductCategory = new ProductCategory { Name = "Categoria", Slug = "categoria" } };
            db.AddRange(representative, seller, customer, term, product, new ProductInventory { Product = product, QuantityOnHand = 10 });
            await db.SaveChangesAsync();

            var session = new TestSession();
            var priceService = new PriceService(db);
            var cartService = new CartService(db, priceService);
            if (withCart)
            {
                await cartService.AddAsync(customer.Id, product.Id, 1, session, CartScope.ForSeller(representative.Id, customer.Id));
            }

            return new CheckoutFixture
            {
                Service = new SellerCheckoutService(db, new SalesRepresentativeAccessService(db), cartService, new PaymentEligibilityService(db, Microsoft.Extensions.Options.Options.Create(new PaymentEligibilityOptions()))),
                Seller = seller,
                Customer = customer,
                Session = session
            };
        }
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> values = [];
        public IEnumerable<string> Keys => values.Keys;
        public string Id => "checkout-test";
        public bool IsAvailable => true;
        public void Clear() => values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
    }
}