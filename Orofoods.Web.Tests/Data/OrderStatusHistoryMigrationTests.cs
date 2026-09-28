using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Tests.Data;

public class OrderStatusHistoryMigrationTests
{
    [Fact]
    public async Task Current_schema_stores_order_status_history_with_integration_fields()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var customer = new Customer
        {
            LegalName = "Cliente legado Ltda",
            TradeName = "Cliente legado",
            Cnpj = "98.765.432/0001-00",
            Status = CustomerStatus.Approved,
            IsActive = true
        };
        var user = new ApplicationUser
        {
            Id = "legacy-user",
            UserName = "legacy@orofoods.local",
            NormalizedUserName = "LEGACY@OROFOODS.LOCAL",
            Email = "legacy@orofoods.local",
            NormalizedEmail = "LEGACY@OROFOODS.LOCAL",
            IsActive = true
        };
        var createdAt = new DateTime(2026, 8, 20, 14, 0, 0, DateTimeKind.Utc);
        var order = new Order
        {
            Customer = customer,
            CreatedByUser = user,
            Number = "ORO-2026-000777",
            Status = OrderStatus.Picking,
            CreatedAt = createdAt
        };
        order.StatusHistory.Add(new OrderStatusHistory
        {
            Status = order.Status,
            ChangedAt = createdAt,
            ChangedByUser = user
        });
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var history = await db.OrderStatusHistories.SingleAsync();
        Assert.Equal(order.Id, history.OrderId);
        Assert.Equal(OrderStatus.Picking, history.Status);
        Assert.Equal(createdAt, history.ChangedAt);
        Assert.Equal(user.Id, history.ChangedByUserId);
    }
}
