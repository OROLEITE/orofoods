using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orofoods.Web.Controllers;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Services.Catalog;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Payments;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Controllers;

public class PortalControllerCatalogTests
{
    private sealed class UnusedPaymentGateway : IPaymentGateway
    {
        public Task<PaymentGatewayOrder> CreatePixAsync(CreatePixPaymentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PaymentGatewayOrder> CreateCreditCardPaymentAsync(CreateCreditCardPaymentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PaymentGatewayOrder> GetOrderAsync(string gatewayOrderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PaymentGatewayOrder> RefundAsync(RefundPaymentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Catalog_exposes_the_total_number_of_cases_in_the_cart()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer
        {
            LegalName = "Cliente Ltda",
            TradeName = "Cliente",
            Cnpj = "12.345.678/0001-99",
            Status = CustomerStatus.Approved,
            IsActive = true
        };
        var category = new ProductCategory { Name = "Congelados", Slug = "congelados", IsActive = true };
        var firstProduct = CreateProduct("BIM-001", "Pao Brioche", category);
        var secondProduct = CreateProduct("BIM-002", "Pao Australiano", category);
        var user = new ApplicationUser
        {
            Id = "customer-user",
            UserName = "customer@orofoods.local",
            NormalizedUserName = "CUSTOMER@OROFOODS.LOCAL",
            Email = "customer@orofoods.local",
            NormalizedEmail = "CUSTOMER@OROFOODS.LOCAL",
            Customer = customer,
            IsActive = true
        };
        db.AddRange(customer, category, firstProduct, secondProduct, user);
        await db.SaveChangesAsync();
        db.ProductInventories.AddRange(
            new ProductInventory { ProductId = firstProduct.Id, QuantityOnHand = 20 },
            new ProductInventory { ProductId = secondProduct.Id, QuantityOnHand = 20 });
        await db.SaveChangesAsync();

        var session = new TestSession();
        session.SetString("orofoods-cart-product-ids", $"{{\"{firstProduct.Id}\":2,\"{secondProduct.Id}\":3}}");
        var controller = CreateController(db, session);

        await controller.Catalog(null, null, null, null);

        Assert.Equal(5, controller.ViewData["CartQuantity"] as int?);
    }

    private static Product CreateProduct(string sku, string name, ProductCategory category) => new()
    {
        Sku = sku,
        Name = name,
        Brand = "Bimbo",
        Unit = "caixa",
        BasePrice = 10m,
        MinimumCases = 1,
        IsActive = true,
        IsAvailable = true,
        ProductCategory = category
    };

    private static PortalController CreateController(Orofoods.Web.Data.ApplicationDbContext db, ISession session)
    {
        var priceService = new PriceService(db);
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
            new PaymentEligibilityService(db, Options.Create(new PaymentEligibilityOptions())),
            new PaymentOrchestrationService(
                db,
                new PaymentEligibilityService(db, Options.Create(new PaymentEligibilityOptions())),
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
                    [new Claim(ClaimTypes.NameIdentifier, "customer-user")],
                    "test")),
                Session = session
            }
        };
        return controller;
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
