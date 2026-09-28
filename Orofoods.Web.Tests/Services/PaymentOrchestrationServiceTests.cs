using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Payments;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class PaymentOrchestrationServiceTests
{
    [Fact]
    public async Task Pix_uses_server_amount_and_persists_display_data()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddOrderAsync(db, "PIX", 89.90m);
        var gateway = new FakeGateway
        {
            Next = GatewayOrder(PaymentStatus.Pending, amount: 89.90m, pixCode: "copy-code", pixBase64: "image-base64")
        };
        var service = CreateService(db, gateway);

        var payment = await service.CreatePixAsync(order.Id, order.CustomerId, "attempt-pix-1");

        Assert.Equal(89.90m, gateway.LastPixRequest!.Amount);
        Assert.Equal("attempt-pix-1", gateway.LastPixRequest.IdempotencyKey);
        Assert.Equal("copy-code", payment.PixCopyPaste);
        Assert.Equal("image-base64", payment.PixQrCodeBase64);
        Assert.Equal("MP-ORDER-1", payment.GatewayOrderId);
        Assert.Equal(PaymentMethodType.Pix, payment.Method);
        Assert.Equal(DateTimeKind.Utc, payment.UpdatedAt.Kind);
    }

    [Fact]
    public async Task Same_attempt_returns_existing_payment_without_a_second_charge()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddOrderAsync(db, "PIX", 20m);
        var gateway = new FakeGateway { Next = GatewayOrder(PaymentStatus.Pending, amount: 20m) };
        var service = CreateService(db, gateway);

        var first = await service.CreatePixAsync(order.Id, order.CustomerId, "same-attempt");
        var second = await service.CreatePixAsync(order.Id, order.CustomerId, "same-attempt");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, gateway.PixCalls);
        Assert.Single(db.Payments);
    }

    [Fact]
    public async Task Ambiguous_network_retry_reuses_the_persisted_key()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddOrderAsync(db, "PIX", 30m);
        var gateway = new FakeGateway
        {
            FailFirstPixCall = true,
            Next = GatewayOrder(PaymentStatus.Pending, amount: 30m)
        };
        var service = CreateService(db, gateway);

        await Assert.ThrowsAsync<HttpRequestException>(() => service.CreatePixAsync(order.Id, order.CustomerId, "retry-key"));
        var persisted = Assert.Single(db.Payments);
        Assert.Equal("retry-key", persisted.IdempotencyKey);
        Assert.Null(persisted.GatewayOrderId);

        var retried = await service.CreatePixAsync(order.Id, order.CustomerId, "retry-key");

        Assert.Equal(persisted.Id, retried.Id);
        Assert.Equal(["retry-key", "retry-key"], gateway.PixKeys);
    }

    [Fact]
    public async Task Card_token_is_forwarded_but_never_persisted()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddOrderAsync(db, "CREDIT_CARD", 125m);
        var gateway = new FakeGateway { Next = GatewayOrder(PaymentStatus.Rejected, amount: 125m, cardBrand: "visa", installments: 2) };
        var service = CreateService(db, gateway);

        var payment = await service.CreateCreditCardAsync(
            order.Id, order.CustomerId, "card-attempt", "one-use-browser-token", "visa", 2);

        Assert.Equal("one-use-browser-token", gateway.LastCardRequest!.CardToken);
        Assert.Equal(PaymentStatus.Rejected, payment.Status);
        Assert.Equal("visa", payment.CardBrand);
        Assert.DoesNotContain("one-use-browser-token", db.Entry(payment).CurrentValues.Properties.Select(property => db.Entry(payment).Property(property.Name).CurrentValue?.ToString()));
    }

    [Fact]
    public async Task Card_creation_approved_persists_gateway_ids_brand_last_four_installments_and_paid_at()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddOrderAsync(db, "CREDIT_CARD", 200m);
        var gateway = new FakeGateway
        {
            Next = GatewayOrder(PaymentStatus.Approved, amount: 200m, cardBrand: "mastercard", installments: 3, lastFourDigits: "4242", authorizationCode: "AUTH-123")
        };
        var service = CreateService(db, gateway);

        var payment = await service.CreateCreditCardAsync(order.Id, order.CustomerId, "card-approved", "token-1", "mastercard", 3);

        Assert.Equal(PaymentStatus.Approved, payment.Status);
        Assert.Equal("MP-ORDER-1", payment.GatewayOrderId);
        Assert.Equal("MP-PAYMENT-1", payment.GatewayPaymentId);
        Assert.Equal("mastercard", payment.CardBrand);
        Assert.Equal("4242", payment.LastFourDigits);
        Assert.Equal(3, payment.Installments);
        Assert.Equal("AUTH-123", payment.AuthorizationCode);
        Assert.NotNull(payment.PaidAt);
        Assert.Equal(DateTimeKind.Utc, payment.PaidAt!.Value.Kind);
    }

    [Fact]
    public async Task Card_creation_pending_persists_status_without_paid_at()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddOrderAsync(db, "CREDIT_CARD", 90m);
        var gateway = new FakeGateway { Next = GatewayOrder(PaymentStatus.Pending, amount: 90m, cardBrand: "visa", installments: 1) };
        var service = CreateService(db, gateway);

        var payment = await service.CreateCreditCardAsync(order.Id, order.CustomerId, "card-pending", "token-2", "visa", 1);

        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Null(payment.PaidAt);
    }

    [Fact]
    public async Task Card_second_attempt_after_rejection_creates_a_new_payment_and_preserves_the_rejected_one()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddOrderAsync(db, "CREDIT_CARD", 60m);
        var originalOrderTotal = order.Total;
        var originalOrderStatus = order.Status;
        var gateway = new FakeGateway
        {
            Next = GatewayOrder(
                PaymentStatus.Rejected,
                amount: 60m,
                cardBrand: "visa",
                installments: 1,
                gatewayOrderId: "MP-ORDER-REJECTED",
                gatewayPaymentId: "MP-PAYMENT-REJECTED")
        };
        var service = CreateService(db, gateway);

        var rejected = await service.CreateCreditCardAsync(order.Id, order.CustomerId, "attempt-1", "token-a", "visa", 1);

        Assert.Equal(1, await db.Payments.CountAsync(payment => payment.OrderId == order.Id));
        Assert.Equal(PaymentMethodType.CreditCard, rejected.Method);
        Assert.Equal(PaymentStatus.Rejected, rejected.Status);
        Assert.Equal(originalOrderTotal, rejected.Amount);
        Assert.Null(rejected.PaidAt);
        Assert.Equal("MP-ORDER-REJECTED", rejected.GatewayOrderId);
        Assert.Equal("MP-PAYMENT-REJECTED", rejected.GatewayPaymentId);
        Assert.Equal("visa", rejected.CardBrand);
        Assert.Equal(1, rejected.Installments);
        Assert.Null(rejected.LastFourDigits);
        Assert.Null(rejected.AuthorizationCode);

        gateway.Next = GatewayOrder(
            PaymentStatus.Approved,
            amount: 60m,
            cardBrand: "visa",
            installments: 1,
            gatewayOrderId: "MP-ORDER-APPROVED",
            gatewayPaymentId: "MP-PAYMENT-APPROVED");
        var approved = await service.CreateCreditCardAsync(order.Id, order.CustomerId, "attempt-2", "token-b", "visa", 1);

        Assert.NotEqual(rejected.Id, approved.Id);
        Assert.Equal(2, await db.Payments.CountAsync(payment => payment.OrderId == order.Id));
        var reloadedRejected = await db.Payments.AsNoTracking().SingleAsync(payment => payment.Id == rejected.Id);
        Assert.Equal(PaymentStatus.Rejected, reloadedRejected.Status);
        Assert.Null(reloadedRejected.PaidAt);
        Assert.Equal("MP-ORDER-REJECTED", reloadedRejected.GatewayOrderId);
        Assert.Equal("attempt-1", reloadedRejected.IdempotencyKey);
        Assert.Equal("attempt-2", approved.IdempotencyKey);
        Assert.NotEqual(reloadedRejected.IdempotencyKey, approved.IdempotencyKey);
        Assert.Equal(PaymentStatus.Approved, approved.Status);
        Assert.NotNull(approved.PaidAt);
        Assert.Equal("MP-ORDER-APPROVED", approved.GatewayOrderId);
        Assert.Equal(originalOrderTotal, order.Total);
        Assert.Equal(originalOrderStatus, order.Status);
    }

    [Fact]
    public async Task Card_duplicate_click_with_the_same_attempt_key_does_not_create_a_second_payment()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddOrderAsync(db, "CREDIT_CARD", 60m);
        var gateway = new FakeGateway { Next = GatewayOrder(PaymentStatus.Approved, amount: 60m, cardBrand: "visa", installments: 1) };
        var service = CreateService(db, gateway);

        var first = await service.CreateCreditCardAsync(order.Id, order.CustomerId, "same-click", "token-a", "visa", 1);
        var second = await service.CreateCreditCardAsync(order.Id, order.CustomerId, "same-click", "token-a", "visa", 1);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await db.Payments.CountAsync(payment => payment.OrderId == order.Id));
    }

    [Fact]
    public async Task Card_ambiguous_network_retry_reuses_the_persisted_idempotency_key()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddOrderAsync(db, "CREDIT_CARD", 60m);
        var gateway = new FailFirstCardCallGateway { Next = GatewayOrder(PaymentStatus.Approved, amount: 60m, cardBrand: "visa", installments: 1) };
        var service = CreateService(db, gateway);

        await Assert.ThrowsAsync<HttpRequestException>(() => service.CreateCreditCardAsync(order.Id, order.CustomerId, "retry-key", "token-a", "visa", 1));
        var persisted = Assert.Single(await db.Payments.Where(payment => payment.OrderId == order.Id).ToListAsync());
        Assert.Null(persisted.GatewayOrderId);

        var retried = await service.CreateCreditCardAsync(order.Id, order.CustomerId, "retry-key", "token-a", "visa", 1);

        Assert.Equal(persisted.Id, retried.Id);
        Assert.Equal(1, await db.Payments.CountAsync(payment => payment.OrderId == order.Id));
    }

    [Fact]
    public async Task Card_webhook_reconciliation_approves_and_replay_does_not_duplicate_paid_at()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddOrderAsync(db, "CREDIT_CARD", 60m);
        var payment = await AddCardGatewayPaymentAsync(db, order, PaymentStatus.Pending);
        var gateway = new FakeGateway { Next = GatewayOrder(PaymentStatus.Approved, amount: 60m, cardBrand: "visa", installments: 1) };
        var service = CreateService(db, gateway);

        await service.ReconcileMercadoPagoOrderAsync("MP-ORDER-1");
        var firstPaidAt = payment.PaidAt;
        await service.ReconcileMercadoPagoOrderAsync("MP-ORDER-1");

        Assert.Equal(PaymentStatus.Approved, payment.Status);
        Assert.NotNull(firstPaidAt);
        Assert.Equal(firstPaidAt, payment.PaidAt);
    }

    [Fact]
    public async Task Duplicate_approved_webhook_sets_paid_at_and_calls_future_hook_once()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddOrderAsync(db, "PIX", 40m);
        var payment = await AddGatewayPaymentAsync(db, order, PaymentStatus.Pending);
        var gateway = new FakeGateway { Next = GatewayOrder(PaymentStatus.Approved, amount: 40m) };
        var approval = new RecordingApprovalHandler();
        var service = CreateService(db, gateway, approval);

        await service.ReconcileMercadoPagoOrderAsync("MP-ORDER-1");
        var firstPaidAt = payment.PaidAt;
        await service.ReconcileMercadoPagoOrderAsync("MP-ORDER-1");

        Assert.Equal(PaymentStatus.Approved, payment.Status);
        Assert.NotNull(firstPaidAt);
        Assert.Equal(firstPaidAt, payment.PaidAt);
        Assert.Equal(1, approval.Calls);
        Assert.Equal(2, gateway.GetCalls);
    }

    [Fact]
    public async Task Authoritative_gateway_expiration_marks_pix_expired()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddOrderAsync(db, "PIX", 40m);
        var payment = await AddGatewayPaymentAsync(db, order, PaymentStatus.Pending);
        var gateway = new FakeGateway { Next = GatewayOrder(PaymentStatus.Expired, amount: 40m) };
        var service = CreateService(db, gateway);

        await service.ReconcileMercadoPagoOrderAsync("MP-ORDER-1");

        Assert.Equal(PaymentStatus.Expired, payment.Status);
        Assert.Null(payment.PaidAt);
    }

    [Fact]
    public async Task Gateway_amount_mismatch_is_rejected_without_changing_local_status()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddOrderAsync(db, "PIX", 40m);
        var payment = await AddGatewayPaymentAsync(db, order, PaymentStatus.Pending);
        var gateway = new FakeGateway { Next = GatewayOrder(PaymentStatus.Approved, amount: 41m) };
        var service = CreateService(db, gateway);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReconcileMercadoPagoOrderAsync("MP-ORDER-1"));

        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Null(payment.PaidAt);
    }

    [Fact]
    public async Task Pending_transitions_to_rejected()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddOrderAsync(db, "PIX", 40m);
        var payment = await AddGatewayPaymentAsync(db, order, PaymentStatus.Pending);
        var gateway = new FakeGateway { Next = GatewayOrder(PaymentStatus.Rejected, amount: 40m) };
        var service = CreateService(db, gateway);

        var applied = await service.ReconcileMercadoPagoOrderAsync("MP-ORDER-1");

        Assert.True(applied);
        Assert.Equal(PaymentStatus.Rejected, payment.Status);
        Assert.Null(payment.PaidAt);
    }

    [Fact]
    public async Task Reconciliation_of_an_unchanged_status_is_a_no_op()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddOrderAsync(db, "PIX", 40m);
        var payment = await AddGatewayPaymentAsync(db, order, PaymentStatus.Rejected);
        var gateway = new FakeGateway { Next = GatewayOrder(PaymentStatus.Rejected, amount: 40m) };
        var service = CreateService(db, gateway);

        var applied = await service.ReconcileMercadoPagoOrderAsync("MP-ORDER-1");

        Assert.True(applied);
        Assert.Equal(PaymentStatus.Rejected, payment.Status);
    }

    [Fact]
    public async Task A_stale_notification_cannot_regress_an_already_approved_payment()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddOrderAsync(db, "PIX", 40m);
        var payment = await AddGatewayPaymentAsync(db, order, PaymentStatus.Approved);
        payment.PaidAt = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();
        var gateway = new FakeGateway { Next = GatewayOrder(PaymentStatus.Pending, amount: 40m) };
        var service = CreateService(db, gateway);

        var applied = await service.ReconcileMercadoPagoOrderAsync("MP-ORDER-1");

        Assert.False(applied);
        Assert.Equal(PaymentStatus.Approved, payment.Status);
        Assert.Equal(new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc), payment.PaidAt);
    }

    [Fact]
    public async Task Rejected_webhook_cannot_regress_an_already_approved_payment()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddOrderAsync(db, "CREDIT_CARD", 40m);
        var payment = await AddCardGatewayPaymentAsync(db, order, PaymentStatus.Approved);
        payment.PaidAt = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();
        var gateway = new FakeGateway { Next = GatewayOrder(PaymentStatus.Rejected, amount: 40m, cardBrand: "visa", installments: 1) };
        var service = CreateService(db, gateway);

        var applied = await service.ReconcileMercadoPagoOrderAsync("MP-ORDER-1");

        Assert.False(applied);
        Assert.Equal(PaymentStatus.Approved, payment.Status);
        Assert.Equal(new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc), payment.PaidAt);
    }

    [Fact]
    public async Task An_approved_payment_can_still_move_forward_to_refunded()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddOrderAsync(db, "PIX", 40m);
        var payment = await AddGatewayPaymentAsync(db, order, PaymentStatus.Approved);
        payment.PaidAt = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();
        var gateway = new FakeGateway { Next = GatewayOrder(PaymentStatus.Refunded, amount: 40m) };
        var service = CreateService(db, gateway);

        var applied = await service.ReconcileMercadoPagoOrderAsync("MP-ORDER-1");

        Assert.True(applied);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
    }

    private static PaymentOrchestrationService CreateService(
        ApplicationDbContext db,
        IPaymentGateway gateway,
        IPaymentApprovalHandler? approvalHandler = null)
    {
        var eligibility = new PaymentEligibilityService(db, Options.Create(new PaymentEligibilityOptions()));
        return new PaymentOrchestrationService(
            db,
            eligibility,
            gateway,
            approvalHandler ?? new RecordingApprovalHandler(),
            Options.Create(new MercadoPagoOptions { PixExpiration = TimeSpan.FromMinutes(30) }),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-09-17T12:00:00Z")),
            NullLogger<PaymentOrchestrationService>.Instance);
    }

    private static async Task<Order> AddOrderAsync(ApplicationDbContext db, string paymentCode, decimal total)
    {
        var customer = new Customer
        {
            LegalName = "Gateway Customer Ltda",
            TradeName = "Gateway Customer",
            Cnpj = Guid.NewGuid().ToString("N")[..14],
            Email = "buyer@testuser.com"
        };
        var term = new PaymentTerm
        {
            Code = paymentCode,
            Name = paymentCode,
            DaysUntilDue = 0,
            IsActive = true
        };
        var order = new Order
        {
            Customer = customer,
            PaymentTerm = term,
            PaymentMethod = paymentCode,
            Total = total,
            Status = OrderStatus.Received
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    private static async Task<Payment> AddGatewayPaymentAsync(ApplicationDbContext db, Order order, PaymentStatus status)
    {
        var payment = new Payment
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            PaymentMethod = "PIX",
            Method = PaymentMethodType.Pix,
            Amount = order.Total,
            Status = status,
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

    private static async Task<Payment> AddCardGatewayPaymentAsync(ApplicationDbContext db, Order order, PaymentStatus status)
    {
        var payment = new Payment
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            PaymentMethod = "CREDIT_CARD",
            Method = PaymentMethodType.CreditCard,
            Amount = order.Total,
            Status = status,
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

    private static PaymentGatewayOrder GatewayOrder(
        PaymentStatus status,
        decimal amount,
        string? pixCode = null,
        string? pixBase64 = null,
        string? cardBrand = null,
        int? installments = null,
        string? lastFourDigits = null,
        string? authorizationCode = null,
        string gatewayOrderId = "MP-ORDER-1",
        string gatewayPaymentId = "MP-PAYMENT-1") =>
        new(
            gatewayOrderId,
            gatewayPaymentId,
            null,
            amount,
            status,
            pixCode,
            pixBase64,
            DateTime.Parse("2026-09-17T12:30:00Z").ToUniversalTime(),
            cardBrand,
            installments,
            lastFourDigits,
            authorizationCode);

    private sealed class FakeGateway : IPaymentGateway
    {
        public PaymentGatewayOrder Next { get; set; } = GatewayOrder(PaymentStatus.Pending, 0m);
        public bool FailFirstPixCall { get; set; }
        public int PixCalls { get; private set; }
        public int GetCalls { get; private set; }
        public List<string> PixKeys { get; } = [];
        public CreatePixPaymentRequest? LastPixRequest { get; private set; }
        public CreateCreditCardPaymentRequest? LastCardRequest { get; private set; }

        public Task<PaymentGatewayOrder> CreatePixAsync(CreatePixPaymentRequest request, CancellationToken cancellationToken = default)
        {
            PixCalls++;
            PixKeys.Add(request.IdempotencyKey);
            LastPixRequest = request;
            if (FailFirstPixCall && PixCalls == 1) throw new HttpRequestException("ambiguous transport failure");
            return Task.FromResult(Next with { ExternalReference = request.ExternalReference });
        }

        public Task<PaymentGatewayOrder> CreateCreditCardPaymentAsync(CreateCreditCardPaymentRequest request, CancellationToken cancellationToken = default)
        {
            LastCardRequest = request;
            return Task.FromResult(Next with { ExternalReference = request.ExternalReference });
        }

        public Task<PaymentGatewayOrder> GetOrderAsync(string gatewayOrderId, CancellationToken cancellationToken = default)
        {
            GetCalls++;
            return Task.FromResult(Next);
        }

        public Task<PaymentGatewayOrder> RefundAsync(RefundPaymentRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Next);
    }

    private sealed class FailFirstCardCallGateway : IPaymentGateway
    {
        public PaymentGatewayOrder Next { get; set; } = GatewayOrder(PaymentStatus.Pending, 0m);
        private int _cardCalls;

        public Task<PaymentGatewayOrder> CreatePixAsync(CreatePixPaymentRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaymentGatewayOrder> CreateCreditCardPaymentAsync(CreateCreditCardPaymentRequest request, CancellationToken cancellationToken = default)
        {
            _cardCalls++;
            if (_cardCalls == 1) throw new HttpRequestException("ambiguous transport failure");
            return Task.FromResult(Next with { ExternalReference = request.ExternalReference });
        }

        public Task<PaymentGatewayOrder> GetOrderAsync(string gatewayOrderId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Next);

        public Task<PaymentGatewayOrder> RefundAsync(RefundPaymentRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Next);
    }

    private sealed class RecordingApprovalHandler : IPaymentApprovalHandler
    {
        public int Calls { get; private set; }

        public Task PaymentApprovedAsync(Payment payment, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
