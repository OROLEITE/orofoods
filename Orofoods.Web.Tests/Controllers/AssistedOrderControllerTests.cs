using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Tests.Infrastructure;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Tests.Controllers;

public class AssistedOrderControllerTests
{
    [Fact]
    public async Task NewOrder_ForbidsCustomerOutsideSellerScope()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seller = new SalesRepresentative { Name = "Seller", IsActive = true };
        var owned = CreateCustomer("Owned", seller);
        var outside = CreateCustomer("Outside", null);
        var term = new PaymentTerm { Code = "PIX", Name = "PIX", IsActive = true };
        var user = new ApplicationUser { Id = "seller-user", UserName = "seller-user", Email = "seller@test.local", SalesRepresentative = seller, IsActive = true };
        db.AddRange(seller, owned, outside, term, user);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.NewOrder(outside.Id, null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task NewOrder_AllowsCustomerInSellerScope()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seller = new SalesRepresentative { Name = "Seller", IsActive = true };
        var customer = CreateCustomer("Owned", seller);
        var term = new PaymentTerm { Code = "PIX", Name = "PIX", IsActive = true };
        var user = new ApplicationUser { Id = "seller-user", UserName = "seller-user", Email = "seller@test.local", SalesRepresentative = seller, IsActive = true };
        db.AddRange(seller, customer, term, user);
        await db.SaveChangesAsync();

        var result = await CreateController(db).NewOrder(customer.Id, null);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(customer.TradeName, Assert.IsType<AssistedOrderViewModel>(view.Model).CustomerName);
    }

    private static CustomersController CreateController(Orofoods.Web.Data.ApplicationDbContext db)
    {
        var priceService = new PriceService(db);
        var controller = new CustomersController(
            db,
            new CustomerApprovalService(db),
            new SalesRepresentativeAccessService(db),
            new PaymentEligibilityService(db, Options.Create(new PaymentEligibilityOptions())),
            priceService,
            new AssistedOrderService(db, priceService, new PaymentEligibilityService(db, Options.Create(new PaymentEligibilityOptions())), new OrderReservationService(db)),
            new CommercialAttentionService(db, new SalesRepresentativeAccessService(db), Options.Create(new CrmOptions()), Options.Create(new PaymentEligibilityOptions())));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, "seller-user"),
                    new Claim(ClaimTypes.Role, "Vendedor")
                ], "test"))
            }
        };
        return controller;
    }

    private static Customer CreateCustomer(string name, SalesRepresentative? seller) => new()
    {
        LegalName = $"{name} Ltda",
        TradeName = name,
        Cnpj = Guid.NewGuid().ToString()[..18],
        Status = CustomerStatus.Approved,
        IsActive = true,
        SalesRepresentative = seller,
        Addresses = [new CustomerAddress { Label = "Principal", Street = "Rua A", Number = "1", District = "Centro", City = "Campinas", State = "SP", ZipCode = "13000-000", IsActive = true }]
    };
}