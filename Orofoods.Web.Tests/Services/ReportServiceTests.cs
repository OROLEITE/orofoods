using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Reports;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class ReportServiceTests
{
    [Fact]
    public async Task Applies_period_and_customer_filters_and_excludes_cancelled_revenue()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Paes", Slug = "paes", IsActive = true };
        var product = new Product { Sku = "PAO-001", Name = "Brioche", ProductCategory = category, Brand = "Orofoods", Unit = "caixa", BasePrice = 90m, IsActive = true };
        var selected = new Customer { LegalName = "Burger House Ltda", TradeName = "Burger House", Cnpj = "12.345.678/0001-99", Status = CustomerStatus.Approved, IsActive = true };
        var other = new Customer { LegalName = "Other Ltda", TradeName = "Other", Cnpj = "98.765.432/0001-99", Status = CustomerStatus.Approved, IsActive = true };
        var user = new ApplicationUser { Id = "buyer-1", UserName = "buyer@test", Email = "buyer@test" };
        db.AddRange(category, product, selected, other, user);
        await db.SaveChangesAsync();
        db.Orders.AddRange(
            BuildOrder(selected.Id, user.Id, product, "ORO-1", new DateTime(2026, 8, 10), OrderStatus.Delivered, 200m),
            BuildOrder(selected.Id, user.Id, product, "ORO-2", new DateTime(2026, 8, 11), OrderStatus.Cancelled, 900m),
            BuildOrder(other.Id, user.Id, product, "ORO-3", new DateTime(2026, 8, 12), OrderStatus.Delivered, 500m));
        await db.SaveChangesAsync();
        var sut = new ReportService(db);

        var report = await sut.BuildAsync(new ReportFilter(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31), selected.Id, null, null, null));

        Assert.Equal(1, report.OrderCount);
        Assert.Equal(200m, report.Revenue);
        Assert.Equal(200m, report.AverageTicket);
        Assert.Equal("Brioche", Assert.Single(report.TopProducts).Name);
    }

    private static Order BuildOrder(int customerId, string userId, Product product, string number, DateTime date, OrderStatus status, decimal total) => new()
    {
        CustomerId = customerId, CreatedByUserId = userId, Number = number, CreatedAt = date, Status = status, Subtotal = total, Total = total,
        Items = [new OrderItem { Product = product, ProductNameSnapshot = product.Name, SkuSnapshot = product.Sku, Quantity = 2, UnitPrice = total / 2, Subtotal = total }]
    };
}
