using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Tests.Infrastructure;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Tests.Controllers;

public class CommercialControllerTests
{
    [Fact]
    public async Task Calendar_limits_city_filters_to_the_authenticated_seller_customers()
    {
        await using var db = await TestDbContextFactory.CreateAsync();

        var seller = new SalesRepresentative { Name = "Vendedor A" };
        var anotherSeller = new SalesRepresentative { Name = "Vendedor B" };
        var sellerUser = new ApplicationUser
        {
            Id = "seller-a",
            UserName = "seller-a@orofoods.local",
            SalesRepresentative = seller
        };
        db.AddRange(
            sellerUser,
            new Customer
            {
                LegalName = "Cliente A Ltda",
                TradeName = "Cliente A",
                Cnpj = "11.111.111/0001-11",
                SalesRepresentative = seller,
                Addresses = [new CustomerAddress { City = "Campinas", State = "SP" }]
            },
            new Customer
            {
                LegalName = "Cliente B Ltda",
                TradeName = "Cliente B",
                Cnpj = "22.222.222/0001-22",
                SalesRepresentative = anotherSeller,
                Addresses = [new CustomerAddress { City = "Jundiaí", State = "SP" }]
            });
        await db.SaveChangesAsync();

        var controller = new CommercialController(db, new SalesRepresentativeAccessService(db))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, sellerUser.Id),
                        new Claim(ClaimTypes.Role, "Vendedor")
                    ], "Test"))
                }
            }
        };

        var result = await controller.Calendar(null, null, null, null, null);
        var model = Assert.IsType<CommercialCalendarViewModel>(Assert.IsType<ViewResult>(result).Model);

        Assert.Collection(model.Cities, city => Assert.Equal("Campinas", city.Value));
    }

    [Fact]
    public async Task Index_limits_order_indicators_to_the_authenticated_seller_customers()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var referenceDate = DateTime.Today;

        var seller = new SalesRepresentative { Name = "Vendedor A" };
        var anotherSeller = new SalesRepresentative { Name = "Vendedor B" };
        var sellerUser = new ApplicationUser
        {
            Id = "seller-a",
            UserName = "seller-a@orofoods.local",
            SalesRepresentative = seller
        };
        var customer = new Customer
        {
            LegalName = "Cliente A Ltda",
            TradeName = "Cliente A",
            Cnpj = "11.111.111/0001-11",
            SalesRepresentative = seller
        };
        var anotherCustomer = new Customer
        {
            LegalName = "Cliente B Ltda",
            TradeName = "Cliente B",
            Cnpj = "22.222.222/0001-22",
            SalesRepresentative = anotherSeller
        };
        db.AddRange(sellerUser, customer, anotherCustomer);
        await db.SaveChangesAsync();
        db.Orders.AddRange(
            new Order
            {
                Number = "ORO-SELLER-A",
                CustomerId = customer.Id,
                CreatedByUserId = sellerUser.Id,
                CreatedAt = referenceDate.AddHours(9),
                Status = OrderStatus.Received,
                Total = 125m
            },
            new Order
            {
                Number = "ORO-SELLER-B",
                CustomerId = anotherCustomer.Id,
                CreatedByUserId = sellerUser.Id,
                CreatedAt = referenceDate.AddHours(10),
                Status = OrderStatus.Received,
                Total = 875m
            });
        await db.SaveChangesAsync();

        var controller = new CommercialController(db, new SalesRepresentativeAccessService(db))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, sellerUser.Id),
                        new Claim(ClaimTypes.Role, "Vendedor")
                    ], "Test"))
                }
            }
        };

        var result = await controller.Index(referenceDate);
        var model = Assert.IsType<CommercialDashboardViewModel>(Assert.IsType<ViewResult>(result).Model);

        Assert.Equal(1, model.OrdersToday);
        Assert.Equal(125m, model.TodayRevenue);
    }
}
