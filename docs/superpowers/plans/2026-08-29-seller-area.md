# Seller Area Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a dedicated seller area that limits each active seller to assigned customers and supports customer-scoped carts, repeat ordering, and direct `Received` order confirmation.

**Architecture:** Add a seller authorization policy and seller-scope query service, extend the existing cart with typed session scopes, and centralize order placement in a transactional service shared by customer and seller flows. Expose the feature through a separate `Vendedor` MVC area so seller navigation and authorization never mix with Admin or customer controllers.

**Tech Stack:** .NET 10, ASP.NET Core MVC and Identity, EF Core 10 with SQLite, Razor, Bootstrap, xUnit.

**Spec:** `docs/superpowers/specs/2026-08-29-seller-area-design.md`

## Global Constraints

- Sellers can access only active, approved customers linked to their active `SalesRepresentative`.
- Seller-created orders start as `Received` and store the seller user in `CreatedByUserId` and initial `OrderStatusHistory`.
- Prices, availability, minimum quantities, customer minimum order, addresses, payment terms, and credit are revalidated on the server.
- Existing customer cart session keys remain compatible.
- No database migration or destructive data operation is required.
- `Info` remains untracked and must not be added to commits.

---

### Task 1: Seller Authorization And Customer Scope

**Files:**
- Create: `Orofoods.Web/Authorization/LinkedSalesRepresentativeRequirement.cs`
- Create: `Orofoods.Web/Services/Identity/SalesRepresentativeAccessService.cs`
- Modify: `Orofoods.Web/Authorization/ApprovedCustomerRequirement.cs`
- Modify: `Orofoods.Web/Program.cs`
- Create: `Orofoods.Web.Tests/Services/SalesRepresentativeAccessServiceTests.cs`

**Interfaces:**
- Produces: `OrofoodsPolicies.LinkedSalesRepresentative`
- Produces: `Task<bool> HasActiveAccessAsync(string? userId, CancellationToken cancellationToken = default)`
- Produces: `Task<SalesRepresentative?> GetActiveRepresentativeAsync(string? userId, CancellationToken cancellationToken = default)`
- Produces: `Task<Customer?> GetScopedCustomerAsync(string? userId, int customerId, CancellationToken cancellationToken = default)`

- [ ] **Step 1: Write failing seller-scope tests**

Create fixtures with two representatives, one customer per representative, an inactive representative, and seller users linked to each representative. Exercise the real SQLite-backed service:

```csharp
[Fact]
public async Task Seller_can_resolve_only_an_assigned_active_approved_customer()
{
    await using var db = await TestDbContextFactory.CreateAsync();
    var seller = await AddSellerFixtureAsync(db);
    var sut = new SalesRepresentativeAccessService(db);

    Assert.NotNull(await sut.GetScopedCustomerAsync(seller.User.Id, seller.AssignedCustomer.Id));
    Assert.Null(await sut.GetScopedCustomerAsync(seller.User.Id, seller.OtherCustomer.Id));
}

[Fact]
public async Task Inactive_representative_has_no_seller_access()
{
    await using var db = await TestDbContextFactory.CreateAsync();
    var seller = await AddSellerFixtureAsync(db, representativeActive: false);
    var sut = new SalesRepresentativeAccessService(db);

    Assert.False(await sut.HasActiveAccessAsync(seller.User.Id));
}
```

Define the fixture in the same test file so every relationship is explicit:

