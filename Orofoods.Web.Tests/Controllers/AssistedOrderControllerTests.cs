using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Tests.Infrastructure;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Tests.Controllers;

public class AssistedOrderControllerTests
{
    [Fact]
    public async Task NewOrder_ForbidsCustomerOutsideSellerScope()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seller = new SalesRepresentative { Name = "Seller", IsActive = true };
        var owned = CreateCustomer("Owned", seller);
        var outside = CreateCustomer("Outside", null);
        var term = new PaymentTerm { Code = "PIX", Name = "PIX", IsActive = true };
        var user = new ApplicationUser { Id = "seller-user", UserName = "seller-user", Email = "seller@test.local", SalesRepresentative = seller, IsActive = true };
        db.AddRange(seller, owned, outside, term, user);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.NewOrder(outside.Id, null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task NewOrder_AllowsCustomerInSellerScope()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seller = new SalesRepresentative { Name = "Seller", IsActive = true };
        var customer = CreateCustomer("Owned", seller);
        var term = new PaymentTerm { Code = "PIX", Name = "PIX", IsActive = true };
        var user = new ApplicationUser { Id = "seller-user", UserName = "seller-user", Email = "seller@test.local", SalesRepresentative = seller, IsActive = true };
        db.AddRange(seller, customer, term, user);
        await db.SaveChangesAsync();

        var result = await CreateController(db).NewOrder(customer.Id, null);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AssistedOrderViewModel>(view.Model);
        Assert.Equal(customer.TradeName, model.CustomerName);
        Assert.Contains(model.PaymentTerms, term => term.Code == "PIX");
    }

    [Fact]
    public async Task NewOrder_InvalidSubmission_RendersTheReasonAndEligiblePaymentTerms()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seller = new SalesRepresentative { Name = "Seller", IsActive = true };
        var customer = CreateCustomer("Owned", seller);
        var term = new PaymentTerm { Code = "CASH", Name = "À vista", DaysUntilDue = 0, IsActive = true };
        var product = new Product
        {
            Sku = "SKU-1", Name = "Produto", Brand = "Marca", Unit = "PC", BasePrice = 20m,
            IsActive = true, IsAvailable = true, ProductCategory = new ProductCategory { Name = "Categoria", Slug = "categoria" }
        };
        db.AddRange(seller, customer, term, product, new ProductInventory { Product = product, QuantityOnHand = 10 }, new ApplicationUser { Id = "seller-user", UserName = "seller-user", Email = "seller@test.local", SalesRepresentative = seller, IsActive = true });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        controller.HttpContext.Session = new TestSession();
        var result = await controller.NewOrder(new AssistedOrderViewModel
        {
            CustomerId = customer.Id,
            AddressId = customer.Addresses.Single().Id,
            PaymentTermId = 0,
            AttemptKey = "attempt-1"
        }, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AssistedOrderViewModel>(view.Model);
        Assert.Contains("condição de pagamento", string.Join(" ", controller.ModelState.Values.SelectMany(value => value.Errors).Select(error => error.ErrorMessage)), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(model.PaymentTerms, paymentTerm => paymentTerm.Code == "CASH");
    }

    private static CustomersController CreateController(Orofoods.Web.Data.ApplicationDbContext db)
    {
        var priceService = new PriceService(db);
        var controller = new CustomersController(
            db,
            new CustomerApprovalService(db),
            new SalesRepresentativeAccessService(db),
            new PaymentEligibilityService(db, Options.Create(new PaymentEligibilityOptions())),
            priceService,
            new AssistedOrderService(db, priceService, new PaymentEligibilityService(db, Options.Create(new PaymentEligibilityOptions())), new OrderReservationService(db)),
            new CommercialAttentionService(db, new SalesRepresentativeAccessService(db), Options.Create(new CrmOptions()), Options.Create(new PaymentEligibilityOptions())));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, "seller-user"),
                    new Claim(ClaimTypes.Role, "Vendedor")
                ], "test"))
            }
        };
        return controller;
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> values = new(StringComparer.Ordinal);

        public bool IsAvailable => true;
        public string Id => "test-session";
        public IEnumerable<string> Keys => values.Keys;
        public void Clear() => values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
    }

    private static Customer CreateCustomer(string name, SalesRepresentative? seller) => new()
    {
        LegalName = $"{name} Ltda",
        TradeName = name,
        Cnpj = Guid.NewGuid().ToString()[..18],
        Status = CustomerStatus.Approved,
        IsActive = true,
        SalesRepresentative = seller,
        Addresses = [new CustomerAddress { Label = "Principal", Street = "Rua A", Number = "1", District = "Centro", City = "Campinas", State = "SP", ZipCode = "13000-000", IsActive = true }]
    };
}
