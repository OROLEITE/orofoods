using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class SavedOrderServiceTests
{
    [Fact]
    public async Task Saves_active_model_and_loads_only_available_products_for_its_customer()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Paes", Slug = "paes", SortOrder = 1, IsActive = true };
        var customer = new Customer { LegalName = "Burger House Ltda", TradeName = "Burger House", Cnpj = "12.345.678/0001-99", Status = CustomerStatus.Approved, IsActive = true };
        var otherCustomer = new Customer { LegalName = "Other Ltda", TradeName = "Other", Cnpj = "98.765.432/0001-99", Status = CustomerStatus.Approved, IsActive = true };
        var available = new Product { Sku = "PAO-001", Name = "Brioche", ProductCategory = category, Brand = "Orofoods", Unit = "caixa", MinimumCases = 2, BasePrice = 90m, IsActive = true, IsAvailable = true };
        var unavailable = new Product { Sku = "PAO-002", Name = "Indisponivel", ProductCategory = category, Brand = "Orofoods", Unit = "caixa", BasePrice = 70m, IsActive = true, IsAvailable = false };
        db.AddRange(category, customer, otherCustomer, available, unavailable, new ApplicationUser { Id = "buyer-1", UserName = "buyer@test", Email = "buyer@test" });
        await db.SaveChangesAsync();
        var sut = new SavedOrderService(db);

        var saved = await sut.SaveAsync(customer.Id, "Pedido semanal", [new SavedOrderLine(available.Id, 1), new SavedOrderLine(unavailable.Id, 5)]);
        var loadable = await sut.GetLoadableAsync(customer.Id, saved.Id);
        var otherCustomerResult = await sut.GetLoadableAsync(otherCustomer.Id, saved.Id);

        Assert.Equal("Pedido semanal", saved.Name);
        Assert.True(saved.IsActive);
        Assert.Collection(loadable, item => { Assert.Equal(available.Id, item.ProductId); Assert.Equal(2, item.Quantity); });
        Assert.Empty(otherCustomerResult);
        await sut.DeactivateAsync(customer.Id, saved.Id);
        Assert.Empty(await sut.GetLoadableAsync(customer.Id, saved.Id));
    }
}