```csharp
private sealed record SellerFixtureData(
    ApplicationUser User,
    SalesRepresentative Representative,
    Customer AssignedCustomer,
    Customer OtherCustomer);

private static async Task<SellerFixtureData> AddSellerFixtureAsync(
    ApplicationDbContext db,
    bool representativeActive = true)
{
    var representative = new SalesRepresentative { Name = "Vendedor A", Email = "a@orofoods.local", IsActive = representativeActive };
    var otherRepresentative = new SalesRepresentative { Name = "Vendedor B", Email = "b@orofoods.local", IsActive = true };
    var assigned = new Customer { LegalName = "Burger A Ltda", TradeName = "Burger A", Cnpj = "11.111.111/0001-11", Status = CustomerStatus.Approved, IsActive = true, SalesRepresentative = representative };
    var other = new Customer { LegalName = "Burger B Ltda", TradeName = "Burger B", Cnpj = "22.222.222/0001-22", Status = CustomerStatus.Approved, IsActive = true, SalesRepresentative = otherRepresentative };
    var user = new ApplicationUser { Id = "seller-a", UserName = "a@orofoods.local", Email = "a@orofoods.local", IsActive = true, SalesRepresentative = representative };
    db.AddRange(representative, otherRepresentative, assigned, other, user);
    await db.SaveChangesAsync();
    return new(user, representative, assigned, other);
}
```

Use `AddSellerFixtureAsync` in both tests instead of a global shared fixture.

- [ ] **Step 2: Run tests and verify RED**

Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~SalesRepresentativeAccessServiceTests`

Expected: compilation fails because `SalesRepresentativeAccessService` does not exist.

- [ ] **Step 3: Implement the scoped access service and policy**

Use joins that enforce active user, active representative, role-independent representative linkage, active customer, approved status, and matching representative ID:

```csharp
public class SalesRepresentativeAccessService(ApplicationDbContext db)
{
    public async Task<bool> HasActiveAccessAsync(string? userId, CancellationToken cancellationToken = default) =>
        await GetActiveRepresentativeAsync(userId, cancellationToken) is not null;

