using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Authorization;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Authorization;

public class LinkedSalesRepresentativeAuthorizationTests
{
    [Fact]
    public async Task Active_vendedor_with_active_representative_succeeds()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var representative = new SalesRepresentative { Name = "Vendedor", IsActive = true };
        var user = new ApplicationUser { Id = "seller", UserName = "seller", IsActive = true, SalesRepresentative = representative };
        db.AddRange(representative, user);
        await db.SaveChangesAsync();

        var context = CreateContext(SellerPrincipal(user.Id));
        await new LinkedSalesRepresentativeHandler(new SalesRepresentativeAccessService(db)).HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Anonymous_non_vendedor_and_inactive_seller_are_denied()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var representative = new SalesRepresentative { Name = "Vendedor", IsActive = false };
        var user = new ApplicationUser { Id = "seller", UserName = "seller", IsActive = true, SalesRepresentative = representative };
        db.AddRange(representative, user);
        await db.SaveChangesAsync();

        var handler = new LinkedSalesRepresentativeHandler(new SalesRepresentativeAccessService(db));
        var anonymous = CreateContext(new ClaimsPrincipal(new ClaimsIdentity()));
        var customer = CreateContext(new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Role, "Cliente")
        ], "test")));
        var inactive = CreateContext(SellerPrincipal(user.Id));

        await handler.HandleAsync(anonymous);
        await handler.HandleAsync(customer);
        await handler.HandleAsync(inactive);

        Assert.False(anonymous.HasSucceeded);
        Assert.False(customer.HasSucceeded);
        Assert.False(inactive.HasSucceeded);
    }

    private static AuthorizationHandlerContext CreateContext(ClaimsPrincipal principal) =>
        new([new LinkedSalesRepresentativeRequirement()], principal, null);

    private static ClaimsPrincipal SellerPrincipal(string userId) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, "Vendedor")],
        "test"));
}