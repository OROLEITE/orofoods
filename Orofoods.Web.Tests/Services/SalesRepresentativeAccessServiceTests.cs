using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class SalesRepresentativeAccessServiceTests
{
    [Fact]
    public async Task ScopeIncludesExternalAndInternalCustomersWithoutDuplicates()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seller = new SalesRepresentative { Name = "Carlos", IsActive = true };
        var internalUser = new ApplicationUser { Id = "internal-user", UserName = "jefferson", Email = "jefferson@test.local", IsActive = true };
        var externalOnly = CreateCustomer("Externo", seller, null);
        var internalOnly = CreateCustomer("Interno", null, internalUser.Id);
        var both = CreateCustomer("Ambos", seller, internalUser.Id);
        db.AddRange(seller, internalUser, externalOnly, internalOnly, both);
        await db.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, internalUser.Id),
            new Claim(ClaimTypes.Role, "Vendedor")
        ], "test"));
        var scope = await new SalesRepresentativeAccessService(db).GetScopeAsync(principal);
        var customers = await new SalesRepresentativeAccessService(db)
            .ApplyCustomerScope(db.Customers.AsNoTracking(), scope)
            .Select(x => x.Id)
            .ToListAsync();

        Assert.Equal(2, customers.Count);
        Assert.Contains(internalOnly.Id, customers);
        Assert.Contains(both.Id, customers);
        Assert.DoesNotContain(externalOnly.Id, customers);
    }

    [Fact]
    public async Task RemovingInternalUserSetsAssignmentNullAndKeepsCustomer()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var internalUser = new ApplicationUser { Id = "internal-user", UserName = "jefferson", Email = "jefferson@test.local", IsActive = true };
        var customer = CreateCustomer("Cliente", null, internalUser.Id);
        db.AddRange(internalUser, customer);
        await db.SaveChangesAsync();

        db.Users.Remove(internalUser);
        await db.SaveChangesAsync();

        var persisted = await db.Customers.AsNoTracking().SingleAsync(x => x.Id == customer.Id);
        Assert.Null(persisted.InternalSalesUserId);
    }

    private static Customer CreateCustomer(string name, SalesRepresentative? seller, string? internalUserId) => new()
    {
        LegalName = $"{name} Ltda",
        TradeName = name,
        Cnpj = Guid.NewGuid().ToString()[..18],
        Status = CustomerStatus.Approved,
        IsActive = true,
        SalesRepresentative = seller,
        InternalSalesUserId = internalUserId
    };
}