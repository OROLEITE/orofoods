using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orofoods.Web.Controllers.Api.V1;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Payments;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Controllers;

public class MercadoPagoWebhooksControllerTests
{
    [Fact]
    public async Task Invalid_signature_is_rejected_without_touching_local_state()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var payment = await SeedApprovablePaymentAsync(db);
        var controller = CreateController(db, new FakeGateway(PaymentStatus.Approved), signatureIsValid: false);
        SetRequest(controller, dataId: "MP-ORDER-1");

        var result = await controller.Receive(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Equal(PaymentStatus.Pending, (await db.Payments.FindAsync(payment.Id))!.Status);
    }

    [Fact]
    public async Task Valid_signature_reconciles_and_repeated_delivery_is_a_no_op()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var payment = await SeedApprovablePaymentAsync(db);
        var controller = CreateController(db, new FakeGateway(PaymentStatus.Approved), signatureIsValid: true);
        SetRequest(controller, dataId: "MP-ORDER-1");

        var firstResult = await controller.Receive(CancellationToken.None);
        var firstPaidAt = (await db.Payments.FindAsync(payment.Id))!.PaidAt;
        var secondResult = await controller.Receive(CancellationToken.None);
        var secondPaidAt = (await db.Payments.FindAsync(payment.Id))!.PaidAt;

