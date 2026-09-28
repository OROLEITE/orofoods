using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Sellers;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class SellerWorkspaceServiceTests
{
    [Fact]
    public async Task Dashboard_and_search_include_only_active_approved_portfolio_customers()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var representative = new SalesRepresentative { Name = "Vendedor A", IsActive = true };
        var seller = new ApplicationUser { Id = "seller-a", UserName = "seller-a", IsActive = true, SalesRepresentative = representative };
        var assigned = Customer("Burger A", "A-001", representative, CustomerStatus.Approved, true);
        var other = Customer("Burger B", "B-001", new SalesRepresentative { Name = "Vendedor B", IsActive = true }, CustomerStatus.Approved, true);
        var pending = Customer("Burger Pendente", "P-001", representative, CustomerStatus.Pending, true);
        var inactive = Customer("Burger Inativo", "I-001", representative, CustomerStatus.Approved, false);
        db.AddRange(representative, seller, other.SalesRepresentative!, assigned, other, pending, inactive);
        await db.SaveChangesAsync();

        var service = new SellerWorkspaceService(db, new SalesRepresentativeAccessService(db));
        var principal = SellerPrincipal(seller.Id);

        var dashboard = Assert.IsType<SellerDashboardViewModel>(await service.GetDashboardAsync(principal));
        var search = Assert.IsType<SellerCustomerListViewModel>(await service.SearchCustomersAsync(principal, "A-001"));

        Assert.Equal(1, dashboard.ActiveCustomerCount);
        Assert.Single(dashboard.Customers);
        Assert.Equal(assigned.Id, dashboard.Customers[0].Id);
        Assert.Single(search.Customers);
        Assert.Equal(assigned.Id, search.Customers[0].Id);
    }

    [Fact]
    public async Task Workspace_rejects_customer_outside_portfolio_and_unapproved_customer()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var representativeA = new SalesRepresentative { Name = "Vendedor A", IsActive = true };
        var representativeB = new SalesRepresentative { Name = "Vendedor B", IsActive = true };
        var seller = new ApplicationUser { Id = "seller-a", UserName = "seller-a", IsActive = true, SalesRepresentative = representativeA };
        var assigned = Customer("Cliente A", "A-001", representativeA, CustomerStatus.Approved, true);
        var other = Customer("Cliente B", "B-001", representativeB, CustomerStatus.Approved, true);
        var pending = Customer("Cliente Pendente", "P-001", representativeA, CustomerStatus.Pending, true);
        db.AddRange(representativeA, representativeB, seller, assigned, other, pending);
        await db.SaveChangesAsync();

        var service = new SellerWorkspaceService(db, new SalesRepresentativeAccessService(db));
        var principal = SellerPrincipal(seller.Id);

        Assert.NotNull(await service.GetCustomerWorkspaceAsync(principal, assigned.Id));
        Assert.Null(await service.GetCustomerWorkspaceAsync(principal, other.Id));
        Assert.Null(await service.GetCustomerWorkspaceAsync(principal, pending.Id));
    }

    private static Customer Customer(string name, string code, SalesRepresentative representative, CustomerStatus status, bool active) => new()
    {
        LegalName = $"{name} Ltda",
        TradeName = name,
        Cnpj = $"{code[..1]}2.345.678/0001-00",
        WmcCode = code,
        Status = status,
        IsActive = active,
        SalesRepresentative = representative,
        Addresses = [new CustomerAddress { City = "Campinas", State = "SP", IsActive = true }]
    };

    private static ClaimsPrincipal SellerPrincipal(string userId) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, "Vendedor")],
        "test"));
}