    public Task<SalesRepresentative?> GetActiveRepresentativeAsync(string? userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return Task.FromResult<SalesRepresentative?>(null);
        return db.Users
            .Where(x => x.Id == userId && x.IsActive && x.SalesRepresentativeId.HasValue)
            .Join(db.SalesRepresentatives.Where(x => x.IsActive),
                user => user.SalesRepresentativeId,
                representative => representative.Id,
                (_, representative) => representative)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<Customer?> GetScopedCustomerAsync(string? userId, int customerId, CancellationToken cancellationToken = default)
    {
        var representative = await GetActiveRepresentativeAsync(userId, cancellationToken);
        if (representative is null) return null;
        return await db.Customers
            .Include(x => x.Addresses)
            .Include(x => x.PriceTable)
            .Include(x => x.CustomerPaymentTerms).ThenInclude(x => x.PaymentTerm)
            .SingleOrDefaultAsync(x => x.Id == customerId && x.SalesRepresentativeId == representative.Id &&
                x.IsActive && x.Status == CustomerStatus.Approved, cancellationToken);
    }
}
```

Register the service and handler. Define the policy with both role and custom requirement:

```csharp
public const string LinkedSalesRepresentative = "LinkedSalesRepresentative";

options.AddPolicy(OrofoodsPolicies.LinkedSalesRepresentative, policy =>
    policy.RequireAuthenticatedUser()
        .RequireRole("Vendedor")
        .AddRequirements(new LinkedSalesRepresentativeRequirement()));
```

- [ ] **Step 4: Run focused and full tests**

Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~SalesRepresentativeAccessServiceTests`

Expected: PASS.

Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --no-restore`

Expected: all existing and new tests pass.

- [ ] **Step 5: Commit**

```powershell
git add Orofoods.Web/Authorization/LinkedSalesRepresentativeRequirement.cs Orofoods.Web/Authorization/ApprovedCustomerRequirement.cs Orofoods.Web/Services/Identity/SalesRepresentativeAccessService.cs Orofoods.Web/Program.cs Orofoods.Web.Tests/Services/SalesRepresentativeAccessServiceTests.cs
git commit -m "feat: add seller-scoped authorization"
```

---

### Task 2: Isolated Seller And Customer Carts

**Files:**
- Create: `Orofoods.Web/Services/Orders/CartScope.cs`
- Modify: `Orofoods.Web/Services/Orders/CartService.cs`
- Create: `Orofoods.Web.Tests/Infrastructure/TestSession.cs`
- Create: `Orofoods.Web.Tests/Services/CartServiceTests.cs`

**Interfaces:**
- Produces: `CartScope.CustomerPortal`
- Produces: `CartScope.ForSeller(string sellerUserId, int customerId)`
- Modifies: all `CartService` operations accept optional `CartScope? scope = null`

- [ ] **Step 1: Write failing cart-isolation tests**

Implement an in-memory `ISession` test double backed by `Dictionary<string, byte[]>`. Test that seller/customer pairs cannot see each other's quantities and that the legacy customer scope still uses `orofoods-cart-product-ids`:

```csharp
[Fact]
public async Task Seller_carts_are_isolated_by_seller_and_customer()
{
    await using var db = await TestDbContextFactory.CreateAsync();
    var data = await AddCartFixtureAsync(db);
    var session = new TestSession();
    var sut = new CartService(db, new PriceService(db));

    await sut.AddAsync(data.CustomerA.Id, data.Product.Id, 2, session, CartScope.ForSeller("seller-1", data.CustomerA.Id));

    Assert.Single((await sut.GetAsync(data.CustomerA.Id, session, CartScope.ForSeller("seller-1", data.CustomerA.Id))).Items);
    Assert.Empty((await sut.GetAsync(data.CustomerA.Id, session, CartScope.ForSeller("seller-2", data.CustomerA.Id))).Items);
    Assert.Empty((await sut.GetAsync(data.CustomerB.Id, session, CartScope.ForSeller("seller-1", data.CustomerB.Id))).Items);
}
```

Define `CartFixtureData` and `AddCartFixtureAsync` as private members in the test file. The helper creates two approved active customers, one active category, and this exact active product before saving:

```csharp
private sealed record CartFixtureData(Customer CustomerA, Customer CustomerB, Product Product);

private static async Task<CartFixtureData> AddCartFixtureAsync(ApplicationDbContext db)
{
    var category = new ProductCategory { Name = "Paes", Slug = "paes", IsActive = true };
    var customerA = new Customer { LegalName = "Cliente A Ltda", TradeName = "Cliente A", Cnpj = "33.333.333/0001-33", Status = CustomerStatus.Approved, IsActive = true };
    var customerB = new Customer { LegalName = "Cliente B Ltda", TradeName = "Cliente B", Cnpj = "44.444.444/0001-44", Status = CustomerStatus.Approved, IsActive = true };
    var product = new Product { Sku = "PAO-SELLER", Name = "Pao Seller", ProductCategory = category, Brand = "Orofoods", BasePrice = 80m, MinimumCases = 1, IsActive = true, IsAvailable = true };
    db.AddRange(category, customerA, customerB, product);
    await db.SaveChangesAsync();
    return new(customerA, customerB, product);
}
```

Use `AddCartFixtureAsync` in the test snippet instead of `CartFixture.AddAsync`.

- [ ] **Step 2: Run tests and verify RED**

Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~CartServiceTests`

Expected: compilation fails because `CartScope`, `TestSession`, and scoped overloads do not exist.

- [ ] **Step 3: Implement typed session scopes**

```csharp
public sealed record CartScope(string Value)
{
    public static CartScope CustomerPortal { get; } = new("");

    public static CartScope ForSeller(string sellerUserId, int customerId)
    {
        if (string.IsNullOrWhiteSpace(sellerUserId)) throw new ArgumentException("Seller user id is required.", nameof(sellerUserId));
        if (customerId <= 0) throw new ArgumentOutOfRangeException(nameof(customerId));
        return new($"seller:{sellerUserId}:customer:{customerId}");
    }
}
```

Change `Read`, `Write`, and `Clear` to resolve the key through:

```csharp
private const string BaseSessionKey = "orofoods-cart-product-ids";
private static string GetSessionKey(CartScope? scope) =>
    string.IsNullOrEmpty(scope?.Value) ? BaseSessionKey : $"{BaseSessionKey}:{scope.Value}";
```

Append `CartScope? scope = null` to `GetAsync`, `AddAsync`, `Update`, `Remove`, `Clear`, and `ReplaceAsync`, and pass it to every session read/write.

- [ ] **Step 4: Run focused and full tests**

Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~CartServiceTests`

Expected: PASS, including customer legacy-key compatibility and seller/customer isolation.

Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --no-restore`

Expected: all tests pass.

- [ ] **Step 5: Commit**

```powershell
git add Orofoods.Web/Services/Orders/CartScope.cs Orofoods.Web/Services/Orders/CartService.cs Orofoods.Web.Tests/Infrastructure/TestSession.cs Orofoods.Web.Tests/Services/CartServiceTests.cs
git commit -m "feat: isolate carts by seller and customer"
```

---

### Task 3: Shared Transactional Order Placement

**Files:**
- Create: `Orofoods.Web/Services/Orders/OrderPlacementService.cs`
- Create: `Orofoods.Web/ViewModels/OrderPlacementModels.cs`
- Modify: `Orofoods.Web/Controllers/PortalController.cs`
- Modify: `Orofoods.Web/Program.cs`
- Create: `Orofoods.Web.Tests/Services/OrderPlacementServiceTests.cs`

**Interfaces:**
- Produces: `OrderPlacementCommand(int AddressId, int PaymentTermId, DateTime RequestedDeliveryDate, string Notes)`
- Produces: `OrderPlacementResult(Order? Order, IReadOnlyList<string> Errors)` with `Succeeded`
- Produces: `Task<OrderPlacementResult> PlaceAsync(int customerId, string createdByUserId, OrderPlacementCommand command, ISession session, CartScope? scope = null, CancellationToken cancellationToken = default)`

- [ ] **Step 1: Write failing commercial-rule tests**

Use SQLite, real `PriceService`, real `CartService`, and `TestSession`. Write separate tests that catch each mutation:

```csharp
[Fact]
public async Task Places_received_order_with_current_price_snapshots_and_initial_history()
{
    await using var fixture = await PlacementFixture.CreateAsync();
    var result = await fixture.Service.PlaceAsync(
        fixture.Customer.Id,
        fixture.SellerUser.Id,
        new(fixture.Address.Id, fixture.PaymentTerm.Id, new DateTime(2026, 9, 1), "Sem cebola"),
        fixture.Session,
        fixture.Scope);

    Assert.True(result.Succeeded);
    Assert.Equal(OrderStatus.Received, result.Order!.Status);
    Assert.Equal(fixture.SellerUser.Id, result.Order.CreatedByUserId);
    Assert.Equal(fixture.CurrentPrice, Assert.Single(result.Order.Items).UnitPrice);
    Assert.Equal(fixture.Product.Name, result.Order.Items[0].ProductNameSnapshot);
    Assert.Equal(OrderStatus.Received, Assert.Single(result.Order.StatusHistory).Status);
}

[Fact]
public async Task Rejects_order_below_customer_minimum_without_partial_persistence()
{
    await using var fixture = await PlacementFixture.CreateAsync();
    fixture.Customer.MinimumOrder = 500m;
    await fixture.Db.SaveChangesAsync();

    var result = await fixture.PlaceAsync();

    Assert.False(result.Succeeded);
    Assert.Empty(fixture.Db.Orders);
    Assert.Empty(fixture.Db.OrderStatusHistories);
}
```

Add independent tests for unavailable product, wrong address, wrong payment term, exhausted credit, empty cart, quantity minimum, generated number, and cart clearing only after commit.

Define `PlacementFixture` as a private `IAsyncDisposable` test fixture with these exact members: `ApplicationDbContext Db`, `OrderPlacementService Service`, `Customer Customer`, `ApplicationUser SellerUser`, `CustomerAddress Address`, `PaymentTerm PaymentTerm`, `Product Product`, `decimal CurrentPrice`, `TestSession Session`, and `CartScope Scope`. `CreateAsync` must create and save an active approved customer with `CreditLimit = 10000m`, an active address, an active assigned payment term, an active available product and price-table item, and an active seller user; it then adds one product to the scoped cart using the real `CartService`. `PlaceAsync` calls the service with those persisted IDs and `DisposeAsync` disposes `Db`.

- [ ] **Step 2: Run tests and verify RED**

Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~OrderPlacementServiceTests`

Expected: compilation fails because placement contracts and service do not exist.

- [ ] **Step 3: Implement server-side revalidation and transaction**

The service must reload the active approved customer, active address, assigned active payment term, current cart, and available credit. Return literal Portuguese messages instead of throwing for commercial validation failures. Persist atomically:

```csharp
await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
db.Orders.Add(order);
await db.SaveChangesAsync(cancellationToken);
order.Number = $"ORO-{timeProvider.GetUtcNow():yyyy}-{order.Id:000000}";
order.ConfirmedAt = timeProvider.GetUtcNow().UtcDateTime;
await db.SaveChangesAsync(cancellationToken);
await transaction.CommitAsync(cancellationToken);
cartService.Clear(session, scope);
```

Build `OrderItem` from current `CartLineViewModel` values and add initial history before the first save:

```csharp
order.StatusHistory.Add(new OrderStatusHistory
{
    Status = OrderStatus.Received,
    ChangedAt = order.CreatedAt,
    ChangedByUserId = createdByUserId
});
```

- [ ] **Step 4: Refactor customer checkout to use the shared service**

Replace inline order creation in `PortalController.Checkout(CheckoutViewModel)` with `OrderPlacementService.PlaceAsync`. Preserve ModelState errors and redirect to `Success` only on success:

```csharp
var result = await orderPlacementService.PlaceAsync(
    customer.Id,
    userId,
    new(input.AddressId, input.PaymentTermId, input.RequestedDeliveryDate, input.Notes),
    HttpContext.Session);
if (!result.Succeeded)
{
    foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error);
    return View(await BuildCheckoutAsync(customer));
}
return RedirectToAction(nameof(Success), new { id = result.Order!.Id });
```

- [ ] **Step 5: Run focused and full tests**

Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~OrderPlacementServiceTests`

