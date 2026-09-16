using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class AdminOrderServiceTests
{
    [Fact]
    public async Task Updates_order_status_owned_by_the_server()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Burger House Ltda", TradeName = "Burger House", Cnpj = "12.345.678/0001-99", Status = CustomerStatus.Approved, IsActive = true };
        var user = new ApplicationUser { Id = "buyer-1", UserName = "buyer@test", Email = "buyer@test" };
        var order = new Order { Customer = customer, CreatedByUser = user, Number = "ORO-2026-000001", Status = OrderStatus.Received };
        db.Add(order);
        await db.SaveChangesAsync();
        var changedAt = new DateTimeOffset(2026, 8, 30, 3, 0, 0, TimeSpan.Zero);
        var sut = new AdminOrderService(db, new FixedTimeProvider(changedAt), new OrderReservationService(db));

        await sut.UpdateStatusAsync(order.Id, OrderStatus.Approved, user.Id);

        Assert.Equal(OrderStatus.Approved, order.Status);
        var history = Assert.Single(db.OrderStatusHistories);
        Assert.Equal(OrderStatus.Approved, history.Status);
        Assert.Equal(user.Id, history.ChangedByUserId);
        Assert.Equal(changedAt.UtcDateTime, history.ChangedAt);
    }

    [Fact]
    public async Task Does_not_duplicate_history_when_status_does_not_change()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Burger House Ltda", TradeName = "Burger House", Cnpj = "12.345.678/0001-99", Status = CustomerStatus.Approved, IsActive = true };
        var user = new ApplicationUser { Id = "buyer-1", UserName = "buyer@test", Email = "buyer@test" };
        var order = new Order { Customer = customer, CreatedByUser = user, Number = "ORO-2026-000001", Status = OrderStatus.Received };
        db.Add(order);
        await db.SaveChangesAsync();
        var sut = new AdminOrderService(db, TimeProvider.System, new OrderReservationService(db));

        await sut.UpdateStatusAsync(order.Id, OrderStatus.Received, user.Id);

        Assert.Empty(db.OrderStatusHistories);
    }

    [Fact]
    public async Task Cancelling_order_releases_inventory_and_credit()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Burger House Ltda", TradeName = "Burger House", Cnpj = "12.345.678/0001-99", CreditLimit = 1000m };
        var user = new ApplicationUser { Id = "buyer-1", UserName = "buyer@test", Email = "buyer@test" };
        var product = new Product
        {
            Sku = "BIM-001",
            Name = "Pao",
            Brand = "Bimbo",
            ProductCategory = new ProductCategory { Name = "Congelados", Slug = "congelados" }
        };
        var order = new Order { Customer = customer, CreatedByUser = user, Number = "ORO-2026-000001", Status = OrderStatus.Received, PaymentMethod = "14 dias", Total = 100m };
        order.Items.Add(new OrderItem { Product = product, Quantity = 3, UnitPrice = 100m, Subtotal = 100m });
        var inventory = new ProductInventory { Product = product, QuantityOnHand = 5 };
        db.AddRange(order, inventory);
        await db.SaveChangesAsync();
        var reservationService = new OrderReservationService(db);
        Assert.True((await reservationService.ReserveAsync(order)).IsValid);
        var sut = new AdminOrderService(db, TimeProvider.System, reservationService);

        await sut.UpdateStatusAsync(order.Id, OrderStatus.Cancelled, user.Id);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(0, inventory.QuantityReserved);
        Assert.Equal(0m, customer.CreditUsed);
        Assert.Equal(InventoryReservationStatus.Released, Assert.Single(db.InventoryReservations).Status);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
