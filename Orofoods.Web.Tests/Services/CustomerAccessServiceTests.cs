using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class CustomerAccessServiceTests
{
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