Expected: all placement scenarios PASS.

Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --no-restore`

Expected: all tests pass.

- [ ] **Step 6: Commit**

```powershell
git add Orofoods.Web/Services/Orders/OrderPlacementService.cs Orofoods.Web/ViewModels/OrderPlacementModels.cs Orofoods.Web/Controllers/PortalController.cs Orofoods.Web/Program.cs Orofoods.Web.Tests/Services/OrderPlacementServiceTests.cs
git commit -m "feat: centralize transactional order placement"
```

---

### Task 4: Seller Dashboard And Customer Workspace Queries

**Files:**
- Create: `Orofoods.Web/Services/Sellers/SellerWorkspaceService.cs`
- Create: `Orofoods.Web/ViewModels/SellerViewModels.cs`
- Create: `Orofoods.Web.Tests/Services/SellerWorkspaceServiceTests.cs`

**Interfaces:**
- Produces: `Task<SellerDashboardViewModel> SearchCustomersAsync(string userId, string? query, CancellationToken cancellationToken = default)`
- Produces: `Task<SellerCustomerWorkspaceViewModel?> GetCustomerWorkspaceAsync(string userId, int customerId, CancellationToken cancellationToken = default)`
- Produces: `Task<SellerCatalogViewModel?> GetCatalogAsync(string userId, int customerId, string? query, string? category, CancellationToken cancellationToken = default)`
- Consumes: `SalesRepresentativeAccessService`, `FrequentProductService`, `PriceService`

- [ ] **Step 1: Write failing scoped-query tests**

Test search by trade name/CNPJ/contact, exclusion of another seller's customer, exclusion of pending/inactive customers, empty order history, recent-order ordering, frequent products, and current catalog prices:

```csharp
[Fact]
public async Task Search_returns_only_assigned_active_approved_customers()
{
    await using var fixture = await SellerWorkspaceFixture.CreateAsync();
    var result = await fixture.Service.SearchCustomersAsync(fixture.SellerUser.Id, "burger");

    var customer = Assert.Single(result.Customers);
    Assert.Equal(fixture.AssignedCustomer.Id, customer.Id);
}

