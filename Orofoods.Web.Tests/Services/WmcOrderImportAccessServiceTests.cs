using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public sealed class WmcOrderImportAccessServiceTests
{
    [Fact]
    public async Task Seller_can_import_only_orders_for_customers_in_their_active_portfolio()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var representative = new SalesRepresentative { Name = "Seller", IsActive = true };
        var customer = new Customer
        {
            LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99",
            IsActive = true, Status = CustomerStatus.Approved, SalesRepresentative = representative
        };
        db.AddRange(representative, customer, new ApplicationUser
        {
            Id = "seller-user", UserName = "seller@test.local", Email = "seller@test.local",
            IsActive = true, SalesRepresentative = representative
        });
        await db.SaveChangesAsync();
        var access = new WmcOrderImportAccessService(db, new SalesRepresentativeAccessService(db));
        var seller = Principal(ApplicationRoles.Seller, "seller-user");

        Assert.True(await access.CanAccessCustomerAsync(seller, customer.Id));
        Assert.False(await access.CanAccessCustomerAsync(seller, customer.Id + 100));
    }

    [Theory]
    [InlineData(ApplicationRoles.Administrator)]
    [InlineData(ApplicationRoles.Operator)]
    public async Task Administrator_and_operator_can_access_orders_in_the_wmc_import_queue(string role)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var access = new WmcOrderImportAccessService(db, new SalesRepresentativeAccessService(db));

        Assert.True(await access.CanAccessCustomerAsync(Principal(role, "operator-user"), 123));
    }

    [Fact]
    public async Task User_without_an_import_role_is_denied()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var access = new WmcOrderImportAccessService(db, new SalesRepresentativeAccessService(db));

        Assert.False(await access.CanAccessCustomerAsync(Principal(ApplicationRoles.Customer, "customer-user"), 123));
    }

    private static ClaimsPrincipal Principal(string role, string userId) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, userId),
        new Claim(ClaimTypes.Role, role)
    ], "test"));
}
