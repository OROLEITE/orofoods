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

public class AssistedOrderServiceTests
{
    [Fact]
    public async Task CreateAsync_RepricesLinesAndCreatesNormalOrder()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = CreateCustomer();
        var term = new PaymentTerm { Code = "PIX", Name = "PIX", DaysUntilDue = 0, IsActive = true };
        var product = CreateProduct("VIS-001", "Visconti", 99m);
        db.AddRange(customer, term, product, new ProductInventory { Product = product, QuantityOnHand = 20 }, CreateUser("operator"));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.CreateAsync(customer.Id, "operator", customer.Addresses.Single().Id, term.Id, DateTime.Today.AddDays(1), null, [(product.Id, 3)]);

        Assert.True(result.Succeeded, result.ErrorMessage);
        var order = await db.Orders.Include(x => x.Items).SingleAsync();
        Assert.Equal("operator", order.CreatedByUserId);
        Assert.Single(order.Items);
        Assert.Equal(3, order.Items[0].Quantity);
        Assert.Equal(99m, order.Items[0].UnitPrice);
        Assert.Equal(297m, order.Total);
    }

    [Fact]
    public async Task CreateAsync_UsesCurrentPriceInsteadOfSubmittedPrice()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = CreateCustomer();
        var table = new PriceTable { Name = "Cliente", IsActive = true };
        var term = new PaymentTerm { Code = "PIX", Name = "PIX", DaysUntilDue = 0, IsActive = true };
        var product = CreateProduct("VIS-002", "Visconti Brioche", 100m);
        db.AddRange(customer, table, term, product, new PriceTableItem { PriceTable = table, Product = product, Price = 42m }, new ProductInventory { Product = product, QuantityOnHand = 20 }, CreateUser("operator"));
        customer.PriceTable = table;
        await db.SaveChangesAsync();

        var result = await CreateService(db).CreateAsync(customer.Id, "operator", customer.Addresses.Single().Id, term.Id, DateTime.Today.AddDays(1), null, [(product.Id, 2)]);

        Assert.True(result.Succeeded, result.ErrorMessage);
        var item = await db.OrderItems.SingleAsync();
        Assert.Equal(42m, item.UnitPrice);
        Assert.Equal(84m, item.Subtotal);
    }

    [Fact]
    public async Task CreateAsync_RejectsTermBeforeThreeValidPurchases()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = CreateCustomer();
        var term = new PaymentTerm { Code = "NET14", Name = "Boleto 14 dias", DaysUntilDue = 14, IsActive = true };
        var product = CreateProduct("VIS-003", "Visconti", 10m);
        db.AddRange(customer, term, product, new ProductInventory { Product = product, QuantityOnHand = 20 }, CreateUser("operator"));
        await db.SaveChangesAsync();

        var result = await CreateService(db).CreateAsync(customer.Id, "operator", customer.Addresses.Single().Id, term.Id, DateTime.Today.AddDays(1), null, [(product.Id, 1)]);

        Assert.False(result.Succeeded);
        Assert.Contains("três primeiras", result.ErrorMessage);
        Assert.Empty(db.Orders);
    }

    [Fact]
    public async Task CreateAsync_RejectsCreditWhenCustomerIsBlocked()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = CreateCustomer();
        customer.CreditBlocked = true;
        var term = new PaymentTerm { Code = "NET14", Name = "Boleto 14 dias", DaysUntilDue = 14, IsActive = true };
        var product = CreateProduct("VIS-004", "Visconti", 10m);
        db.AddRange(customer, term, product, new ProductInventory { Product = product, QuantityOnHand = 20 }, CreateUser("operator"));
        await db.SaveChangesAsync();

        var result = await CreateService(db).CreateAsync(customer.Id, "operator", customer.Addresses.Single().Id, term.Id, DateTime.Today.AddDays(1), null, [(product.Id, 1)]);

        Assert.False(result.Succeeded);
        Assert.Empty(db.Orders);
    }

    private static AssistedOrderService CreateService(Orofoods.Web.Data.ApplicationDbContext db)
    {
        var options = Options.Create(new PaymentEligibilityOptions());
        return new AssistedOrderService(db, new PriceService(db), new PaymentEligibilityService(db, options), new OrderReservationService(db));
    }

    private static Customer CreateCustomer()
    {
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = Guid.NewGuid().ToString()[..18], Status = CustomerStatus.Approved, IsActive = true, MinimumOrder = 1m, CreditLimit = 1000m };
        customer.Addresses.Add(new CustomerAddress { Label = "Principal", Street = "Rua A", Number = "1", District = "Centro", City = "Campinas", State = "SP", ZipCode = "13000-000", IsActive = true, IsPrimary = true });
        return customer;
    }

    private static Product CreateProduct(string sku, string name, decimal price) => new() { Sku = sku, Name = name, Brand = "Bimbo", Unit = "caixa", BasePrice = price, MinimumCases = 1, IsActive = true, IsAvailable = true, ProductCategory = new ProductCategory { Name = "Pães", Slug = Guid.NewGuid().ToString() } };
    private static ApplicationUser CreateUser(string id) => new() { Id = id, UserName = id, Email = $"{id}@test.local" };
}