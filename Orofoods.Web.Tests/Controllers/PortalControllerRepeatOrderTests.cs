using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orofoods.Web.Controllers;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Models.Pricing;
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

public class PortalControllerRepeatOrderTests
{
    private sealed class UnusedPaymentGateway : IPaymentGateway
    {
        public Task<PaymentGatewayOrder> CreatePixAsync(CreatePixPaymentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PaymentGatewayOrder> CreateCreditCardPaymentAsync(CreateCreditCardPaymentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PaymentGatewayOrder> GetOrderAsync(string gatewayOrderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PaymentGatewayOrder> RefundAsync(RefundPaymentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Confirm_forbids_using_an_address_owned_by_another_customer()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer
        {
            LegalName = "Cliente A Ltda",
            TradeName = "Cliente A",
            Cnpj = "12.345.678/0001-99",
            Status = CustomerStatus.Approved,
            IsActive = true,
            MinimumOrder = 1m
        };
        var otherCustomer = new Customer
        {
            LegalName = "Cliente B Ltda",
            TradeName = "Cliente B",
            Cnpj = "98.765.432/0001-10",
            Status = CustomerStatus.Approved,
            IsActive = true
        };
        otherCustomer.Addresses.Add(new CustomerAddress
        {
            Label = "Endereco de outro cliente",
            Street = "Rua B",
            Number = "2",
            District = "Centro",
            City = "Campinas",
            State = "SP",
            ZipCode = "13000-000",
            IsActive = true
        });
        var category = new ProductCategory { Name = "Paes", Slug = "paes", IsActive = true };
        var product = new Product
        {
            Sku = "BIM-001",
            Name = "Pao",
            ProductCategory = category,
            Brand = "BIMBO",
            Unit = "caixa",
            BasePrice = 10m,
            MinimumCases = 1,
            IsActive = true,
            IsAvailable = true
        };
        db.AddRange(customer, otherCustomer, product);
        await db.SaveChangesAsync();
        db.ProductInventories.Add(new ProductInventory { ProductId = product.Id, QuantityOnHand = 10 });
        db.Users.Add(new ApplicationUser
        {
            Id = "customer-user",
            UserName = "customer@orofoods.local",
            NormalizedUserName = "CUSTOMER@OROFOODS.LOCAL",
            Email = "customer@orofoods.local",
            NormalizedEmail = "CUSTOMER@OROFOODS.LOCAL",
            CustomerId = customer.Id,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, "customer-user");

        var result = await controller.Confirm(new ConfirmOrderVm
        {
            SourceOrderId = 1,
            AddressId = otherCustomer.Addresses.Single().Id,
            ProductIds = [product.Id],
            Quantities = [1]
        });

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(db.Orders);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("Deixar na doca lateral", "Deixar na doca lateral")]
    public async Task Confirm_persists_order_when_notes_are_missing_or_provided(string? notes, string expectedNotes)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer
        {
            LegalName = "Cliente A Ltda",
            TradeName = "Cliente A",
            Cnpj = "12.345.678/0001-99",
            Status = CustomerStatus.Approved,
            IsActive = true,
            MinimumOrder = 1m,
            CreditLimit = 1000m
        };
        var address = new CustomerAddress
        {
            Label = "Principal", Street = "Rua A", Number = "1", District = "Centro",
            City = "Campinas", State = "SP", ZipCode = "13000-000", IsActive = true,
            IsPrimary = true, Customer = customer
        };
        var user = new Orofoods.Web.Models.Identity.ApplicationUser
        {
            Id = "customer-user", UserName = "customer@orofoods.local", Email = "customer@orofoods.local",
            NormalizedUserName = "CUSTOMER@OROFOODS.LOCAL", NormalizedEmail = "CUSTOMER@OROFOODS.LOCAL",
            Customer = customer, IsActive = true
        };
        var product = new Orofoods.Web.Models.Catalog.Product
        {
            Sku = "BIM-001", Name = "Pao", ProductCategory = new() { Name = "Paes", Slug = "paes" },
            Brand = "BIMBO", Unit = "caixa", BasePrice = 10m, MinimumCases = 1,
            IsActive = true, IsAvailable = true
        };
        var paymentTerm = new PaymentTerm { Code = "PIX", Name = "PIX", DaysUntilDue = 0, IsActive = true };
        var sourceOrder = new Orofoods.Web.Models.Orders.Order
        {
            Customer = customer, CreatedByUser = user, Number = "ORO-ORIGINAL", Status = Orofoods.Web.Models.Orders.OrderStatus.Received
        };
        db.AddRange(customer, address, user, product, paymentTerm, sourceOrder, new Orofoods.Web.Models.Inventory.ProductInventory { Product = product, QuantityOnHand = 10 });
        await db.SaveChangesAsync();

        var controller = CreateController(db, user.Id);
        var result = await controller.Confirm(new ConfirmOrderVm
        {
            SourceOrderId = sourceOrder.Id,
            AddressId = address.Id,
            PaymentTermId = paymentTerm.Id,
            RequestedDate = DateTime.Today.AddDays(1),
            ProductIds = [product.Id],
            Quantities = [1],
            Notes = notes!
        });

        Assert.IsType<RedirectToActionResult>(result);
        var createdOrder = await db.Orders.SingleAsync(order => order.Id != sourceOrder.Id);
        Assert.Equal(expectedNotes, createdOrder.Notes);
        Assert.Equal(10m, createdOrder.Total);
    }

    private static PortalController CreateController(Orofoods.Web.Data.ApplicationDbContext db, string userId)
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
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test"))
            }
        };
        return controller;
    }
}
