# Administrator Full Access Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let administrators enter every management module and use the customer portal only after explicitly choosing the customer context, while translating login validation messages to Portuguese.

**Architecture:** Keep the existing `Administrador` role as the authorization boundary. Add a session-backed selected-customer context for administrators; approved client users retain their own linked customer context. The portal authorization policy recognizes administrators, while the portal controller directs an administrator without a selected customer to a selection screen.

**Tech Stack:** ASP.NET Core MVC 10, Razor Pages Identity, ASP.NET Core Identity roles, Entity Framework Core, xUnit.

**Spec:** Conversation-approved bounded design on 2026-08-30.

## Global Constraints

- Only the `Administrador` role can access the administrative area.
- Administrators must explicitly select an approved active customer before acting in customer portal routes.
- Do not change customer price, credit, order, or cart data without a selected customer context.
- Login validation and authentication error messages must be in Portuguese.

---

### Task 1: Administrator Customer Context

**Files:**
- Create: `Orofoods.Web/Services/Identity/AdminCustomerContextService.cs`
- Modify: `Orofoods.Web/Authorization/ApprovedCustomerRequirement.cs`
- Modify: `Orofoods.Web/Program.cs`
- Test: `Orofoods.Web.Tests/Services/AdminCustomerContextServiceTests.cs`

**Interfaces:**
- Produces: `AdminCustomerContextService.GetSelectedCustomerId(ISession)`, `SetSelectedCustomerId(ISession, int)`, and `Clear(ISession)`.
- Consumes: ASP.NET Core `ISession` and existing customer records.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void Selected_customer_id_is_available_only_after_selection()
{
    var session = new TestSession();
    var sut = new AdminCustomerContextService();

    Assert.Null(sut.GetSelectedCustomerId(session));
    sut.SetSelectedCustomerId(session, 42);
    Assert.Equal(42, sut.GetSelectedCustomerId(session));
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj --filter FullyQualifiedName~AdminCustomerContextServiceTests --no-restore`

Expected: FAIL because `AdminCustomerContextService` does not exist.

- [ ] **Step 3: Write the minimal implementation**

```csharp
public sealed class AdminCustomerContextService
{
    private const string CustomerIdKey = "AdminCustomerId";

    public int? GetSelectedCustomerId(ISession session) => session.GetInt32(CustomerIdKey);
    public void SetSelectedCustomerId(ISession session, int customerId) => session.SetInt32(CustomerIdKey, customerId);
    public void Clear(ISession session) => session.Remove(CustomerIdKey);
}
```

- [ ] **Step 4: Run the focused test to verify it passes**

Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj --filter FullyQualifiedName~AdminCustomerContextServiceTests --no-restore`

Expected: PASS.

### Task 2: Customer Selection and Portal Resolution

**Files:**
- Modify: `Orofoods.Web/Controllers/PortalController.cs`
- Create: `Orofoods.Web/Views/Portal/SelectCustomer.cshtml`
- Test: `Orofoods.Web.Tests/Services/CustomerAccessServiceTests.cs`

**Interfaces:**
- Consumes: `AdminCustomerContextService`, `CustomerAccessService`, current role claims, and `ApplicationDbContext`.
- Produces: `PortalController.SelectCustomer` GET/POST routes and selected customer resolution for administrator portal actions.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task Returns_an_approved_active_customer_for_a_selected_context()
{
    await using var db = await TestDbContextFactory.CreateAsync();
    var customer = await AddApprovedCustomerAsync(db);
    var sut = new CustomerAccessService(db);

    var result = await sut.GetApprovedCustomerByIdAsync(customer.Id);

    Assert.Equal(customer.Id, result!.Id);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj --filter FullyQualifiedName~CustomerAccessServiceTests --no-restore`

Expected: FAIL because `GetApprovedCustomerByIdAsync` does not exist.

- [ ] **Step 3: Write the minimal implementation**

```csharp
public Task<Customer?> GetApprovedCustomerByIdAsync(int customerId, CancellationToken cancellationToken = default) =>
    db.Customers.Include(customer => customer.Addresses)
        .Include(customer => customer.PriceTable)
        .Include(customer => customer.CustomerPaymentTerms).ThenInclude(link => link.PaymentTerm)
        .SingleOrDefaultAsync(customer => customer.Id == customerId && customer.IsActive && customer.Status == CustomerStatus.Approved, cancellationToken);
```

- [ ] **Step 4: Add selection actions and portal redirection**

```csharp
[Authorize(Roles = "Administrador")]
public async Task<IActionResult> SelectCustomer() => View(await db.Customers.Where(x => x.IsActive && x.Status == CustomerStatus.Approved).OrderBy(x => x.TradeName).ToListAsync());
```

- [ ] **Step 5: Run focused tests to verify they pass**

Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj --filter FullyQualifiedName~CustomerAccessServiceTests --no-restore`

Expected: PASS.

### Task 3: Login Destination and Portuguese Validation

**Files:**
- Modify: `Orofoods.Web/Areas/Identity/Pages/Account/Login.cshtml.cs`
- Test: `Orofoods.Web.Tests/Views/IdentityLoginViewTests.cs`

**Interfaces:**
- Consumes: `UserManager<ApplicationUser>` for role detection after successful sign-in.
- Produces: administrator default destination `/Admin/Dashboard` and Portuguese login validation messages.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void Login_view_model_declares_portuguese_required_messages()
{
    var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web/Areas/Identity/Pages/Account/Login.cshtml.cs"));
    var source = File.ReadAllText(path);

    Assert.Contains("O e-mail corporativo e obrigatorio.", source);
    Assert.Contains("A senha e obrigatoria.", source);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj --filter FullyQualifiedName~IdentityLoginViewTests --no-restore`

Expected: FAIL because the model uses framework-default required messages.

- [ ] **Step 3: Write the minimal implementation**

```csharp
[Required(ErrorMessage = "O e-mail corporativo e obrigatorio.")]
public string Email { get; set; } = string.Empty;

[Required(ErrorMessage = "A senha e obrigatoria.")]
public string Password { get; set; } = string.Empty;
```

- [ ] **Step 4: Redirect administrators after a default login**

```csharp
if (ReturnUrl == Url.Content("~/") && await userManager.IsInRoleAsync(user, "Administrador"))
{
    return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
}
```

- [ ] **Step 5: Run focused tests to verify they pass**

Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj --filter FullyQualifiedName~IdentityLoginViewTests --no-restore`

Expected: PASS.

### Task 4: Full Verification

**Files:**
- Verify: `Orofoods.slnx`

- [ ] **Step 1: Run the complete test suite**

Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj --no-restore`

Expected: all tests pass.

- [ ] **Step 2: Build the solution**

Run: `dotnet build Orofoods.slnx --no-restore`

Expected: build succeeds with zero errors.
