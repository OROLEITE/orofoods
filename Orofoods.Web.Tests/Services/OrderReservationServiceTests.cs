using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class OrderReservationServiceTests
{
    [Fact]
    public async Task ReserveAsync_AcceptsOrder_WhenInventoryIsInsufficientOrMissing()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99", CreditLimit = 1000m };
        var product = CreateProduct();
        var noInventoryProduct = CreateProduct();
        noInventoryProduct.Sku = "SEM-ESTOQUE";
        var order = new Order { Customer = customer, CreatedByUser = CreateUser(), Total = 100m, PaymentMethod = "14 dias" };
        order.Items.Add(new OrderItem { Product = product, Quantity = 3, UnitPrice = 10m, Subtotal = 30m });
        order.Items.Add(new OrderItem { Product = noInventoryProduct, Quantity = 2, UnitPrice = 10m, Subtotal = 20m });
        db.AddRange(order, new ProductInventory { Product = product, QuantityOnHand = 0 });
        await db.SaveChangesAsync();

        var result = await new OrderReservationService(db).ReserveAsync(order);

        Assert.True(result.IsValid);
        Assert.All(db.ProductInventories, x => Assert.InRange(x.QuantityReserved, 0, x.QuantityOnHand));
        Assert.Equal(0, (await db.ProductInventories.SingleAsync()).QuantityReserved);

        await new OrderReservationService(db).ReleaseAsync(order);

        Assert.All(db.ProductInventories, x => Assert.Equal(0, x.QuantityReserved));
    }

    [Fact]
    public async Task ReserveAsync_ReservesInventoryAndCredit_ForTermPayment()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99", CreditLimit = 1000m };
        var product = CreateProduct();
        var order = new Order { Customer = customer, CreatedByUser = CreateUser(), Total = 100m, PaymentMethod = "14 dias" };
        order.Items.Add(new OrderItem { Product = product, Quantity = 3, UnitPrice = 10m, Subtotal = 30m });
        var inventory = new ProductInventory { Product = product, QuantityOnHand = 5 };
        db.AddRange(order, inventory);
        await db.SaveChangesAsync();

        var result = await new OrderReservationService(db).ReserveAsync(order);

        Assert.True(result.IsValid);
        Assert.Equal(3, inventory.QuantityReserved);
        Assert.Equal(100m, customer.CreditUsed);
        Assert.Single(db.InventoryReservations);
    }

    [Fact]
    public async Task ReserveAsync_DoesNotUseCredit_ForPix()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99", CreditLimit = 0m };
        var product = CreateProduct();
        var order = new Order { Customer = customer, CreatedByUser = CreateUser(), Total = 100m, PaymentMethod = "PIX" };
        order.Items.Add(new OrderItem { Product = product, Quantity = 1, UnitPrice = 100m, Subtotal = 100m });
        db.AddRange(order, new ProductInventory { Product = product, QuantityOnHand = 1 });
        await db.SaveChangesAsync();

        var result = await new OrderReservationService(db).ReserveAsync(order);

        Assert.True(result.IsValid);
        Assert.Equal(0m, customer.CreditUsed);
    }

    [Fact]
    public async Task ReserveAsync_DoesNotUseCredit_ForCreditCard()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99", CreditLimit = 0m };
        var product = CreateProduct();
        var order = new Order
        {
            Customer = customer,
            CreatedByUser = CreateUser(),
            Total = 100m,
            PaymentMethod = "Cartão de crédito",
            PaymentTerm = new PaymentTerm { Code = "CREDIT_CARD", Name = "Cartão de crédito", DaysUntilDue = 0 }
        };
        order.Items.Add(new OrderItem { Product = product, Quantity = 1, UnitPrice = 100m, Subtotal = 100m });
        db.AddRange(order, new ProductInventory { Product = product, QuantityOnHand = 1 });
        await db.SaveChangesAsync();

        var result = await new OrderReservationService(db).ReserveAsync(order);

        Assert.True(result.IsValid);
        Assert.Equal(0m, customer.CreditUsed);
        Assert.Single(db.InventoryReservations);
    }

    private static ApplicationUser CreateUser() => new()
    {
        Id = Guid.NewGuid().ToString(),
        UserName = "buyer@test.local",
        Email = "buyer@test.local"
    };

    private static Product CreateProduct() => new()
    {
        Sku = Guid.NewGuid().ToString(),
        Name = "Pao",
        Brand = "Bimbo",
        ProductCategory = new ProductCategory { Name = "Congelados", Slug = Guid.NewGuid().ToString() }
    };
}
