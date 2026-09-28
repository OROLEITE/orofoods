using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Sellers;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class SellerOrderHistoryServiceTests
{
    [Fact]
    public async Task History_returns_only_portfolio_orders_newest_first()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var fixture = await OrderFixture.CreateAsync(db);
        db.Orders.AddRange(
            Order(fixture.Customer.Id, "ORO-001", DateTime.UtcNow.AddDays(-2), 100m),
            Order(fixture.Customer.Id, "ORO-002", DateTime.UtcNow.AddDays(-1), 200m),
            Order(fixture.OtherCustomer.Id, "ORO-003", DateTime.UtcNow, 300m));
        await db.SaveChangesAsync();

        var result = await fixture.Service.GetHistoryAsync(Seller(fixture.User.Id), fixture.Customer.Id);

        Assert.Equal(["ORO-002", "ORO-001"], result!.Orders.Select(x => x.Number));
    }

    [Fact]
    public async Task Detail_rejects_order_from_other_customer_and_includes_items_and_payment()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var fixture = await OrderFixture.CreateAsync(db);
        var own = Order(fixture.Customer.Id, "ORO-010", DateTime.UtcNow, 150m);
        own.Items.Add(new OrderItem { ProductNameSnapshot = "Pão", SkuSnapshot = "PAO", Quantity = 2, UnitPrice = 75m, Subtotal = 150m });
        own.Payments.Add(new Payment { CustomerId = fixture.Customer.Id, PaymentMethod = "PIX", Method = PaymentMethodType.Pix, Amount = 150m, Status = PaymentStatus.Approved });
        var other = Order(fixture.OtherCustomer.Id, "ORO-011", DateTime.UtcNow, 50m);
        db.Orders.AddRange(own, other);
        await db.SaveChangesAsync();

        var detail = await fixture.Service.GetDetailsAsync(Seller(fixture.User.Id), fixture.Customer.Id, own.Id);
        var forbidden = await fixture.Service.GetDetailsAsync(Seller(fixture.User.Id), fixture.Customer.Id, other.Id);

        Assert.NotNull(detail);
        Assert.Equal(150m, detail!.Total);
        Assert.Single(detail.Items);
        Assert.Equal(PaymentStatus.Approved, detail.PaymentStatus);
        Assert.Null(forbidden);
    }

    [Fact]
    public async Task History_defaults_to_ten_and_supports_page_size_twenty()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var fixture = await OrderFixture.CreateAsync(db);
        db.Orders.AddRange(Enumerable.Range(1, 25).Select(index => Order(fixture.Customer.Id, $"ORO-{index:000}", DateTime.UtcNow.AddMinutes(-index), index)));
        await db.SaveChangesAsync();

        var defaultPage = await fixture.Service.GetHistoryAsync(Seller(fixture.User.Id), fixture.Customer.Id);
        var pageTwenty = await fixture.Service.GetHistoryAsync(Seller(fixture.User.Id), fixture.Customer.Id, pageSize: 20);

        Assert.Equal(10, defaultPage!.PageSize);
        Assert.Equal(10, defaultPage.Orders.Count);
        Assert.Equal(20, pageTwenty!.PageSize);
        Assert.Equal(20, pageTwenty.Orders.Count);
        Assert.Equal(25, pageTwenty.TotalItems);
    }

    [Fact]
    public async Task History_applies_filter_before_paging_and_clamps_invalid_values()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var fixture = await OrderFixture.CreateAsync(db);
        db.Orders.AddRange(Enumerable.Range(1, 12).Select(index => Order(fixture.Customer.Id, index % 2 == 0 ? $"PIX-{index:000}" : $"BOLETO-{index:000}", DateTime.UtcNow.AddMinutes(-index), index)));
        await db.SaveChangesAsync();

        var result = await fixture.Service.GetHistoryAsync(Seller(fixture.User.Id), fixture.Customer.Id, query: "BOLETO", page: 2, pageSize: 99);

        Assert.Equal(10, result!.PageSize);
        Assert.Equal(6, result.TotalItems);
        Assert.Equal(6, result.Orders.Count);
        Assert.All(result.Orders, order => Assert.StartsWith("BOLETO-", order.Number));
        Assert.Equal(1, result.Page);
    }

    private static Order Order(int customerId, string number, DateTime createdAt, decimal total) => new()
    {
        CustomerId = customerId,
        Number = number,
        CreatedAt = createdAt,
        ConfirmedAt = createdAt,
        Status = OrderStatus.Invoiced,
        Total = total,
        Subtotal = total,
        PaymentMethod = "PIX"
    };

    private static ClaimsPrincipal Seller(string userId) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, "Vendedor")], "test"));

    private sealed class OrderFixture
    {
        public required SellerOrderHistoryService Service { get; init; }
        public required ApplicationUser User { get; init; }
        public required Customer Customer { get; init; }
        public required Customer OtherCustomer { get; init; }

        public static async Task<OrderFixture> CreateAsync(Orofoods.Web.Data.ApplicationDbContext db)
        {
            var representative = new SalesRepresentative { Name = "Vendedor", IsActive = true };
            var otherRepresentative = new SalesRepresentative { Name = "Outro", IsActive = true };
            var user = new ApplicationUser { Id = "seller", UserName = "seller", IsActive = true, SalesRepresentative = representative };
            var customer = new Customer { LegalName = "Cliente A", TradeName = "Cliente A", Cnpj = "11.111.111/0001-11", Status = CustomerStatus.Approved, IsActive = true, SalesRepresentative = representative };
            var other = new Customer { LegalName = "Cliente B", TradeName = "Cliente B", Cnpj = "22.222.222/0001-22", Status = CustomerStatus.Approved, IsActive = true, SalesRepresentative = otherRepresentative };
            db.AddRange(representative, otherRepresentative, user, customer, other);
            await db.SaveChangesAsync();
            return new OrderFixture { Service = new SellerOrderHistoryService(db, new SalesRepresentativeAccessService(db)), User = user, Customer = customer, OtherCustomer = other };
        }
    }
}