using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class CustomerAccessServiceTests
{
    [Fact]
    public async Task Customer_suggestions_search_names_normalized_cnpj_and_wmc_with_active_approved_limit()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var approved = new Customer
        {
            LegalName = "Mercado Cliente Ltda",
            TradeName = "Mercado Cliente",
            Cnpj = "51.735.297/0001-74",
            WmcCode = "WMC-517",
            Status = CustomerStatus.Approved,
            IsActive = true,
            Addresses = [new() { City = "Campinas", State = "SP", IsActive = true }]
        };
        var inactive = new Customer
        {
            LegalName = "Cliente Inativo Ltda", TradeName = "Cliente Inativo", Cnpj = "11.111.111/0001-11",
            Status = CustomerStatus.Approved, IsActive = false
        };
        var pending = new Customer
        {
            LegalName = "Cliente Pendente Ltda", TradeName = "Cliente Pendente", Cnpj = "22.222.222/0001-22",
            Status = CustomerStatus.Pending, IsActive = true
        };
        db.AddRange(approved, inactive, pending);
        await db.SaveChangesAsync();
        var sut = new CustomerAccessService(db);

        var byName = await sut.SearchApprovedCustomerSuggestionsAsync("Cliente", 50);
        var byCnpj = await sut.SearchApprovedCustomerSuggestionsAsync("51735297000174", 20);
        var byWmc = await sut.SearchApprovedCustomerSuggestionsAsync("WMC-517", 20);
        var blank = await sut.SearchApprovedCustomerSuggestionsAsync("   ", 20);

        var suggestion = Assert.Single(byName);
        Assert.Equal(approved.Id, suggestion.Id);
        Assert.Equal("Mercado Cliente", suggestion.TradeName);
        Assert.Equal("Mercado Cliente Ltda", suggestion.LegalName);
        Assert.Equal("51.735.297/0001-74", suggestion.Cnpj);
        Assert.Equal("WMC-517", suggestion.WmcCode);
        Assert.Equal("Campinas", suggestion.City);
        Assert.Equal(approved.Id, Assert.Single(byCnpj).Id);
        Assert.Equal(approved.Id, Assert.Single(byWmc).Id);
        Assert.Empty(blank);
    }

    [Fact]
    public async Task Customer_suggestions_accept_a_single_character_and_keep_approval_filters()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var approved = new Customer
        {
            LegalName = "Burger House Ltda", TradeName = "Burger House", Cnpj = "51.735.297/0001-74",
            Status = CustomerStatus.Approved, IsActive = true
        };
        var pending = new Customer
        {
            LegalName = "Balcão Pendente Ltda", TradeName = "Balcão Pendente", Cnpj = "22.222.222/0001-22",
            Status = CustomerStatus.Pending, IsActive = true
        };
        var inactive = new Customer
        {
            LegalName = "Bar Inativo Ltda", TradeName = "Bar Inativo", Cnpj = "11.111.111/0001-11",
            Status = CustomerStatus.Approved, IsActive = false
        };
        db.AddRange(approved, pending, inactive);
        await db.SaveChangesAsync();

        var suggestions = await new CustomerAccessService(db).SearchApprovedCustomerSuggestionsAsync("B", 20);

        Assert.Equal(approved.Id, Assert.Single(suggestions).Id);
    }

    [Fact]
    public async Task Customer_suggestions_are_capped_at_twenty()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        for (var index = 0; index < 25; index++)
        {
            db.Customers.Add(new Customer
            {
                LegalName = $"Cliente {index:D2} Ltda", TradeName = $"Cliente {index:D2}",
                Cnpj = $"{index:D2}.123.456/0001-78", Status = CustomerStatus.Approved, IsActive = true
            });
        }
        await db.SaveChangesAsync();

        var results = await new CustomerAccessService(db).SearchApprovedCustomerSuggestionsAsync("Cliente", 100);

        Assert.Equal(20, results.Count);
    }

    [Fact]
    public async Task Approved_active_customer_can_be_loaded_by_selected_context()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var approved = new Customer
        {
            LegalName = "Burger House Ltda",
            TradeName = "Burger House",
            Cnpj = "12.345.678/0001-99",
            Status = CustomerStatus.Approved,
            IsActive = true
        };
        var pending = new Customer
        {
            LegalName = "Pendente Ltda",
            TradeName = "Pendente",
            Cnpj = "98.765.432/0001-99",
            Status = CustomerStatus.Pending,
            IsActive = true
        };
        db.AddRange(approved, pending);
        await db.SaveChangesAsync();
        var sut = new CustomerAccessService(db);

        var selected = await sut.GetApprovedCustomerByIdAsync(approved.Id);
        var unavailable = await sut.GetApprovedCustomerByIdAsync(pending.Id);

        Assert.Equal(approved.Id, selected!.Id);
        Assert.Null(unavailable);
    }

    [Fact]
    public async Task Approved_customer_user_has_commercial_access()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer
        {
            LegalName = "Burger House Ltda",
            TradeName = "Burger House",
            Cnpj = "12.345.678/0001-99",
            Status = CustomerStatus.Approved,
            IsActive = true
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        db.Users.Add(new ApplicationUser
        {
            Id = "user-1",
            UserName = "compras@burgerhouse.com",
            Email = "compras@burgerhouse.com",
            CustomerId = customer.Id,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var sut = new CustomerAccessService(db);

        Assert.True(await sut.HasApprovedCustomerAccessAsync("user-1"));
    }

    [Fact]
    public async Task Pending_customer_user_does_not_have_commercial_access()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer
        {
            LegalName = "Burger House Ltda",
            TradeName = "Burger House",
            Cnpj = "12.345.678/0001-99",
            Status = CustomerStatus.Pending,
            IsActive = true
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        db.Users.Add(new ApplicationUser
        {
            Id = "user-2",
            UserName = "pendente@burgerhouse.com",
            Email = "pendente@burgerhouse.com",
            CustomerId = customer.Id,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var sut = new CustomerAccessService(db);

        Assert.False(await sut.HasApprovedCustomerAccessAsync("user-2"));
    }
}