[Fact]
public async Task Workspace_returns_null_for_another_sellers_customer()
{
    await using var fixture = await SellerWorkspaceFixture.CreateAsync();
    Assert.Null(await fixture.Service.GetCustomerWorkspaceAsync(
        fixture.SellerUser.Id,
        fixture.OtherCustomer.Id));
}
```

Define `SellerWorkspaceFixture` as a private `IAsyncDisposable` fixture with `SellerWorkspaceService Service`, `ApplicationUser SellerUser`, `Customer AssignedCustomer`, `Customer OtherCustomer`, and the backing `ApplicationDbContext`. `CreateAsync` uses two active representatives, assigns one approved active customer to each, links the seller user only to the first representative, and constructs the service with real `SalesRepresentativeAccessService`, `FrequentProductService`, and `PriceService` instances.

- [ ] **Step 2: Run tests and verify RED**

Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~SellerWorkspaceServiceTests`

Expected: compilation fails because workspace service and view models do not exist.

- [ ] **Step 3: Implement view models and queries**

Define explicit projection models so views never receive unrestricted `IQueryable<Customer>`:

```csharp
public sealed record SellerCustomerRow(
    int Id, string TradeName, string LegalName, string Cnpj,
    string Contact, string City, DateTime? LastOrderAt, decimal? LastOrderTotal);

public sealed class SellerDashboardViewModel
{
    public required SalesRepresentative Representative { get; init; }
    public IReadOnlyList<SellerCustomerRow> Customers { get; init; } = [];
    public string? Query { get; init; }
}
```

