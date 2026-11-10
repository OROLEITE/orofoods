using System.Security.Claims;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
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
        var priceTable = new PriceTable { Name = "Tabela autorizada" };
        customer.PriceTable = priceTable;
        var term = new PaymentTerm { Code = "PIX", Name = "PIX", IsActive = true };
        var product = new Product
        {
            Sku = "SKU-ENTRY", WmcCode = "WMC-ENTRY", Name = "Produto para pedido", Brand = "Marca",
            Unit = "CX", BasePrice = 27.50m, IsActive = true, IsAvailable = true,
            ProductCategory = new ProductCategory { Name = "Categoria", Slug = "categoria" }
        };
        var user = new ApplicationUser { Id = "seller-user", UserName = "seller-user", Email = "seller@test.local", SalesRepresentative = seller, IsActive = true };
        db.AddRange(seller, customer, term, product, priceTable, user);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var suggestion = Assert.Single((await SearchOrderCustomersAsync(controller, "owned")).Items);
        var result = await controller.NewOrder(suggestion.Id, null);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AssistedOrderViewModel>(view.Model);
        Assert.Equal(customer.TradeName, model.CustomerName);
        Assert.Equal("Seller", model.SalesRepresentativeName);
        Assert.Equal("Tabela autorizada", model.PriceTableName);
        Assert.Null(typeof(AssistedOrderViewModel).GetProperty("AddressId"));
        Assert.Equal(product.BasePrice, Assert.Single(model.Products).UnitPrice);
        Assert.Contains(model.PaymentTerms, term => term.Code == "PIX");
    }

    [Fact]
    public async Task NewOrder_without_a_customer_opens_the_customer_selection_entry_state()
    {
        await using var db = await TestDbContextFactory.CreateAsync();

        var result = await CreateController(db).NewOrder(0, null);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AssistedOrderViewModel>(view.Model);
        Assert.Equal(0, model.CustomerId);
        Assert.Empty(model.Products);
        Assert.Empty(model.PaymentTerms);
    }

    [Fact]
    public async Task Customer_search_matches_accent_insensitive_trade_legal_name_cnpj_and_wmc_code()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seller = new SalesRepresentative { Name = "Seller", IsActive = true };
        var customer = CreateCustomer("Padaria Stág", seller);
        customer.LegalName = "Indústria Estágio Ltda";
        customer.Cnpj = "12.345.678/0001-99";
        customer.WmcCode = "WMC-AB-987";
        db.AddRange(seller, customer, new ApplicationUser
        {
            Id = "seller-user", UserName = "seller-user", Email = "seller@test.local",
            SalesRepresentative = seller, IsActive = true
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        Assert.Equal(customer.Id, Assert.Single((await SearchOrderCustomersAsync(controller, "STAG")).Items).Id);
        Assert.Equal(customer.Id, Assert.Single((await SearchOrderCustomersAsync(controller, "estagio")).Items).Id);
        Assert.Equal(customer.Id, Assert.Single((await SearchOrderCustomersAsync(controller, "12345678")).Items).Id);
        Assert.Equal(customer.Id, Assert.Single((await SearchOrderCustomersAsync(controller, "ab-987")).Items).Id);
    }

    [Fact]
    public async Task Customer_search_is_scoped_and_returns_a_bounded_server_page()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seller = new SalesRepresentative { Name = "Seller", IsActive = true };
        var outsideSeller = new SalesRepresentative { Name = "Other", IsActive = true };
        var ownedCustomers = Enumerable.Range(1, 12).Select(index => CreateCustomer($"Stág Cliente {index:00}", seller)).ToList();
        var outside = CreateCustomer("Stág Cliente externo", outsideSeller);
        var pending = CreateCustomer("Stág Cliente pendente", seller);
        pending.Status = CustomerStatus.Pending;
        var inactive = CreateCustomer("Stág Cliente inativo", seller);
        inactive.IsActive = false;
        db.AddRange(seller, outsideSeller, outside, pending, inactive);
        db.AddRange(ownedCustomers);
        db.Users.Add(new ApplicationUser
        {
            Id = "seller-user", UserName = "seller-user", Email = "seller@test.local",
            SalesRepresentative = seller, IsActive = true
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var firstPage = await SearchOrderCustomersAsync(controller, "stag", 1);
        var secondPage = await SearchOrderCustomersAsync(controller, "stag", 2);
        var outOfScope = await SearchOrderCustomersAsync(controller, "externo");

        Assert.Equal(10, firstPage.Items.Count);
        Assert.True(firstPage.HasMore);
        Assert.Equal(2, secondPage.Items.Count);
        Assert.False(secondPage.HasMore);
        Assert.Empty(outOfScope.Items);
        Assert.DoesNotContain(firstPage.Items.Concat(secondPage.Items), result => result.Id == outside.Id);
    }

    [Fact]
    public void Customer_search_uses_the_existing_admin_seller_and_commercial_manager_authorization()
    {
        var controllerAuthorization = typeof(CustomersController).GetCustomAttribute<AuthorizeAttribute>();
        var searchAction = typeof(CustomersController).GetMethod("SearchOrderCustomers");

        Assert.Equal("Administrador,Vendedor,GerenteComercial", controllerAuthorization?.Roles);
        Assert.NotNull(searchAction?.GetCustomAttribute<HttpGetAttribute>());
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
            PaymentTermId = 0,
            AttemptKey = "attempt-1"
        }, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AssistedOrderViewModel>(view.Model);
        Assert.Contains("condição de pagamento", string.Join(" ", controller.ModelState.Values.SelectMany(value => value.Errors).Select(error => error.ErrorMessage)), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(model.PaymentTerms, paymentTerm => paymentTerm.Code == "CASH");
    }

    [Theory]
    [InlineData("Administrador")]
    [InlineData("GerenteComercial")]
    public async Task Admin_and_commercial_manager_assisted_order_creates_order_without_a_delivery_address(string role)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seller = new SalesRepresentative { Name = "Seller", IsActive = true };
        var customer = CreateCustomer("Owned", seller);
        var term = new PaymentTerm { Code = "CASH", Name = "À vista", DaysUntilDue = 0, IsActive = true };
        var product = new Product
        {
            Sku = "SKU-ADMIN-ADDRESSLESS", Name = "Produto", Brand = "Marca", Unit = "PC", BasePrice = 200m,
            IsActive = true, IsAvailable = true, ProductCategory = new ProductCategory { Name = "Categoria", Slug = "categoria" }
        };
        db.AddRange(seller, customer, term, product, new ProductInventory { Product = product, QuantityOnHand = 10 },
            new ApplicationUser { Id = "admin-user", UserName = "admin-user", Email = "admin@test.local", IsActive = true },
            new Orofoods.Web.Models.Commercial.OrderMinimumSettings { MinimumAmount = 1m });
        await db.SaveChangesAsync();

        var controller = CreateController(db, role);
        controller.HttpContext.Session = new TestSession();
        var result = await controller.NewOrder(new AssistedOrderViewModel
        {
            CustomerId = customer.Id,
            PaymentTermId = term.Id,
            AttemptKey = $"{role}-checkout-without-address",
            ProductIds = [product.Id],
            Quantities = [1]
        }, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var order = await db.Orders.SingleAsync();
        Assert.Null(order.DeliveryAddressId);
        Assert.Empty(controller.ModelState.Values.SelectMany(value => value.Errors));
    }

    private static CustomersController CreateController(Orofoods.Web.Data.ApplicationDbContext db, string role = "Vendedor")
    {
        var userId = role == "Vendedor" ? "seller-user" : "admin-user";
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
                    new Claim(ClaimTypes.NameIdentifier, userId),
                    new Claim(ClaimTypes.Role, role)
                ], "test"))
            }
        };
        return controller;
    }

    private static async Task<SearchPage> SearchOrderCustomersAsync(
        CustomersController controller,
        string query,
        int page = 1)
    {
        var action = typeof(CustomersController).GetMethod("SearchOrderCustomers");
        Assert.NotNull(action);
        var task = Assert.IsAssignableFrom<Task<IActionResult>>(
            action!.Invoke(controller, [query, page, CancellationToken.None]));
        var result = Assert.IsType<JsonResult>(await task);
        return JsonSerializer.Deserialize<SearchPage>(
            JsonSerializer.Serialize(result.Value),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private sealed record SearchPage(IReadOnlyList<SearchItem> Items, bool HasMore, int Page);
    private sealed record SearchItem(int Id, string TradeName, string LegalName, string Cnpj, string? WmcCode);

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
