using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Payments;
using Orofoods.Web.Tests.Infrastructure;
using Microsoft.Extensions.Options;

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
        var sut = CreateService(db, new FixedTimeProvider(changedAt), new OrderReservationService(db));

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
        var sut = CreateService(db, TimeProvider.System, new OrderReservationService(db));

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
        var sut = CreateService(db, TimeProvider.System, reservationService);

        await sut.UpdateStatusAsync(order.Id, OrderStatus.Cancelled, user.Id);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(0, inventory.QuantityReserved);
        Assert.Equal(0m, customer.CreditUsed);
        Assert.Equal(InventoryReservationStatus.Released, Assert.Single(db.InventoryReservations).Status);
    }

    [Fact]
    public async Task Cannot_cancel_order_after_WMC_export_has_been_claimed()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        var user = new ApplicationUser { Id = "buyer-processing", UserName = "processing@test", Email = "processing@test" };
        var order = new Order
        {
            Customer = customer,
            CreatedByUser = user,
            Number = "ORO-2026-000009",
            Status = OrderStatus.Approved,
            IntegrationStatus = Orofoods.Web.Models.Integrations.IntegrationStatus.Processing
        };
        db.Add(order);
        await db.SaveChangesAsync();
        var sut = CreateService(db, TimeProvider.System, new OrderReservationService(db));

        var result = await sut.UpdateStatusAsync(order.Id, OrderStatus.Cancelled, user.Id);

        Assert.False(result.Succeeded);
        Assert.Equal("O pedido está sendo enviado ao WMC e não pode ser cancelado agora.", result.ErrorMessage);
        Assert.Equal(OrderStatus.Approved, order.Status);
        Assert.Equal(Orofoods.Web.Models.Integrations.IntegrationStatus.Processing, order.IntegrationStatus);
        Assert.Empty(db.OrderStatusHistories);
    }

    [Fact]
    public async Task CardOnDelivery_PendingPayment_CannotBeDelivered()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (customer, user, order) = await CreateOrderAsync(db, "CARD_ON_DELIVERY", PaymentStatus.Pending);
        var sut = CreateService(db, TimeProvider.System, new OrderReservationService(db));

        var result = await sut.UpdateStatusAsync(order.Id, OrderStatus.Delivered, user.Id);

        Assert.False(result.Succeeded);
        Assert.Equal("O pedido utiliza Cartão na Entrega e o pagamento ainda não foi aprovado.", result.ErrorMessage);
        Assert.Equal(OrderStatus.Received, order.Status);
        Assert.Empty(db.OrderStatusHistories);
        Assert.Equal(customer.Id, order.CustomerId);
    }

    [Fact]
    public async Task CardOnDelivery_WithoutPayment_CannotBeDelivered()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (_, user, order) = await CreateOrderAsync(db, "CARD_ON_DELIVERY", paymentStatus: null);
        var sut = CreateService(db, TimeProvider.System, new OrderReservationService(db));

        var result = await sut.UpdateStatusAsync(order.Id, OrderStatus.Delivered, user.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(OrderStatus.Received, order.Status);
        Assert.Empty(db.OrderStatusHistories);
    }

    [Fact]
    public async Task CardOnDelivery_ApprovedPayment_CanBeDelivered()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (_, user, order) = await CreateOrderAsync(db, "CARD_ON_DELIVERY", PaymentStatus.Approved);
        var sut = CreateService(db, TimeProvider.System, new OrderReservationService(db));

        var result = await sut.UpdateStatusAsync(order.Id, OrderStatus.Delivered, user.Id);

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal(OrderStatus.Delivered, Assert.Single(db.OrderStatusHistories).Status);
    }

    [Fact]
    public async Task CardOnDelivery_PaidPayment_CanBeDelivered()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (_, user, order) = await CreateOrderAsync(db, "CARD_ON_DELIVERY", PaymentStatus.Paid);
        var sut = CreateService(db, TimeProvider.System, new OrderReservationService(db));

        var result = await sut.UpdateStatusAsync(order.Id, OrderStatus.Delivered, user.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(OrderStatus.Delivered, order.Status);
    }

    [Theory]
    [InlineData("CASH")]
    [InlineData("PIX")]
    public async Task OtherPaymentTerms_CanBeDeliveredWithoutCardApproval(string termCode)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (_, user, order) = await CreateOrderAsync(db, termCode, paymentStatus: null);
        var sut = CreateService(db, TimeProvider.System, new OrderReservationService(db));

        var result = await sut.UpdateStatusAsync(order.Id, OrderStatus.Delivered, user.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(OrderStatus.Delivered, order.Status);
    }

    [Fact]
    public async Task PendingCardOnDelivery_CanStillBeCancelled()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (_, user, order) = await CreateOrderAsync(db, "CARD_ON_DELIVERY", PaymentStatus.Pending);
        var sut = CreateService(db, TimeProvider.System, new OrderReservationService(db));

        var result = await sut.UpdateStatusAsync(order.Id, OrderStatus.Cancelled, user.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    private static async Task<(Customer Customer, ApplicationUser User, Order Order)> CreateOrderAsync(
        ApplicationDbContext db,
        string termCode,
        PaymentStatus? paymentStatus)
    {
        var customer = new Customer { LegalName = "Teste Ltda", TradeName = "Teste", Cnpj = "12.345.678/0001-99", Status = CustomerStatus.Approved, IsActive = true };
        var user = new ApplicationUser { Id = "buyer-1", UserName = "buyer@test", Email = "buyer@test" };
        var term = new PaymentTerm { Code = termCode, Name = termCode, DaysUntilDue = 0, IsActive = true };
        var order = new Order
        {
            Customer = customer,
            CreatedByUser = user,
            PaymentTerm = term,
            PaymentMethod = termCode,
            Number = "ORO-2026-000001",
            Status = OrderStatus.Received,
            Total = 100m
        };
        if (paymentStatus is not null)
        {
            order.Payments.Add(new Payment
            {
                Customer = customer,
                PaymentMethod = termCode,
                Method = PaymentMethodType.CardOnDelivery,
                Amount = order.Total,
                Status = paymentStatus.Value
            });
        }
        db.Add(order);
        await db.SaveChangesAsync();
        return (customer, user, order);
    }

    private static AdminOrderService CreateService(ApplicationDbContext db, TimeProvider timeProvider, OrderReservationService reservationService)
    {
        var eligibilityOptions = Options.Create(new PaymentEligibilityOptions());
        return new AdminOrderService(
            db,
            timeProvider,
            reservationService,
            new PaymentEligibilityService(db, eligibilityOptions),
            new PaymentService(db, eligibilityOptions, new PendingBoletoProvider()));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
