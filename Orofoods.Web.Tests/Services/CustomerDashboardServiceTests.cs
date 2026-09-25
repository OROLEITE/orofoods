using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class CustomerDashboardServiceTests
{
    [Fact]
    public async Task Customer_without_orders_receives_an_empty_dashboard()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = await AddCustomerAsync(db);
        var sut = CreateService(db, new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero));

        var result = await sut.GetAsync(customer);

        Assert.Null(result.LastOrder);
        Assert.Equal(0, result.OpenOrders);
        Assert.Equal(0m, result.MonthTotal);
        Assert.Equal(0, result.MonthOrderCount);
        Assert.Equal(0m, result.AverageTicket);
        Assert.Equal(6, result.MonthlyPurchases.Count);
        Assert.All(result.MonthlyPurchases, month => { Assert.Equal(0m, month.Total); Assert.Equal(0, month.OrderCount); });
        Assert.Empty(result.RecentOrders);
    }

    [Fact]
    public async Task Month_total_uses_the_current_month_and_year()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = await AddCustomerAsync(db);
        db.Users.Add(new ApplicationUser
        {
            Id = "user-1",
            UserName = "compras@cliente.local",
            Email = "compras@cliente.local",
            CustomerId = customer.Id,
            IsActive = true
        });
        await db.SaveChangesAsync();
        db.Orders.AddRange(
            NewOrder(customer.Id, new DateTime(2026, 8, 5, 10, 0, 0, DateTimeKind.Utc), 120m),
            NewOrder(customer.Id, new DateTime(2025, 8, 5, 10, 0, 0, DateTimeKind.Utc), 900m),
            NewOrder(customer.Id, new DateTime(2026, 7, 31, 10, 0, 0, DateTimeKind.Utc), 300m));
        await db.SaveChangesAsync();
        var sut = CreateService(db, new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero));

        var result = await sut.GetAsync(customer);

        Assert.Equal(120m, result.MonthTotal);
    }

    [Fact]
    public async Task Dashboard_metrics_cover_six_months_and_only_this_customers_non_draft_purchases()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = await AddCustomerAsync(db);
        var otherCustomer = await AddCustomerAsync(db, "98.765.432/0001-10");
        db.Orders.AddRange(
            NewOrder(customer.Id, new DateTime(2026, 8, 5, 10, 0, 0, DateTimeKind.Utc), 120m),
            NewOrder(customer.Id, new DateTime(2026, 8, 9, 10, 0, 0, DateTimeKind.Utc), 60m, OrderStatus.Delivered),
            NewOrder(customer.Id, new DateTime(2026, 7, 31, 10, 0, 0, DateTimeKind.Utc), 300m, OrderStatus.Approved),
            NewOrder(customer.Id, new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc), 40m, OrderStatus.Delivered),
            NewOrder(customer.Id, new DateTime(2026, 2, 28, 10, 0, 0, DateTimeKind.Utc), 900m, OrderStatus.Delivered),
            NewOrder(customer.Id, new DateTime(2026, 8, 10, 10, 0, 0, DateTimeKind.Utc), 700m, OrderStatus.Cancelled),
            NewOrder(customer.Id, new DateTime(2026, 8, 11, 10, 0, 0, DateTimeKind.Utc), 800m, OrderStatus.Draft),
            NewOrder(otherCustomer.Id, new DateTime(2026, 8, 12, 10, 0, 0, DateTimeKind.Utc), 10000m, OrderStatus.Delivered));
        await db.SaveChangesAsync();
        var sut = CreateService(db, new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero));

        var result = await sut.GetAsync(customer);

        Assert.Equal(180m, result.MonthTotal);
        Assert.Equal(2, result.MonthOrderCount);
        Assert.Equal(90m, result.AverageTicket);
        Assert.Collection(result.MonthlyPurchases,
            month => { Assert.Equal(new DateTime(2026, 3, 1), month.Month); Assert.Equal(40m, month.Total); Assert.Equal(1, month.OrderCount); },
            month => { Assert.Equal(new DateTime(2026, 4, 1), month.Month); Assert.Equal(0m, month.Total); Assert.Equal(0, month.OrderCount); },
            month => { Assert.Equal(new DateTime(2026, 5, 1), month.Month); Assert.Equal(0m, month.Total); Assert.Equal(0, month.OrderCount); },
            month => { Assert.Equal(new DateTime(2026, 6, 1), month.Month); Assert.Equal(0m, month.Total); Assert.Equal(0, month.OrderCount); },
            month => { Assert.Equal(new DateTime(2026, 7, 1), month.Month); Assert.Equal(300m, month.Total); Assert.Equal(1, month.OrderCount); },
            month => { Assert.Equal(new DateTime(2026, 8, 1), month.Month); Assert.Equal(180m, month.Total); Assert.Equal(2, month.OrderCount); });
        Assert.Equal(5, result.RecentOrders.Count);
        Assert.All(result.RecentOrders, order => Assert.Equal(customer.Id, order.CustomerId));
        Assert.Equal("ORO-20260811", result.LastOrder?.Number);
    }

    private static CustomerDashboardService CreateService(
        Orofoods.Web.Data.ApplicationDbContext db,
        DateTimeOffset now)
    {
        var priceService = new PriceService(db);
        var frequentProductService = new FrequentProductService(db, priceService);
        return new CustomerDashboardService(db, frequentProductService, new FixedTimeProvider(now));
    }

    private static async Task<Customer> AddCustomerAsync(Orofoods.Web.Data.ApplicationDbContext db, string cnpj = "12.345.678/0001-00")
    {
        var customer = new Customer
        {
            LegalName = "Cliente sem pedidos Ltda",
            TradeName = "Cliente sem pedidos",
            Cnpj = cnpj,
            Status = CustomerStatus.Approved,
            IsActive = true
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer;
    }

    private static Order NewOrder(int customerId, DateTime createdAt, decimal total, OrderStatus status = OrderStatus.Received) => new()
    {
        CustomerId = customerId,
        Number = $"ORO-{createdAt:yyyyMMdd}",
        CreatedByUserId = "user-1",
        CreatedAt = createdAt,
        Status = status,
        Subtotal = total,
        Total = total
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
