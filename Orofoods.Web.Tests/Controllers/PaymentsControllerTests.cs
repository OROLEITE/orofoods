using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orofoods.Web.Controllers.Api.V1;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Payments;
using Orofoods.Web.Tests.Infrastructure;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Tests.Controllers;

public class PaymentsControllerTests
{
    [Fact]
    public async Task Owner_can_create_a_pix_attempt_and_read_it_back()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (customer, user) = await SeedCustomerAsync(db);
        var order = await SeedOrderAsync(db, customer, "PIX");
        var controller = await CreateControllerAsync(db, user);

        var createResult = await controller.CreatePix(new CreatePixAttemptRequest { OrderId = order.Id, IdempotencyKey = "attempt-1" }, CancellationToken.None);
        var created = Assert.IsType<PaymentAttemptResponse>(Assert.IsType<OkObjectResult>(createResult.Result).Value);

        var readResult = await controller.Get(created.Id, CancellationToken.None);
        var read = Assert.IsType<PaymentAttemptResponse>(Assert.IsType<OkObjectResult>(readResult.Result).Value);

        Assert.Equal(PaymentMethodType.Pix, created.Method);
        Assert.Equal(created.Id, read.Id);
    }

    [Fact]
    public async Task Owner_can_create_a_card_attempt_and_read_it_back()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (customer, user) = await SeedCustomerAsync(db);
        var order = await SeedOrderAsync(db, customer, "CREDIT_CARD");
        var controller = await CreateControllerAsync(db, user);

        var createResult = await controller.CreateCard(
            new CreateCardAttemptRequest { OrderId = order.Id, IdempotencyKey = "card-attempt-1", CardToken = "browser-token", PaymentMethodId = "visa", Installments = 2 },
            CancellationToken.None);
        var created = Assert.IsType<PaymentAttemptResponse>(Assert.IsType<OkObjectResult>(createResult.Result).Value);

        var readResult = await controller.Get(created.Id, CancellationToken.None);
        var read = Assert.IsType<PaymentAttemptResponse>(Assert.IsType<OkObjectResult>(readResult.Result).Value);

        Assert.Equal(PaymentMethodType.CreditCard, created.Method);
        Assert.Equal(created.Id, read.Id);
    }

    [Fact]
    public void CreateCardAttemptRequest_never_accepts_amount_or_customer_from_the_client()
    {
        // Amount and CustomerId are always derived server-side from the authenticated customer's order;
        // the client can only supply tokenization output and non-financial metadata.
        var properties = typeof(CreateCardAttemptRequest).GetProperties().Select(property => property.Name);

        Assert.DoesNotContain("Amount", properties);
        Assert.DoesNotContain("CustomerId", properties);
        Assert.DoesNotContain("Total", properties);
    }

    [Fact]
    public async Task Customer_cannot_read_another_customers_payment()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (ownerCustomer, _) = await SeedCustomerAsync(db, cnpj: "11.111.111/0001-11", userId: "owner-user");
        var order = await SeedOrderAsync(db, ownerCustomer, "PIX");
        var payment = new Payment
        {
            OrderId = order.Id,
            CustomerId = ownerCustomer.Id,
            PaymentMethod = "PIX",
            Method = PaymentMethodType.Pix,
            Amount = order.Total,
            IdempotencyKey = "owner-key"
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var (_, otherUser) = await SeedCustomerAsync(db, cnpj: "22.222.222/0001-22", userId: "other-user");
        var controller = await CreateControllerAsync(db, otherUser);

        var result = await controller.Get(payment.Id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    private static async Task<PaymentsController> CreateControllerAsync(Orofoods.Web.Data.ApplicationDbContext db, ApplicationUser user)
    {
        var gateway = new FakeGateway();
        var orchestration = new PaymentOrchestrationService(
            db,
            new PaymentEligibilityService(db, Options.Create(new PaymentEligibilityOptions())),
            gateway,
            new NoOpPaymentApprovalHandler(),
            Options.Create(new MercadoPagoOptions()),
            TimeProvider.System,
            NullLogger<PaymentOrchestrationService>.Instance);
        var controller = new PaymentsController(
            orchestration,
            TestIdentityFactory.CreateUserManager(db),
            new CustomerAccessService(db),
            new AdminCustomerContextService());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id)], "test")),
                Session = new TestSession()
            }
        };
        return controller;
    }

    private static async Task<(Customer Customer, ApplicationUser User)> SeedCustomerAsync(
        Orofoods.Web.Data.ApplicationDbContext db, string cnpj = "12.345.678/0001-99", string userId = "customer-user")
    {
        var customer = new Customer
        {
            LegalName = "Cliente Ltda",
            TradeName = "Cliente",
            Cnpj = cnpj,
            Status = CustomerStatus.Approved,
            IsActive = true,
            Email = "cliente@testuser.com"
        };
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"{userId}@orofoods.local",
            NormalizedUserName = $"{userId}@ORFOODS.LOCAL".ToUpperInvariant(),
            Email = $"{userId}@orofoods.local",
            NormalizedEmail = $"{userId}@orofoods.local".ToUpperInvariant(),
            Customer = customer,
            IsActive = true
        };
        db.AddRange(customer, user);
        await db.SaveChangesAsync();
        return (customer, user);
    }

    private static async Task<Order> SeedOrderAsync(Orofoods.Web.Data.ApplicationDbContext db, Customer customer, string paymentCode)
    {
        var term = new PaymentTerm { Code = paymentCode, Name = paymentCode, DaysUntilDue = 0, IsActive = true };
        var order = new Order { Customer = customer, PaymentTerm = term, PaymentMethod = paymentCode, Total = 50m, Status = OrderStatus.Received };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    private sealed class FakeGateway : IPaymentGateway
    {
        public Task<PaymentGatewayOrder> CreatePixAsync(CreatePixPaymentRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaymentGatewayOrder("MP-1", "MP-PAY-1", request.ExternalReference, request.Amount, PaymentStatus.Pending, "copy-paste", "base64", DateTime.UtcNow.AddMinutes(30), null, null));

        public Task<PaymentGatewayOrder> CreateCreditCardPaymentAsync(CreateCreditCardPaymentRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaymentGatewayOrder("MP-1", "MP-PAY-1", request.ExternalReference, request.Amount, PaymentStatus.Approved, null, null, null, "visa", request.Installments));

        public Task<PaymentGatewayOrder> GetOrderAsync(string gatewayOrderId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaymentGatewayOrder> RefundAsync(RefundPaymentRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> values = [];
        public IEnumerable<string> Keys => values.Keys;
        public string Id => "test";
        public bool IsAvailable => true;
        public void Clear() => values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
    }
}