Resolve the representative first, apply `SalesRepresentativeId`, `IsActive`, and `Approved` before search predicates, and project the last order through an ordered subquery. Customer workspace queries must repeat ownership constraints rather than accepting a previously loaded customer.

- [ ] **Step 4: Run focused and full tests**

Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~SellerWorkspaceServiceTests`

Expected: PASS.

Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --no-restore`

Expected: all tests pass.

- [ ] **Step 5: Commit**

```powershell
git add Orofoods.Web/Services/Sellers/SellerWorkspaceService.cs Orofoods.Web/ViewModels/SellerViewModels.cs Orofoods.Web.Tests/Services/SellerWorkspaceServiceTests.cs
git commit -m "feat: add seller customer workspace queries"
```

---

### Task 5: Seller MVC Area And Direct Checkout

**Files:**
- Create: `Orofoods.Web/Areas/Vendedor/Controllers/DashboardController.cs`
- Create: `Orofoods.Web/Areas/Vendedor/Controllers/CustomersController.cs`
- Create: `Orofoods.Web/Areas/Vendedor/Controllers/OrdersController.cs`
- Create: `Orofoods.Web/Areas/Vendedor/Views/_ViewImports.cshtml`
- Create: `Orofoods.Web/Areas/Vendedor/Views/_ViewStart.cshtml`
- Create: `Orofoods.Web/Areas/Vendedor/Views/Dashboard/Index.cshtml`
- Create: `Orofoods.Web/Areas/Vendedor/Views/Customers/Details.cshtml`
- Create: `Orofoods.Web/Areas/Vendedor/Views/Orders/Catalog.cshtml`
- Create: `Orofoods.Web/Areas/Vendedor/Views/Orders/Cart.cshtml`
- Create: `Orofoods.Web/Areas/Vendedor/Views/Orders/Checkout.cshtml`
- Create: `Orofoods.Web/Areas/Vendedor/Views/Orders/Details.cshtml`
- Create: `Orofoods.Web/Areas/Vendedor/Views/Orders/Success.cshtml`
- Modify: `Orofoods.Web/Program.cs`
- Create: `Orofoods.Web.Tests/Views/SellerViewConfigurationTests.cs`
- Create: `Orofoods.Web.Tests/Controllers/SellerControllerTests.cs`

