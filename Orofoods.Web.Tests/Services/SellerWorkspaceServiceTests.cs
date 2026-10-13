using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Sellers;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public sealed class SellerWorkspaceServiceTests
{
    [Fact]
    public async Task Draft_customer_search_normalizes_cnpj_and_returns_only_the_sellers_portfolio()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var representative = new SalesRepresentative { Name = "Vendedor", IsActive = true };
        var otherRepresentative = new SalesRepresentative { Name = "Outro vendedor", IsActive = true };
        var seller = new ApplicationUser { Id = "seller", UserName = "seller", IsActive = true, SalesRepresentative = representative };
        var ownCustomer = new Customer
        {
            LegalName = "Auto Posto",
            TradeName = "Auto Posto",
            Cnpj = "51.735.297/0001-74",
            Status = CustomerStatus.Approved,
            IsActive = true,
            SalesRepresentative = representative
        };
        var otherCustomer = new Customer
        {
            LegalName = "Outro Cliente",
            TradeName = "Outro Cliente",
            Cnpj = "12.345.678/0001-95",
            Status = CustomerStatus.Approved,
            IsActive = true,
            SalesRepresentative = otherRepresentative
        };
        db.AddRange(representative, otherRepresentative, seller, ownCustomer, otherCustomer);
        await db.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, seller.Id), new Claim(ClaimTypes.Role, ApplicationRoles.Seller)], "test"));
        var result = await new SellerWorkspaceService(db, new SalesRepresentativeAccessService(db))
            .SearchCustomersForOrderDraftAsync(principal, "51735297000174");

        Assert.NotNull(result);
        var match = Assert.Single(result.Customers);
        Assert.Equal(ownCustomer.Id, match.Id);
        Assert.Equal("AUTO POSTO", match.Name.ToUpperInvariant());
    }
}
