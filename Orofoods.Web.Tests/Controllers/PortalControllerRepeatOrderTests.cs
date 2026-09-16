using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Controllers;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Services.Catalog;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Tests.Infrastructure;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Tests.Controllers;

public class PortalControllerRepeatOrderTests
{
    [Fact]
    public async Task Confirm_forbids_using_an_address_owned_by_another_customer()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer
        {
            LegalName = "Cliente A Ltda",
            TradeName = "Cliente A",
            Cnpj = "12.345.678/0001-99",
            Status = CustomerStatus.Approved,
            IsActive = true,
            MinimumOrder = 1m
        };
        var otherCustomer = new Customer
        {
            LegalName = "Cliente B Ltda",
            TradeName = "Cliente B",
            Cnpj = "98.765.432/0001-10",
            Status = CustomerStatus.Approved,
            IsActive = true
        };
        otherCustomer.Addresses.Add(new CustomerAddress
        {
            Label = "Endereco de outro cliente",
            Street = "Rua B",
            Number = "2",
            District = "Centro",
            City = "Campinas",
            State = "SP",
            ZipCode = "13000-000",
            IsActive = true
        });
        var category = new ProductCategory { Name = "Paes", Slug = "paes", IsActive = true };
        var product = new Product
        {
            Sku = "BIM-001",
            Name = "Pao",
            ProductCategory = category,
            Brand = "BIMBO",
            Unit = "caixa",
            BasePrice = 10m,
            MinimumCases = 1,
            IsActive = true,
            IsAvailable = true
        };
        db.AddRange(customer, otherCustomer, product);
        await db.SaveChangesAsync();
        db.ProductInventories.Add(new ProductInventory { ProductId = product.Id, QuantityOnHand = 10 });
        db.Users.Add(new ApplicationUser
        {
            Id = "customer-user",
            UserName = "customer@orofoods.local",
            NormalizedUserName = "CUSTOMER@OROFOODS.LOCAL",
            Email = "customer@orofoods.local",
            NormalizedEmail = "CUSTOMER@OROFOODS.LOCAL",
            CustomerId = customer.Id,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, "customer-user");

        var result = await controller.Confirm(new ConfirmOrderVm
        {
            SourceOrderId = 1,
            AddressId = otherCustomer.Addresses.Single().Id,
            ProductIds = [product.Id],
            Quantities = [1]
        });

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(db.Orders);
    }

    private static PortalController CreateController(Orofoods.Web.Data.ApplicationDbContext db, string userId)
    {
        var priceService = new PriceService(db);
        var controller = new PortalController(
            db,
            TestIdentityFactory.CreateUserManager(db),
            new CustomerAccessService(db),
            new AdminCustomerContextService(),
            priceService,
            new CartService(db, priceService),
            new CustomerDashboardService(db, new FrequentProductService(db, priceService), TimeProvider.System),
            new SavedOrderService(db),
            new OrderReservationService(db));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test"))
            }
        };
        return controller;
    }
}