        Assert.IsType<OkResult>(firstResult);
        Assert.IsType<OkResult>(secondResult);
        Assert.NotNull(firstPaidAt);
        Assert.Equal(firstPaidAt, secondPaidAt);
    }

    [Fact]
    public async Task Gateway_timeout_during_reconciliation_propagates_so_mercado_pago_retries()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        await SeedApprovablePaymentAsync(db);
        var controller = CreateController(db, new TimingOutGateway(), signatureIsValid: true);
        SetRequest(controller, dataId: "MP-ORDER-1");

        await Assert.ThrowsAsync<TaskCanceledException>(() => controller.Receive(CancellationToken.None));
    }

    [Fact]
    public async Task Missing_x_signature_header_is_rejected_by_the_real_validator()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var payment = await SeedApprovablePaymentAsync(db);
        var controller = CreateControllerWithRealValidator(db, new FakeGateway(PaymentStatus.Approved));
        SetRequest(controller, dataId: "MP-ORDER-1", signature: null);

        var result = await controller.Receive(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Equal(PaymentStatus.Pending, (await db.Payments.FindAsync(payment.Id))!.Status);
    }

    [Fact]
    public async Task Missing_x_request_id_header_is_rejected_by_the_real_validator()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var payment = await SeedApprovablePaymentAsync(db);
        var controller = CreateControllerWithRealValidator(db, new FakeGateway(PaymentStatus.Approved));
        SetRequest(controller, dataId: "MP-ORDER-1", requestId: null);

        var result = await controller.Receive(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Equal(PaymentStatus.Pending, (await db.Payments.FindAsync(payment.Id))!.Status);
    }

    [Fact]
    public async Task Missing_data_id_returns_bad_request_without_validating_signature()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var payment = await SeedApprovablePaymentAsync(db);
        var controller = CreateController(db, new FakeGateway(PaymentStatus.Approved), signatureIsValid: true);
        SetRequest(controller, dataId: null);

        var result = await controller.Receive(CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
        Assert.Equal(PaymentStatus.Pending, (await db.Payments.FindAsync(payment.Id))!.Status);
    }

    [Fact]
    public async Task Unsupported_notification_type_is_ignored_without_reconciling()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var payment = await SeedApprovablePaymentAsync(db);
        var controller = CreateController(db, new FakeGateway(PaymentStatus.Approved), signatureIsValid: true);
        SetRequest(controller, dataId: "MP-ORDER-1", type: "payment");

        var result = await controller.Receive(CancellationToken.None);

        Assert.IsType<OkResult>(result);
        Assert.Equal(PaymentStatus.Pending, (await db.Payments.FindAsync(payment.Id))!.Status);
    }

    [Fact]
    public async Task Order_type_notification_is_processed_when_type_is_present()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var payment = await SeedApprovablePaymentAsync(db);
        var controller = CreateController(db, new FakeGateway(PaymentStatus.Approved), signatureIsValid: true);
        SetRequest(controller, dataId: "MP-ORDER-1", type: "order");

        var result = await controller.Receive(CancellationToken.None);

        Assert.IsType<OkResult>(result);
        Assert.Equal(PaymentStatus.Approved, (await db.Payments.FindAsync(payment.Id))!.Status);
    }

    [Fact]
    public async Task Unknown_gateway_order_id_is_logged_and_does_not_fail_the_request()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        await SeedApprovablePaymentAsync(db);
        var controller = CreateController(db, new FakeGateway(PaymentStatus.Approved), signatureIsValid: true);
        SetRequest(controller, dataId: "MP-ORDER-UNKNOWN");

        var result = await controller.Receive(CancellationToken.None);

        Assert.IsType<OkResult>(result);
    }

    private static MercadoPagoWebhooksController CreateController(Orofoods.Web.Data.ApplicationDbContext db, IPaymentGateway gateway, bool signatureIsValid)
    {
        var orchestration = new PaymentOrchestrationService(
            db,
            new PaymentEligibilityService(db, Options.Create(new PaymentEligibilityOptions())),
            gateway,
            new NoOpPaymentApprovalHandler(),
            Options.Create(new MercadoPagoOptions()),
            TimeProvider.System,
            NullLogger<PaymentOrchestrationService>.Instance);
        var controller = new MercadoPagoWebhooksController(
            new FakeSignatureValidator(signatureIsValid),
            orchestration,
            NullLogger<MercadoPagoWebhooksController>.Instance);
        return controller;
    }

    private static MercadoPagoWebhooksController CreateControllerWithRealValidator(Orofoods.Web.Data.ApplicationDbContext db, IPaymentGateway gateway)
    {
        var orchestration = new PaymentOrchestrationService(
            db,
            new PaymentEligibilityService(db, Options.Create(new PaymentEligibilityOptions())),
            gateway,
            new NoOpPaymentApprovalHandler(),
            Options.Create(new MercadoPagoOptions()),
            TimeProvider.System,
            NullLogger<PaymentOrchestrationService>.Instance);
        var validator = new MercadoPagoWebhookSignatureValidator(
            Options.Create(new MercadoPagoOptions { WebhookSecret = "webhook-secret-not-real" }),
            TimeProvider.System);
        return new MercadoPagoWebhooksController(validator, orchestration, NullLogger<MercadoPagoWebhooksController>.Instance);
    }

    private static void SetRequest(
        MercadoPagoWebhooksController controller,
        string? dataId,
        string? signature = "ts=1,v1=deadbeef",
        string? requestId = "req-1",
        string? type = null)
    {
        var httpContext = new DefaultHttpContext();
        var query = dataId is null ? "" : $"?data.id={dataId}";
        if (type is not null)
        {
            query += query.Length == 0 ? $"?type={type}" : $"&type={type}";
        }
        httpContext.Request.QueryString = new QueryString(query);
        if (signature is not null) httpContext.Request.Headers["x-signature"] = signature;
        if (requestId is not null) httpContext.Request.Headers["x-request-id"] = requestId;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    private static async Task<Payment> SeedApprovablePaymentAsync(Orofoods.Web.Data.ApplicationDbContext db)
    {
        var customer = new Customer { LegalName = "Webhook Ltda", TradeName = "Webhook", Cnpj = Guid.NewGuid().ToString(), Email = "buyer@testuser.com" };
        var term = new PaymentTerm { Code = "PIX", Name = "PIX", DaysUntilDue = 0, IsActive = true };
        var order = new Order { Customer = customer, PaymentTerm = term, PaymentMethod = "PIX", Total = 40m, Status = OrderStatus.Received };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var payment = new Payment
        {
            OrderId = order.Id,
            CustomerId = customer.Id,
            PaymentMethod = "PIX",
            Method = PaymentMethodType.Pix,
            Amount = 40m,
            Status = PaymentStatus.Pending,
            Gateway = "MercadoPago",
            GatewayOrderId = "MP-ORDER-1",
            GatewayPaymentId = "MP-PAYMENT-1",
            ExternalReference = $"orofoods-order-{order.Id}",
            IdempotencyKey = Guid.NewGuid().ToString("N")
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        return payment;
    }

    private sealed class FakeSignatureValidator(bool isValid) : IMercadoPagoWebhookSignatureValidator
    {
        public bool IsValid(string? signature, string? requestId, string? dataId) => isValid;
    }

    private sealed class FakeGateway(PaymentStatus status) : IPaymentGateway
    {
        public Task<PaymentGatewayOrder> CreatePixAsync(CreatePixPaymentRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaymentGatewayOrder> CreateCreditCardPaymentAsync(CreateCreditCardPaymentRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaymentGatewayOrder> GetOrderAsync(string gatewayOrderId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaymentGatewayOrder(gatewayOrderId, "MP-PAYMENT-1", null, 40m, status, null, null, null, null, null));

        public Task<PaymentGatewayOrder> RefundAsync(RefundPaymentRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TimingOutGateway : IPaymentGateway
    {
        public Task<PaymentGatewayOrder> CreatePixAsync(CreatePixPaymentRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaymentGatewayOrder> CreateCreditCardPaymentAsync(CreateCreditCardPaymentRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaymentGatewayOrder> GetOrderAsync(string gatewayOrderId, CancellationToken cancellationToken = default) =>
            throw new TaskCanceledException("The request timed out.");

        public Task<PaymentGatewayOrder> RefundAsync(RefundPaymentRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