**Interfaces:**
- Consumes: `OrofoodsPolicies.LinkedSalesRepresentative`
- Consumes: seller workspace, cart scope, and order placement contracts from Tasks 1-4

- [ ] **Step 1: Write failing view-configuration and controller-scope tests**

Verify seller `_ViewStart` uses the shared layout, `_ViewImports` enables tag helpers, all controllers carry the seller policy, and an unauthorized customer ID returns `NotFoundResult`:

```csharp
[Fact]
public void Seller_controllers_require_linked_sales_representative_policy()
{
    var controllers = new[] { typeof(DashboardController), typeof(CustomersController), typeof(OrdersController) };
    Assert.All(controllers, controller =>
        Assert.Contains(controller.GetCustomAttributes<AuthorizeAttribute>(),
            attribute => attribute.Policy == OrofoodsPolicies.LinkedSalesRepresentative));
}
```

- [ ] **Step 2: Run tests and verify RED**

Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~SellerViewConfigurationTests|FullyQualifiedName~SellerControllerTests"`

Expected: compilation fails because the seller area does not exist.

- [ ] **Step 3: Implement controllers with ownership checks**

Apply the area and policy at controller level:

```csharp
[Area("Vendedor")]
[Authorize(Policy = OrofoodsPolicies.LinkedSalesRepresentative)]
public class OrdersController(
    ApplicationDbContext db,
    SalesRepresentativeAccessService accessService,
    SellerWorkspaceService workspaceService,
    CartService cartService,
    OrderPlacementService orderPlacementService) : Controller
```

Every action obtains `User.FindFirstValue(ClaimTypes.NameIdentifier)`. Before catalog, cart, checkout, repeat, or details, call `GetScopedCustomerAsync`; return `NotFound()` when it returns null. Build scope only after validation:

```csharp
var scope = CartScope.ForSeller(userId, customer.Id);
```

POST actions use `[ValidateAntiForgeryToken]`. Checkout calls `OrderPlacementService.PlaceAsync` and shows returned commercial errors without clearing the cart. Repeat loads only product IDs and quantities from an order that belongs to the scoped customer, then calls `CartService.ReplaceAsync` using the seller scope.

- [ ] **Step 4: Implement Razor views**

Set area imports and layout:

```cshtml
@using Orofoods.Web
@using Orofoods.Web.Models
@using Orofoods.Web.ViewModels
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```

```cshtml
@{
    Layout = "/Views/Shared/_Layout.cshtml";
}
```

The dashboard includes search and assigned-customer cards. Customer details include commercial summary, active addresses/payment terms, recent orders, frequent products, and CTAs for catalog and repeat. Catalog and cart forms include `customerId` hidden fields. Checkout uses only server-provided address/payment options. Success links back to the same customer and seller order details.

- [ ] **Step 5: Run focused and full tests**

Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~SellerViewConfigurationTests|FullyQualifiedName~SellerControllerTests"`

