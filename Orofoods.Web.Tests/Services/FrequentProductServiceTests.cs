using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class FrequentProductServiceTests
{
    [Fact]
    public async Task Returns_active_products_ranked_by_ordered_quantity_with_current_prices()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Paes", Slug = "paes", SortOrder = 1, IsActive = true };
        var customer = new Customer
        {
            LegalName = "Burger House Ltda",
            TradeName = "Burger House",
            Cnpj = "12.345.678/0001-99",
            Status = CustomerStatus.Approved,
            IsActive = true
        };
        var brioche = new Product { Sku = "PAO-001", Name = "Brioche", ProductCategory = category, Brand = "Orofoods", Unit = "caixa", BasePrice = 90m, IsAvailable = true, IsActive = true };
        var frozen = new Product { Sku = "PAO-002", Name = "Pao congelado", ProductCategory = category, Brand = "Orofoods", Unit = "caixa", BasePrice = 75m, PromotionalPrice = 70m, IsAvailable = true, IsActive = true };
        var inactive = new Product { Sku = "PAO-003", Name = "Descontinuado", ProductCategory = category, Brand = "Orofoods", Unit = "caixa", BasePrice = 40m, IsAvailable = true, IsActive = false };
        var otherCustomer = new Customer
        {
            LegalName = "Outro Cliente Ltda",
            TradeName = "Outro Cliente",
            Cnpj = "98.765.432/0001-10",
            Status = CustomerStatus.Approved,
            IsActive = true
        };
        var user = new ApplicationUser { Id = "buyer-1", UserName = "buyer@burgerhouse.test", Email = "buyer@burgerhouse.test" };
        db.AddRange(category, customer, otherCustomer, brioche, frozen, inactive, user);
        await db.SaveChangesAsync();
        db.Orders.Add(new Order
        {
            CustomerId = customer.Id,
            CreatedByUserId = user.Id,
            Number = "ORO-2026-000001",
            Status = OrderStatus.Delivered,
            Items = [
                new OrderItem { ProductId = brioche.Id, ProductNameSnapshot = brioche.Name, SkuSnapshot = brioche.Sku, Quantity = 8, UnitPrice = 80m, Subtotal = 640m },
                new OrderItem { ProductId = frozen.Id, ProductNameSnapshot = frozen.Name, SkuSnapshot = frozen.Sku, Quantity = 5, UnitPrice = 65m, Subtotal = 325m },
                new OrderItem { ProductId = inactive.Id, ProductNameSnapshot = inactive.Name, SkuSnapshot = inactive.Sku, Quantity = 12, UnitPrice = 40m, Subtotal = 480m }
            ]
        });
        db.Orders.AddRange(
            new Order
            {
                CustomerId = customer.Id,
                CreatedByUserId = user.Id,
                Number = "ORO-CANCELLED",
                Status = OrderStatus.Cancelled,
                Items = [new OrderItem { ProductId = brioche.Id, ProductNameSnapshot = brioche.Name, SkuSnapshot = brioche.Sku, Quantity = 90, UnitPrice = 80m, Subtotal = 7200m }]
            },
            new Order
            {
                CustomerId = customer.Id,
                CreatedByUserId = user.Id,
                Number = "ORO-DRAFT",
                Status = OrderStatus.Draft,
                Items = [new OrderItem { ProductId = frozen.Id, ProductNameSnapshot = frozen.Name, SkuSnapshot = frozen.Sku, Quantity = 80, UnitPrice = 65m, Subtotal = 5200m }]
            },
            new Order
            {
                CustomerId = otherCustomer.Id,
                CreatedByUserId = user.Id,
                Number = "ORO-OTHER-CUSTOMER",
                Status = OrderStatus.Delivered,
                Items = [new OrderItem { ProductId = brioche.Id, ProductNameSnapshot = brioche.Name, SkuSnapshot = brioche.Sku, Quantity = 100, UnitPrice = 80m, Subtotal = 8000m }]
            });
        await db.SaveChangesAsync();

        var sut = new FrequentProductService(db, new PriceService(db));

        var result = await sut.GetAsync(customer.Id);

        Assert.Collection(result,
            item => { Assert.Equal(brioche.Id, item.Product.Id); Assert.Equal(8, item.OrderedQuantity); Assert.Equal(90m, item.CurrentPrice); },
            item => { Assert.Equal(frozen.Id, item.Product.Id); Assert.Equal(5, item.OrderedQuantity); Assert.Equal(70m, item.CurrentPrice); });
    }
}
