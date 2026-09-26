using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class SellerCartSecurityTests
{
    [Fact]
    public async Task Seller_can_scope_only_an_active_approved_customer_in_their_portfolio()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var representativeA = new SalesRepresentative { Name = "Vendedor A", IsActive = true };
        var representativeB = new SalesRepresentative { Name = "Vendedor B", IsActive = true };
        var seller = new ApplicationUser { Id = "seller-a", UserName = "seller-a", IsActive = true, SalesRepresentative = representativeA };
        var assigned = new Customer { LegalName = "Cliente A", TradeName = "Cliente A", Cnpj = "11.111.111/0001-11", Status = CustomerStatus.Approved, IsActive = true, SalesRepresentative = representativeA };
        var other = new Customer { LegalName = "Cliente B", TradeName = "Cliente B", Cnpj = "22.222.222/0001-22", Status = CustomerStatus.Approved, IsActive = true, SalesRepresentative = representativeB };
        db.AddRange(representativeA, representativeB, seller, assigned, other);
        await db.SaveChangesAsync();

        var principal = SellerPrincipal(seller.Id);
        var service = new SalesRepresentativeAccessService(db);

        Assert.NotNull(await service.GetSellerCartScopeAsync(principal, assigned.Id));
        Assert.Null(await service.GetSellerCartScopeAsync(principal, other.Id));
    }

    [Fact]
    public async Task Inactive_seller_and_unapproved_customer_are_blocked()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var representative = new SalesRepresentative { Name = "Vendedor", IsActive = true };
        var seller = new ApplicationUser { Id = "seller", UserName = "seller", IsActive = false, SalesRepresentative = representative };
        var pending = new Customer { LegalName = "Pendente", TradeName = "Pendente", Cnpj = "33.333.333/0001-33", Status = CustomerStatus.Pending, IsActive = true, SalesRepresentative = representative };
        db.AddRange(representative, seller, pending);
        await db.SaveChangesAsync();

        var service = new SalesRepresentativeAccessService(db);
        Assert.Null(await service.GetSellerCartScopeAsync(SellerPrincipal(seller.Id), pending.Id));

        seller.IsActive = true;
        await db.SaveChangesAsync();
        Assert.Null(await service.GetSellerCartScopeAsync(SellerPrincipal(seller.Id), pending.Id));
    }

    [Fact]
    public async Task Customer_principal_cannot_resolve_a_seller_cart_scope()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var representative = new SalesRepresentative { Name = "Vendedor", IsActive = true };
        var seller = new ApplicationUser { Id = "seller", UserName = "seller", IsActive = true, SalesRepresentative = representative };
        var customer = new Customer { LegalName = "Cliente", TradeName = "Cliente", Cnpj = "44.444.444/0001-44", Status = CustomerStatus.Approved, IsActive = true, SalesRepresentative = representative };
        db.AddRange(representative, seller, customer);
        await db.SaveChangesAsync();

        var customerPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, seller.Id), new Claim(ClaimTypes.Role, "Cliente")],
            "test"));

        Assert.Null(await new SalesRepresentativeAccessService(db).GetSellerCartScopeAsync(customerPrincipal, customer.Id));
    }

    private static ClaimsPrincipal SellerPrincipal(string userId) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, "Vendedor")],
        "test"));
}