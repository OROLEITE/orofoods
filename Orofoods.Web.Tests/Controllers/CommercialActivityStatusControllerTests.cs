using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Controllers;

public class CommercialActivityStatusControllerTests
{
    [Fact]
    public async Task Existing_status_action_persists_an_allowed_kanban_status()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (customer, activity) = await SeedActivityAsync(db);
        var controller = CreateController(db);

        var result = await InvokeStatusActionAsync(controller, activity.Id, customer.Id, (int)CommercialActivityStatus.InProgress, asJson: true);

        Assert.IsType<JsonResult>(result);
        await db.Entry(activity).ReloadAsync();
        Assert.Equal(CommercialActivityStatus.InProgress, activity.Status);
        Assert.Null(activity.CompletedAt);
    }

    [Theory]
    [InlineData((int)CommercialActivityStatus.Cancelled)]
    [InlineData(999)]
    public async Task Existing_status_action_rejects_statuses_that_are_not_kanban_destinations(int status)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (customer, activity) = await SeedActivityAsync(db);
        var controller = CreateController(db);

        var result = await InvokeStatusActionAsync(controller, activity.Id, customer.Id, status, asJson: true);

        Assert.IsType<BadRequestObjectResult>(result);
        await db.Entry(activity).ReloadAsync();
        Assert.Equal(CommercialActivityStatus.Scheduled, activity.Status);
    }

    [Fact]
    public async Task Existing_status_action_rejects_an_unknown_activity_id()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (customer, _) = await SeedActivityAsync(db);
        var controller = CreateController(db);

        var result = await InvokeStatusActionAsync(controller, int.MaxValue, customer.Id, (int)CommercialActivityStatus.InProgress, asJson: true);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Existing_status_action_respects_the_customer_scope_for_sellers()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (customer, activity) = await SeedActivityAsync(db);
        var seller = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "seller-without-customer-access"),
            new Claim(ClaimTypes.Role, ApplicationRoles.Seller)
        ], "test"));
        var controller = CreateController(db, seller);

        var result = await InvokeStatusActionAsync(controller, activity.Id, customer.Id, (int)CommercialActivityStatus.InProgress, asJson: true);

        Assert.IsType<ForbidResult>(result);
        await db.Entry(activity).ReloadAsync();
        Assert.Equal(CommercialActivityStatus.Scheduled, activity.Status);
    }

    [Fact]
    public void Existing_status_action_remains_post_only_and_antiforgery_protected()
    {
        var action = typeof(CustomersController).GetMethod(nameof(CustomersController.CompleteActivity));

        Assert.NotNull(action);
        Assert.Contains(action.GetCustomAttributes(), attribute => attribute is HttpPostAttribute);
        Assert.Contains(action.GetCustomAttributes(), attribute => attribute is ValidateAntiForgeryTokenAttribute);
        var authorization = typeof(CustomersController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorization);
        Assert.Equal("Administrador,Vendedor,GerenteComercial", authorization.Roles);
    }

    private static async Task<IActionResult> InvokeStatusActionAsync(CustomersController controller, int id, int customerId, int status, bool asJson)
    {
        var action = typeof(CustomersController).GetMethod(nameof(CustomersController.CompleteActivity));
        Assert.NotNull(action);
        Assert.Contains(action.GetParameters(), parameter => parameter.Name == "status");
        Assert.Contains(action.GetParameters(), parameter => parameter.Name == "asJson");

        var task = Assert.IsAssignableFrom<Task<IActionResult>>(action.Invoke(controller, [id, customerId, status, asJson]));
        return await task;
    }

    private static async Task<(Customer Customer, CommercialActivity Activity)> SeedActivityAsync(Orofoods.Web.Data.ApplicationDbContext db)
    {
        var customer = new Customer
        {
            LegalName = "Cliente Ltda",
            TradeName = "Cliente",
            Cnpj = "12.345.678/0001-99",
            Status = CustomerStatus.Approved,
            IsActive = true
        };
        var activity = new CommercialActivity
        {
            Customer = customer,
            Type = CommercialActivityType.Call,
            Status = CommercialActivityStatus.Scheduled,
            ScheduledAt = DateTime.Today.AddHours(10),
            Title = "Retorno"
        };
        db.CommercialActivities.Add(activity);
        await db.SaveChangesAsync();
        return (customer, activity);
    }

    private static CustomersController CreateController(Orofoods.Web.Data.ApplicationDbContext db, ClaimsPrincipal? user = null)
    {
        var accessService = new SalesRepresentativeAccessService(db);
        var paymentOptions = Options.Create(new PaymentEligibilityOptions());
        var priceService = new PriceService(db);
        var eligibilityService = new PaymentEligibilityService(db, paymentOptions);
        var controller = new CustomersController(
            db,
            new CustomerApprovalService(db),
            accessService,
            eligibilityService,
            priceService,
            new AssistedOrderService(db, priceService, eligibilityService, new OrderReservationService(db)),
            new CommercialAttentionService(db, accessService, Options.Create(new CrmOptions()), paymentOptions))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user ?? new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, "admin-user"),
                        new Claim(ClaimTypes.Role, ApplicationRoles.Administrator)
                    ], "test"))
                }
            }
        };
        return controller;
    }
}
