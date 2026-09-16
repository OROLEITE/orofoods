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

    private static CustomerDashboardService CreateService(
        Orofoods.Web.Data.ApplicationDbContext db,
        DateTimeOffset now)
    {
        var priceService = new PriceService(db);
        var frequentProductService = new FrequentProductService(db, priceService);
        return new CustomerDashboardService(db, frequentProductService, new FixedTimeProvider(now));
    }

    private static async Task<Customer> AddCustomerAsync(Orofoods.Web.Data.ApplicationDbContext db)
    {
        var customer = new Customer
        {
            LegalName = "Cliente sem pedidos Ltda",
            TradeName = "Cliente sem pedidos",
            Cnpj = "12.345.678/0001-00",
            Status = CustomerStatus.Approved,
            IsActive = true
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer;
    }

    private static Order NewOrder(int customerId, DateTime createdAt, decimal total) => new()
    {
        CustomerId = customerId,
        Number = $"ORO-{createdAt:yyyyMMdd}",
        CreatedByUserId = "user-1",
        CreatedAt = createdAt,
        Status = OrderStatus.Received,
        Subtotal = total,
        Total = total
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
