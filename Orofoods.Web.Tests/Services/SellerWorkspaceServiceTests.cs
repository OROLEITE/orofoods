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
    public async Task Wallet_search_does_not_load_the_customer_directory_and_caps_search_results()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var representative = new SalesRepresentative { Name = "Vendedor", IsActive = true };
        var seller = new ApplicationUser { Id = "wallet-search", UserName = "wallet-search", IsActive = true, SalesRepresentative = representative };
        db.AddRange(representative, seller);
        for (var index = 0; index < 25; index++)
        {
            db.Customers.Add(new Customer
            {
                LegalName = $"Cliente {index:D2} Ltda", TradeName = $"Cliente {index:D2}",
                Cnpj = $"{index:D2}.123.456/0001-78", Status = CustomerStatus.Approved,
                IsActive = true, SalesRepresentative = representative
            });
        }
        await db.SaveChangesAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, seller.Id), new Claim(ClaimTypes.Role, ApplicationRoles.Seller)], "test"));
        var service = new SellerWorkspaceService(db, new SalesRepresentativeAccessService(db));

        var initial = await service.SearchCustomersAsync(principal, null);
        var results = await service.SearchCustomersAsync(principal, "Cliente");

        Assert.Empty(initial!.Customers);
        Assert.Equal(20, results!.Customers.Count);
    }

    [Fact]
    public async Task Customer_suggestions_use_the_authenticated_seller_portfolio_and_require_admin_context()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var representative = new SalesRepresentative { Name = "Vendedor", IsActive = true };
        var otherRepresentative = new SalesRepresentative { Name = "Outro vendedor", IsActive = true };
        var seller = new ApplicationUser { Id = "seller-search", UserName = "seller-search", IsActive = true, SalesRepresentative = representative };
        var ownCustomer = new Customer
        {
            LegalName = "Restaurante Cliente Ltda", TradeName = "Restaurante Cliente", Cnpj = "51.735.297/0001-74",
            WmcCode = "WMC-OWN", Status = CustomerStatus.Approved, IsActive = true, SalesRepresentative = representative
        };
        var otherCustomer = new Customer
        {
            LegalName = "Restaurante Cliente Outro Ltda", TradeName = "Restaurante Cliente Outro", Cnpj = "12.345.678/0001-95",
            WmcCode = "WMC-OTHER", Status = CustomerStatus.Approved, IsActive = true, SalesRepresentative = otherRepresentative
        };
        var inactiveCustomer = new Customer
        {
            LegalName = "Restaurante Cliente Inativo Ltda", TradeName = "Restaurante Cliente Inativo", Cnpj = "11.111.111/0001-11",
            Status = CustomerStatus.Approved, IsActive = false, SalesRepresentative = representative
        };
        db.AddRange(representative, otherRepresentative, seller, ownCustomer, otherCustomer, inactiveCustomer);
        await db.SaveChangesAsync();

        var service = new SellerWorkspaceService(db, new SalesRepresentativeAccessService(db));
        var sellerPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, seller.Id), new Claim(ClaimTypes.Role, ApplicationRoles.Seller)], "test"));
        var selectedAdmin = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "admin"), new Claim(ClaimTypes.Role, ApplicationRoles.Administrator),
             new Claim(AdminSellerContextService.SelectedRepresentativeClaimType, representative.Id.ToString())], "test"));
        var unselectedAdmin = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "admin"), new Claim(ClaimTypes.Role, ApplicationRoles.Administrator)], "test"));

        var sellerResults = await service.SearchCustomerSuggestionsAsync(sellerPrincipal, "51735297000174", 20);
        var adminResults = await service.SearchCustomerSuggestionsAsync(selectedAdmin, "WMC-OWN", 20);
        var unselectedResults = await service.SearchCustomerSuggestionsAsync(unselectedAdmin, "Cliente", 20);

        Assert.Equal(ownCustomer.Id, Assert.Single(sellerResults!).Id);
        Assert.Equal(ownCustomer.Id, Assert.Single(adminResults!).Id);
        Assert.Null(unselectedResults);
    }

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
        var initial = await new SellerWorkspaceService(db, new SalesRepresentativeAccessService(db))
            .SearchCustomersForOrderDraftAsync(principal, null);

        Assert.NotNull(result);
        var match = Assert.Single(result.Customers);
        Assert.Equal(ownCustomer.Id, match.Id);
        Assert.Equal("AUTO POSTO", match.Name.ToUpperInvariant());
        Assert.NotNull(initial);
        Assert.Empty(initial.Customers);
        Assert.Equal(0, initial.TotalCount);
    }

    [Fact]
    public async Task Seller_customer_search_matches_partial_name_normalized_cnpj_and_partial_wmc_code()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var representative = new SalesRepresentative { Name = "Vendedor", IsActive = true };
        var seller = new ApplicationUser { Id = "seller-customer-search", UserName = "seller-customer-search", IsActive = true, SalesRepresentative = representative };
        var customer = new Customer
        {
            LegalName = "Mercado Brasil Distribuidora",
            TradeName = "Mercado Central",
            Cnpj = "51.735.297/0001-74",
            WmcCode = "WMC-92841",
            Status = CustomerStatus.Approved,
            IsActive = true,
            SalesRepresentative = representative
        };
        db.AddRange(representative, seller, customer);
        await db.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, seller.Id), new Claim(ClaimTypes.Role, ApplicationRoles.Seller)], "test"));
        var service = new SellerWorkspaceService(db, new SalesRepresentativeAccessService(db));

        var partialName = await service.SearchCustomersAsync(principal, "Mercado Cen");
        var partialLegalName = await service.SearchCustomersAsync(principal, "Brasil Distrib");
        var normalizedCnpj = await service.SearchCustomersAsync(principal, "735.297/0001");
        var partialWmcCode = await service.SearchCustomersAsync(principal, "WMC-928");

        Assert.Equal(customer.Id, Assert.Single(partialName!.Customers).Id);
        Assert.Equal(customer.Id, Assert.Single(partialLegalName!.Customers).Id);
        Assert.Equal(customer.Id, Assert.Single(normalizedCnpj!.Customers).Id);
        Assert.Equal(customer.Id, Assert.Single(partialWmcCode!.Customers).Id);
    }
}
