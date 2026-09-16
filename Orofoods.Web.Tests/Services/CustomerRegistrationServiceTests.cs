using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class CustomerRegistrationServiceTests
{
    [Fact]
    public async Task Registration_creates_pending_customer_primary_address_and_client_user()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var roleManager = TestIdentityFactory.CreateRoleManager(db);
        var userManager = TestIdentityFactory.CreateUserManager(db);
        await roleManager.CreateAsync(new IdentityRole("Cliente"));

        var sut = new CustomerRegistrationService(db, userManager);
        var request = new CustomerRegistrationRequest
        {
            LegalName = "Burger House Ltda",
            TradeName = "Burger House",
            Cnpj = "12.345.678/0001-99",
            StateRegistration = "123456789",
            ResponsibleName = "Ana Souza",
            ResponsibleDocument = "123.456.789-10",
            Email = "compras@burgerhouse.com",
            Phone = "(19) 3000-1000",
            WhatsApp = "(19) 98888-0000",
            ZipCode = "13000-000",
            Street = "Rua Principal",
            Number = "100",
            Complement = "Sala 1",
            District = "Centro",
            City = "Campinas",
            State = "SP",
            Password = "Cliente123!"
        };

        var result = await sut.RegisterAsync(request);

        var customer = await db.Customers
            .Include(x => x.Addresses)
            .SingleAsync(x => x.Id == result.CustomerId);
        var user = await userManager.FindByIdAsync(result.UserId);

        Assert.Equal(CustomerStatus.Pending, customer.Status);
        Assert.True(customer.IsActive);
        Assert.Single(customer.Addresses);
        Assert.True(customer.Addresses[0].IsPrimary);
        Assert.Equal(customer.Id, user!.CustomerId);
        Assert.True(user.IsActive);
        Assert.True(await userManager.IsInRoleAsync(user, "Cliente"));
    }
}
