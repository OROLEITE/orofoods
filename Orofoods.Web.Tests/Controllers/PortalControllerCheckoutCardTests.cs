using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orofoods.Web.Controllers;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Payments;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Tests.Infrastructure;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Tests.Controllers;

/// <summary>End-to-end (in-process) checkout tests for the credit-card rejected/retry scenarios, using a fake gateway only.</summary>
public class PortalControllerCheckoutCardTests
{
    [Fact]
    public async Task Rejected_card_persists_a_single_payment_without_deleting_the_order_or_marking_it_paid()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (customer, address, product, creditCardTerm) = await SeedCheckoutContextAsync(db);
        var session = new TestSession();
        session.SetString("orofoods-cart-product-ids", $"{{\"{product.Id}\":1}}");
        var gateway = new ConfigurableCardGateway
        {
            Next = new PaymentGatewayOrder("MP-ORD-REJ-1", "MP-PAY-REJ-1", null, product.BasePrice, PaymentStatus.Rejected, null, null, null, "visa", 1)
        };
        var controller = CreateController(db, session, gateway);

        var result = await controller.Checkout(new CheckoutViewModel
        {
            AddressId = address.Id,
            PaymentTermId = creditCardTerm.Id,
            RequestedDeliveryDate = DateTime.Today.AddDays(1),
            AttemptKey = "attempt-rejected-1",
            CardToken = "browser-token-1",
            CardPaymentMethodId = "visa",
            CardInstallments = 1
        });

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        var message = controller.ModelState[""]!.Errors.Single().ErrorMessage;
        Assert.Equal("O pagamento não foi aprovado. Tente novamente ou selecione outra condição de pagamento.", message);
        Assert.DoesNotContain("MP-ORD-REJ-1", message, StringComparison.Ordinal);
        Assert.DoesNotContain("MP-PAY-REJ-1", message, StringComparison.Ordinal);
        Assert.DoesNotContain("browser-token-1", message, StringComparison.Ordinal);
        Assert.DoesNotContain("failed", message, StringComparison.OrdinalIgnoreCase);

        var order = await db.Orders.AsNoTracking().SingleAsync(x => x.CustomerId == customer.Id);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(product.BasePrice, order.Total);

