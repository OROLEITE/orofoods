using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Payments;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Controllers;

public sealed class AdminPointPaymentTests
{
    [Fact]
    public void Point_actions_require_admin_role_post_and_antiforgery()
    {
        var controller = typeof(OrdersController);
        var authorize = Assert.Single(controller.GetCustomAttributes<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());
        Assert.Equal("Administrador", authorize.Roles);
        foreach (var name in new[] { "StartPointCharge", "RefreshPointCharge", "CancelPointCharge", "PollPointCharge" })
        {
            var action = controller.GetMethod(name);
            Assert.NotNull(action);
            Assert.NotEmpty(action!.GetCustomAttributes<HttpPostAttribute>());
            Assert.NotEmpty(action.GetCustomAttributes<ValidateAntiForgeryTokenAttribute>());
        }
    }

    [Fact]
    public async Task Point_charge_is_not_invoked_when_feature_is_disabled()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await SeedCardOrderAsync(db);
        var point = new FakePointService();
        var controller = CreateController(db, point, enabled: false);

        var result = await controller.StartPointCharge(order.Id, 17, Guid.NewGuid().ToString("N"));

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(0, point.StartCalls);
        Assert.NotNull(controller.TempData["PointPaymentError"]);
    }

    [Fact]
    public async Task Point_charge_calls_service_with_order_selected_assignment_and_per_form_request_key()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await SeedCardOrderAsync(db);
        var point = new FakePointService();
        var controller = CreateController(db, point, enabled: true);
        var requestKey = Guid.NewGuid().ToString("N");

        var result = await controller.StartPointCharge(order.Id, 17, requestKey);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal((order.Id, 17, requestKey), point.StartRequest);
        Assert.NotNull(controller.TempData["PointPaymentMessage"]);
    }

    [Fact]
    public async Task Details_exposes_only_service_eligible_test_assignments_when_point_is_enabled()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await SeedCardOrderAsync(db);
        var point = new FakePointService();
        var controller = CreateController(db, point, enabled: true);

        var result = Assert.IsType<ViewResult>(await controller.Details(order.Id));

        Assert.True(Assert.IsType<bool>(controller.ViewBag.PointPaymentEnabled));
        Assert.Same(point.EligibleAssignments, controller.ViewBag.PointEligibleAssignments);
        Assert.Null(result.ViewName);
    }

    private static OrdersController CreateController(ApplicationDbContext db, FakePointService point, bool enabled, string environment = "Test")
    {
        var paymentOptions = Options.Create(new PaymentEligibilityOptions { CardOnDeliveryEnabled = true });
        var service = new AdminOrderService(db, TimeProvider.System, new OrderReservationService(db), new PaymentEligibilityService(db, paymentOptions), new PaymentService(db, paymentOptions, new PendingBoletoProvider()));
        var controller = new OrdersController(
            db,
            service,
            integrationService: null!,
            point,
            Options.Create(new MercadoPagoPointOptions { Enabled = enabled, Environment = environment, AccessToken = "fake" }),
            paymentOptions,
            new TestHostEnvironment(environment));
        var context = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        controller.TempData = new TempDataDictionary(context, new TestTempDataProvider());
        return controller;
    }

    private static async Task<Order> SeedCardOrderAsync(ApplicationDbContext db)
    {
        var customer = new Customer { LegalName = "Point Admin Ltda", TradeName = "Point Admin", Cnpj = Guid.NewGuid().ToString("N")[..14], Email = "point-admin@test.invalid" };
        var term = new PaymentTerm { Code = "CARD_ON_DELIVERY", Name = "Cartão na entrega", IsActive = true };
        var order = new Order { Customer = customer, PaymentTerm = term, Number = "ORO-POINT-1", Status = OrderStatus.OutForDelivery, Total = 55m };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    private sealed class FakePointService : IPointPaymentOrchestrationService
    {
        public int StartCalls { get; private set; }
        public (int OrderId, int AssignmentId, string RequestKey)? StartRequest { get; private set; }
        public IReadOnlyList<DriverPaymentTerminalAssignment> EligibleAssignments { get; } = [new DriverPaymentTerminalAssignment { Id = 17 }];
        public Task<PointPaymentOperationResult> StartChargeAsync(int orderId, int assignmentId, string requestKey, CancellationToken cancellationToken = default, string? adminUserId = null)
        {
            StartCalls++;
            StartRequest = (orderId, assignmentId, requestKey);
            return Task.FromResult(PointPaymentOperationResult.Success(new Payment { Id = 4, OrderId = orderId }));
        }
        public Task<PointPaymentOperationResult> RefreshAsync(int paymentId, CancellationToken cancellationToken = default, string? adminUserId = null) => throw new NotSupportedException();
        public Task<PointPaymentOperationResult> CancelAsync(int paymentId, CancellationToken cancellationToken = default, string? adminUserId = null) => throw new NotSupportedException();
        public Task<IReadOnlyList<DriverPaymentTerminalAssignment>> GetEligibleAssignmentsAsync(CancellationToken cancellationToken = default) => Task.FromResult(EligibleAssignments);
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class TestHostEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Orofoods.Web.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
