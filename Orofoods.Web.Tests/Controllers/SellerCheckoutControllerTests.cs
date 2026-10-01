using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orofoods.Web.Areas.Vendedor.Controllers;
using Orofoods.Web.Authorization;
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
using Orofoods.Web.Services.Sellers;
using Orofoods.Web.Tests.Infrastructure;
using Orofoods.Web.ViewModels;
using System.Reflection;

namespace Orofoods.Web.Tests.Controllers;

public class SellerCheckoutControllerTests
{
    [Fact]
    public async Task Card_on_delivery_assisted_checkout_records_pending_payment_without_gateway_call()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var representative = new SalesRepresentative { Id = 7, Name = "Vendedor", IsActive = true };
        var seller = new ApplicationUser { Id = "seller-cod", UserName = "seller-cod", IsActive = true, SalesRepresentative = representative };
        var customer = new Customer { LegalName = "Cliente teste", TradeName = "Cliente teste", Cnpj = "11.111.111/0001-11", Status = CustomerStatus.Approved, IsActive = true, MinimumOrder = 1m, SalesRepresentative = representative };
        var address = new CustomerAddress { Label = "Principal", Street = "Rua A", Number = "1", District = "Centro", City = "Campinas", State = "SP", ZipCode = "13000-000", IsActive = true };
        customer.Addresses.Add(address);
        var term = new PaymentTerm { Code = "CARD_ON_DELIVERY", Name = "Cartão na entrega", DaysUntilDue = 0, IsActive = true };
        var product = new Product { Sku = "SELLER-COD", Name = "Produto", Brand = "Orofoods", Unit = "caixa", BasePrice = 25m, MinimumCases = 1, IsActive = true, IsAvailable = true, ProductCategory = new ProductCategory { Name = "Categoria", Slug = "categoria" } };
        db.AddRange(representative, seller, customer, term, product, new ProductInventory { Product = product, QuantityOnHand = 10 });
        await db.SaveChangesAsync();

        var session = new TestSession();
        session.SetString($"orofoods-cart-product-ids:seller:{representative.Id}:customer:{customer.Id}", $"{{\"{product.Id}\":1}}");
        var priceService = new PriceService(db);
        var cartService = new CartService(db, priceService);
        var accessService = new SalesRepresentativeAccessService(db);
        var eligibility = new PaymentEligibilityService(db, Options.Create(new PaymentEligibilityOptions { CardOnDeliveryEnabled = true }));
        var gateway = new NeverCalledPaymentGateway();
        var controller = new CheckoutController(
            db,
            new SellerCheckoutService(db, accessService, cartService, eligibility),
            accessService,
            new OrderPlacementService(db, cartService, priceService, eligibility, new OrderReservationService(db)),
            new OrderReservationService(db),
            cartService,
            new PaymentOrchestrationService(db, eligibility, gateway, new NoOpPaymentApprovalHandler(), Options.Create(new MercadoPagoOptions()), TimeProvider.System, NullLogger<PaymentOrchestrationService>.Instance));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, seller.Id),
                    new Claim(ClaimTypes.Role, "Vendedor")], "test")),
                Session = session
            }
        };
        controller.Request.Form = new FormCollection([]);

        var result = await controller.Index(customer.Id, new SellerCheckoutViewModel
        {
            CustomerId = customer.Id,
            AddressId = address.Id,
            PaymentTermId = term.Id,
            RequestedDeliveryDate = DateTime.Today.AddDays(1),
            AttemptKey = "seller-cod-attempt"
        }, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Success", redirect.ActionName);
        Assert.Equal(0, gateway.CallCount);
        var order = await db.Orders.SingleAsync(x => x.CustomerId == customer.Id);
        var payment = await db.Payments.SingleAsync(x => x.OrderId == order.Id);
        Assert.Equal(PaymentMethodType.CardOnDelivery, payment.Method);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Null(payment.GatewayOrderId);
    }

    [Fact]
    public void Checkout_controller_requires_linked_seller_policy()
    {
        Assert.Contains(typeof(CheckoutController).GetCustomAttributes<AuthorizeAttribute>(),
            attribute => attribute.Policy == OrofoodsPolicies.LinkedSalesRepresentative);
    }

    [Fact]
    public void Checkout_post_uses_antiforgery()
    {
        var methods = typeof(CheckoutController).GetMethods()
            .Where(method => method.Name == "Index" && method.GetCustomAttributes<HttpPostAttribute>().Any())
            .ToArray();

        Assert.Single(methods);
        Assert.NotEmpty(methods[0].GetCustomAttributes<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public void Checkout_date_uses_pt_br_text_binding_instead_of_browser_date_rendering()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var view = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Areas", "Vendedor", "Views", "Checkout", "Index.cshtml"));

        Assert.Contains("dd/MM/yyyy", view);
        Assert.DoesNotContain("type=\"date\"", view, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class NeverCalledPaymentGateway : IPaymentGateway
    {
        public int CallCount { get; private set; }
        public Task<PaymentGatewayOrder> CreatePixAsync(CreatePixPaymentRequest request, CancellationToken cancellationToken = default) { CallCount++; throw new InvalidOperationException("Unexpected online payment call."); }
        public Task<PaymentGatewayOrder> CreateCreditCardPaymentAsync(CreateCreditCardPaymentRequest request, CancellationToken cancellationToken = default) { CallCount++; throw new InvalidOperationException("Unexpected online payment call."); }
        public Task<PaymentGatewayOrder> GetOrderAsync(string gatewayOrderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PaymentGatewayOrder> RefundAsync(RefundPaymentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> values = [];
        public IEnumerable<string> Keys => values.Keys;
        public string Id => "seller-checkout-test";
        public bool IsAvailable => true;
        public void Clear() => values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
    }
}