        var payment = await db.Payments.AsNoTracking().SingleAsync(x => x.OrderId == order.Id);
        Assert.Equal(PaymentMethodType.CreditCard, payment.Method);
        Assert.Equal(PaymentStatus.Rejected, payment.Status);
        Assert.Equal(order.Total, payment.Amount);
        Assert.Null(payment.PaidAt);
        Assert.Equal("MP-ORD-REJ-1", payment.GatewayOrderId);
        Assert.Equal("MP-PAY-REJ-1", payment.GatewayPaymentId);
        Assert.Equal("visa", payment.CardBrand);
        Assert.Equal(1, payment.Installments);
        Assert.Null(payment.LastFourDigits);
        Assert.Null(payment.AuthorizationCode);
        Assert.Equal(1, await db.Payments.CountAsync());
    }

    [Fact]
    public async Task Second_attempt_after_rejection_creates_a_new_order_and_payment_that_can_be_approved()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (customer, address, product, creditCardTerm) = await SeedCheckoutContextAsync(db);
        var session = new TestSession();
        session.SetString("orofoods-cart-product-ids", $"{{\"{product.Id}\":1}}");
        var gateway = new ConfigurableCardGateway
        {
            Next = new PaymentGatewayOrder("MP-ORD-REJ-2", "MP-PAY-REJ-2", null, product.BasePrice, PaymentStatus.Rejected, null, null, null, "visa", 1)
        };
        var controller = CreateController(db, session, gateway);

        await controller.Checkout(new CheckoutViewModel
        {
            AddressId = address.Id,
            PaymentTermId = creditCardTerm.Id,
            RequestedDeliveryDate = DateTime.Today.AddDays(1),
            AttemptKey = "attempt-rejected-2",
            CardToken = "browser-token-2",
            CardPaymentMethodId = "visa",
            CardInstallments = 1
        });

        // The cart is preserved after a rejection so the customer can try again with a new attempt.
        gateway.Next = new PaymentGatewayOrder("MP-ORD-APR-2", "MP-PAY-APR-2", null, product.BasePrice, PaymentStatus.Approved, null, null, null, "visa", 1);
        controller = CreateController(db, session, gateway);

        var secondResult = await controller.Checkout(new CheckoutViewModel
        {
            AddressId = address.Id,
            PaymentTermId = creditCardTerm.Id,
            RequestedDeliveryDate = DateTime.Today.AddDays(1),
            AttemptKey = "attempt-approved-2",
            CardToken = "browser-token-3",
            CardPaymentMethodId = "visa",
            CardInstallments = 1
        });

        var redirect = Assert.IsType<RedirectToActionResult>(secondResult);
        Assert.Equal("Success", redirect.ActionName);

        Assert.Equal(2, await db.Orders.CountAsync(x => x.CustomerId == customer.Id));
        var payments = await db.Payments.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(2, payments.Count);
        Assert.Equal(PaymentStatus.Rejected, payments[0].Status);
        Assert.Null(payments[0].PaidAt);
        Assert.Equal("attempt-rejected-2", payments[0].IdempotencyKey);
        Assert.Equal(PaymentStatus.Approved, payments[1].Status);
        Assert.NotNull(payments[1].PaidAt);
        Assert.Equal("attempt-approved-2", payments[1].IdempotencyKey);
        Assert.NotEqual(payments[0].IdempotencyKey, payments[1].IdempotencyKey);
    }

    [Fact]
    public async Task Duplicate_card_submit_with_the_same_attempt_key_reuses_the_existing_order()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (customer, address, product, creditCardTerm) = await SeedCheckoutContextAsync(db);
        var session = new TestSession();
        session.SetString("orofoods-cart-product-ids", $"{{\"{product.Id}\":1}}");
        var gateway = new ConfigurableCardGateway
        {
            Next = new PaymentGatewayOrder("MP-ORD-DUP-CARD", "MP-PAY-DUP-CARD", null, product.BasePrice, PaymentStatus.Approved, null, null, null, "visa", 1)
        };
        var controller = CreateController(db, session, gateway);
        var input = new CheckoutViewModel
        {
            AddressId = address.Id,
            PaymentTermId = creditCardTerm.Id,
            RequestedDeliveryDate = DateTime.Today.AddDays(1),
            AttemptKey = "attempt-duplicate-card",
            CardToken = "browser-token-duplicate",
            CardPaymentMethodId = "visa",
            CardInstallments = 1
        };

        var firstResult = await controller.Checkout(input);
        var secondResult = await controller.Checkout(input);

        var firstRedirect = Assert.IsType<RedirectToActionResult>(firstResult);
        var secondRedirect = Assert.IsType<RedirectToActionResult>(secondResult);
        Assert.Equal("Success", firstRedirect.ActionName);
        Assert.Equal(firstRedirect.RouteValues!["id"], secondRedirect.RouteValues!["id"]);
        Assert.Equal(1, await db.Orders.CountAsync(x => x.CustomerId == customer.Id));
        Assert.Equal(1, await db.Payments.CountAsync());
        Assert.Equal(1, await db.InventoryReservations.CountAsync(x => x.Status == InventoryReservationStatus.Active));
        Assert.Equal(1, await db.ProductInventories.Where(x => x.ProductId == product.Id).Select(x => x.QuantityReserved).SingleAsync());
    }

    [Fact]
    public async Task Duplicate_pix_submit_with_the_same_attempt_key_reuses_the_existing_payment()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (customer, address, product, _) = await SeedCheckoutContextAsync(db);
        var pixTerm = await db.PaymentTerms.SingleAsync(x => x.Code == "PIX");
        var session = new TestSession();
        session.SetString("orofoods-cart-product-ids", $"{{\"{product.Id}\":1}}");
        var gateway = new ConfigurableCardGateway
        {
            PixNext = new PaymentGatewayOrder("MP-ORD-DUP-PIX", "MP-PAY-DUP-PIX", null, product.BasePrice, PaymentStatus.Pending, "pix-copy-paste", "pix-qr", DateTime.UtcNow.AddMinutes(30), null, null)
        };
        var controller = CreateController(db, session, gateway);
        var input = new CheckoutViewModel
        {
            AddressId = address.Id,
            PaymentTermId = pixTerm.Id,
            RequestedDeliveryDate = DateTime.Today.AddDays(1),
            AttemptKey = "attempt-duplicate-pix"
        };

        var firstResult = await controller.Checkout(input);
        var secondResult = await controller.Checkout(input);

        var firstRedirect = Assert.IsType<RedirectToActionResult>(firstResult);
        var secondRedirect = Assert.IsType<RedirectToActionResult>(secondResult);
        Assert.Equal("Pix", firstRedirect.ActionName);
        Assert.Equal(firstRedirect.RouteValues!["id"], secondRedirect.RouteValues!["id"]);
        Assert.Equal(1, await db.Orders.CountAsync(x => x.CustomerId == customer.Id));
        Assert.Equal(1, await db.Payments.CountAsync());
        Assert.Equal(1, await db.InventoryReservations.CountAsync(x => x.Status == InventoryReservationStatus.Active));
        Assert.Equal(1, await db.ProductInventories.Where(x => x.ProductId == product.Id).Select(x => x.QuantityReserved).SingleAsync());
    }

    private static async Task<(Customer Customer, CustomerAddress Address, Product Product, PaymentTerm CreditCardTerm)> SeedCheckoutContextAsync(
        Orofoods.Web.Data.ApplicationDbContext db)
    {
        var customer = new Customer
        {
            LegalName = "Checkout Card Ltda",
            TradeName = "Checkout Card",
            Cnpj = Guid.NewGuid().ToString("N")[..14],
            Status = CustomerStatus.Approved,
            IsActive = true,
            MinimumOrder = 1m,
            Email = "buyer@testuser.com"
        };
        var address = new CustomerAddress
        {
            Label = "Principal",
            Street = "Rua Teste",
            Number = "100",
            District = "Centro",
            City = "Campinas",
            State = "SP",
            ZipCode = "13000-000",
            IsActive = true
        };
        customer.Addresses.Add(address);
        var category = new ProductCategory { Name = "Congelados", Slug = "congelados", IsActive = true };
        var product = new Product
        {
            Sku = "CARD-001",
            Name = "Produto Teste Cartao",
            ProductCategory = category,
            Brand = "BIMBO",
            Unit = "caixa",
            BasePrice = 150m,
            MinimumCases = 1,
            IsActive = true,
            IsAvailable = true
        };
        var creditCardTerm = new PaymentTerm { Code = "CREDIT_CARD", Name = "Cartão de crédito", DaysUntilDue = 0, IsActive = true };
        var pixTerm = new PaymentTerm { Code = "PIX", Name = "Pix", DaysUntilDue = 0, IsActive = true };
        var user = new ApplicationUser
        {
            Id = "checkout-card-user",
            UserName = "checkout-card@orofoods.local",
            NormalizedUserName = "CHECKOUT-CARD@OROFOODS.LOCAL",
            Email = "checkout-card@orofoods.local",
            NormalizedEmail = "CHECKOUT-CARD@OROFOODS.LOCAL",
            Customer = customer,
            IsActive = true
        };
        db.AddRange(customer, category, product, creditCardTerm, pixTerm, user);
        await db.SaveChangesAsync();
        db.ProductInventories.Add(new ProductInventory { ProductId = product.Id, QuantityOnHand = 50 });
        await db.SaveChangesAsync();
        return (customer, address, product, creditCardTerm);
    }

    private static PortalController CreateController(Orofoods.Web.Data.ApplicationDbContext db, ISession session, IPaymentGateway gateway)
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
                gateway,
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
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "checkout-card-user")], "test")),
                Session = session
            }
        };
        return controller;
    }

    private sealed class ConfigurableCardGateway : IPaymentGateway
    {
        public PaymentGatewayOrder Next { get; set; } = null!;
        public PaymentGatewayOrder PixNext { get; set; } = null!;

        public Task<PaymentGatewayOrder> CreatePixAsync(CreatePixPaymentRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(PixNext with { ExternalReference = request.ExternalReference });

        public Task<PaymentGatewayOrder> CreateCreditCardPaymentAsync(CreateCreditCardPaymentRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Next with { ExternalReference = request.ExternalReference });

        public Task<PaymentGatewayOrder> GetOrderAsync(string gatewayOrderId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not exercised by this card-only checkout harness.");

        public Task<PaymentGatewayOrder> RefundAsync(RefundPaymentRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not exercised by this card-only checkout harness.");
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