Expected: PASS.

Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --no-restore`

Expected: all tests pass.

- [ ] **Step 6: Commit**

```powershell
git add Orofoods.Web/Areas/Vendedor Orofoods.Web/Program.cs Orofoods.Web.Tests/Views/SellerViewConfigurationTests.cs Orofoods.Web.Tests/Controllers/SellerControllerTests.cs
git commit -m "feat: add seller ordering area"
```

---

### Task 6: Navigation, Responsive Styling, And End-To-End Verification

**Files:**
- Modify: `Orofoods.Web/Views/Shared/_Layout.cshtml`
- Modify: `Orofoods.Web/Views/Shared/_LoginPartial.cshtml`
- Modify: `Orofoods.Web/wwwroot/css/site.css`
- Modify: `README.md`

**Interfaces:**
- Consumes: seller area routes from Task 5
- Produces: role-aware seller navigation and documented development credentials

- [ ] **Step 1: Verify the missing seller navigation before implementation**

Start the current Release app with a disposable database, log in as `vendedor@orofoods.local`, and inspect the rendered header at desktop and 390px widths.

Expected RED evidence: the rendered header has no link to `/Vendedor` and still presents the generic customer action.

- [ ] **Step 2: Add role-aware navigation**

Render `_LoginPartial` from the shared layout instead of hardcoded login/customer actions. Inject `SignInManager<ApplicationUser>` and use role checks for authenticated users:

```cshtml
@if (User.IsInRole("Vendedor"))
{
    <a class="btn btn-gold" asp-area="Vendedor" asp-controller="Dashboard" asp-action="Index">Area do vendedor</a>
}
```

Preserve customer and administrator links under their own roles. Do not show user/security administration in seller navigation.

- [ ] **Step 3: Add seller-specific responsive styles**

Add CSS variables and classes consistent with the existing gold/charcoal identity. Include responsive rules that convert `.seller-customer-grid`, `.seller-workspace-grid`, `.seller-order-layout`, and seller tables to one-column layouts below `768px`. Keep focus states visible and form labels explicit.

- [ ] **Step 4: Document and run automated verification**

Update `README.md` with `/Vendedor`, `vendedor@orofoods.local`, the development-only password already defined by seed, and the linked-customer restriction.

Run: `dotnet build Orofoods.slnx -c Release --no-restore --nologo`

Expected: 0 errors and 0 warnings.

Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --no-build --no-restore --nologo --verbosity minimal`

Expected: all tests pass, 0 failures.

- [ ] **Step 5: Run browser smoke test**

Start the Release app against a disposable migrated SQLite database. Log in as `vendedor@orofoods.local`, open `/Vendedor`, and verify:

- only Burger da Vila appears;
- another seller/customer ID returns 404;
- customer details render history and frequent products;
- seller/customer cart remains isolated after switching customer IDs;
- checkout creates a `Received` order with seller creator and initial history;
- desktop and 390px mobile layouts have no horizontal overflow;
- Admin user/security links are absent.

- [ ] **Step 6: Inspect Git and commit**

Run: `git diff --check`

Expected: no whitespace errors.

Run: `git status --short`

Expected: only Task 6 files plus untracked `Info` before staging.

```powershell
git add Orofoods.Web/Views/Shared/_Layout.cshtml Orofoods.Web/Views/Shared/_LoginPartial.cshtml Orofoods.Web/wwwroot/css/site.css README.md
git commit -m "feat: finish seller area experience"
```

---

## Final Verification

- [ ] Run `dotnet build Orofoods.slnx -c Release --no-restore --nologo` and confirm 0 errors and 0 warnings.
- [ ] Run `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --no-build --no-restore --nologo --verbosity minimal` and confirm 0 failures.
- [ ] Run `dotnet ef migrations list --project Orofoods.Web --startup-project Orofoods.Web --configuration Release --no-build` and confirm no seller-area migration was added.
- [ ] Run `git diff --check` and confirm no whitespace errors.
- [ ] Run `git status --short --branch` and confirm only untracked `Info` remains.
- [ ] Review all seller POST actions for `[ValidateAntiForgeryToken]` and all customer/order lookups for seller ownership checks.
