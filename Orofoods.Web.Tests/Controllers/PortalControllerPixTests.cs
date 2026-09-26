using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orofoods.Web.Controllers;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Services.Catalog;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Payments;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Tests.Infrastructure;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Tests.Controllers;

public class PortalControllerPixTests
{
    [Fact]
    public async Task Pix_returns_the_owned_payment_from_local_storage()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (payment, user) = await SeedPaymentAsync(db);
        var controller = CreateController(db, user.Id);

        var result = await controller.Pix(payment.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PixPaymentViewModel>(view.Model);
        Assert.Equal(payment.Id, model.PaymentId);
        Assert.Equal(payment.OrderId, model.OrderId);
    }

    [Fact]
    public async Task Pix_returns_not_found_for_another_customers_payment()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (payment, _) = await SeedPaymentAsync(db);
        var otherUser = await SeedApprovedUserAsync(db, "other-user", "22.222.222/0001-22");
        var controller = CreateController(db, otherUser.Id);

        var result = await controller.Pix(payment.Id);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Pix_returns_not_found_when_payment_does_not_exist()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (_, user) = await SeedPaymentAsync(db);
        var controller = CreateController(db, user.Id);

        var result = await controller.Pix(999_999);

        Assert.IsType<NotFoundResult>(result);
    }

    private static async Task<(Payment Payment, ApplicationUser User)> SeedPaymentAsync(Orofoods.Web.Data.ApplicationDbContext db)
    {
        var user = await SeedApprovedUserAsync(db, "owner-user", "11.111.111/0001-11");
        var order = new Order
        {
            Number = "ORO-2026-000057",
            CustomerId = user.CustomerId!.Value,
            CreatedByUserId = user.Id,
            Total = 119.90m,
            PaymentMethod = "PIX"
        };
        var payment = new Payment
        {
            Order = order,
            CustomerId = user.CustomerId.Value,
            Method = PaymentMethodType.Pix,
            PaymentMethod = "PIX",
            Amount = order.Total,
            Status = PaymentStatus.Pending,
            PixCopyPaste = "codigo-persistido",
            PixQrCodeBase64 = "base64-persistido",
            ExpiresAt = new DateTime(2026, 9, 17, 20, 51, 0, DateTimeKind.Utc)
        };
        db.AddRange(order, payment);
        await db.SaveChangesAsync();
        return (payment, user);
    }

    private static async Task<ApplicationUser> SeedApprovedUserAsync(
        Orofoods.Web.Data.ApplicationDbContext db,
        string userId,
        string cnpj)
    {
        var customer = new Customer
        {
            LegalName = $"{userId} Ltda",
            TradeName = userId,
            Cnpj = cnpj,
            Status = CustomerStatus.Approved,
            IsActive = true
        };
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"{userId}@orofoods.local",
            NormalizedUserName = $"{userId}@orofoods.local".ToUpperInvariant(),
            Email = $"{userId}@orofoods.local",
            NormalizedEmail = $"{userId}@orofoods.local".ToUpperInvariant(),
            Customer = customer,
            IsActive = true
        };
        db.AddRange(customer, user);
        await db.SaveChangesAsync();
        return user;
    }

    private static PortalController CreateController(Orofoods.Web.Data.ApplicationDbContext db, string userId)
    {
        var priceService = new PriceService(db);
        var eligibility = new PaymentEligibilityService(db, Options.Create(new PaymentEligibilityOptions()));
        var controller = new PortalController(
            db,
            TestIdentityFactory.CreateUserManager(db),
            new CustomerAccessService(db),
            new AdminCustomerContextService(),
            priceService,
            new CartService(db, priceService),
            new CustomerDashboardService(db, new FrequentProductService(db, priceService), TimeProvider.System),
            new SavedOrderService(db),
            new OrderReservationService(db),
            eligibility,
            new PaymentOrchestrationService(
                db,
                eligibility,
                new UnusedPaymentGateway(),
                new NoOpPaymentApprovalHandler(),
                Options.Create(new MercadoPagoOptions()),
                TimeProvider.System,
                NullLogger<PaymentOrchestrationService>.Instance),
            Options.Create(new MercadoPagoOptions()),
            new AssistedOrderService(db, priceService, new PaymentEligibilityService(db, Options.Create(new PaymentEligibilityOptions())), new OrderReservationService(db)));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId)],
                    "test"))
            }
        };
        return controller;
    }

    private sealed class UnusedPaymentGateway : IPaymentGateway
    {
        public Task<PaymentGatewayOrder> CreatePixAsync(CreatePixPaymentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PaymentGatewayOrder> CreateCreditCardPaymentAsync(CreateCreditCardPaymentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PaymentGatewayOrder> GetOrderAsync(string gatewayOrderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PaymentGatewayOrder> RefundAsync(RefundPaymentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
