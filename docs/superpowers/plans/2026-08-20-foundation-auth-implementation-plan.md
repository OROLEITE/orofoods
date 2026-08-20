# Orofoods Foundation and Authentication Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Evolve the existing Orofoods ASP.NET Core MVC application into a stable B2B foundation with company-aware Identity, customer approval gates, normalized commercial entities, pricing/payment configuration, and test coverage.

**Architecture:** Keep `Orofoods.Web` as a modular monolith. Extend ASP.NET Core Identity with `ApplicationUser`, keep SQLite for this phase, place commercial rules behind services, and separate Customer/Admin UI through MVC Areas. Migrate the existing EF Core model incrementally so historical orders preserve commercial snapshots.

**Tech Stack:** ASP.NET Core MVC, C#, .NET 10 (`net10.0` as currently configured), Entity Framework Core 10, SQLite, ASP.NET Core Identity, Razor Views, Bootstrap 5, xUnit.

**Spec:** `docs/superpowers/specs/2026-08-20-foundation-auth-design.md`

## Global Constraints

- Keep SQLite during this phase.
- Keep `Orofoods.Web` as the application host.
- Do not move business rules into controllers or Razor views.
- Customer is the B2B company; ApplicationUser is a login.
- One Customer can have many users.
- All Customer users have equal customer permissions in v1.
- Global roles: `Administrador`, `Vendedor`, `Cliente`.
- New B2B registrations create a `Pending` customer.
- Pending, Blocked, and Inactive customers cannot access commercial ordering functions.
- Historical OrderItem pricing/name/SKU remain immutable snapshots.
- SQL Server, native mobile, ERP/fiscal, carrier integration, and advanced inventory are out of scope.

## Task 1 — Test foundation

**Files**
- Create `Orofoods.Web.Tests/Orofoods.Web.Tests.csproj`
- Create `Orofoods.Web.Tests/TestDbFactory.cs`
- Modify `Orofoods.slnx`

**Steps**
- [ ] Create xUnit project targeting `net10.0`, referencing `Orofoods.Web` and EF Core SQLite 10.0.11.
- [ ] Add test project to `Orofoods.slnx`.
- [ ] Implement `TestDbFactory.Create()` using an open in-memory SQLite connection and `ApplicationDbContext`.
- [ ] Run `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj` and require a clean baseline.

## Task 2 — ApplicationUser and company-aware Identity

**Files**
- Create `Orofoods.Web/Models/Identity/ApplicationUser.cs`
- Create `Orofoods.Web.Tests/Data/ModelMappingTests.cs`
- Modify `Orofoods.Web/Data/ApplicationDbContext.cs`
- Modify `Orofoods.Web/Program.cs`

**Contract**
```csharp
public class ApplicationUser : IdentityUser
{
    public int? CustomerId { get; set; }
    public int? SalesRepresentativeId { get; set; }
    public bool IsActive { get; set; } = true;
}
```

**TDD steps**
- [ ] Write a model test that fails because `ApplicationUser` is not yet part of the EF Identity model.
- [ ] Add `ApplicationUser`.
- [ ] Change `ApplicationDbContext` to `IdentityDbContext<ApplicationUser>`.
- [ ] Change `AddDefaultIdentity<IdentityUser>` to `AddDefaultIdentity<ApplicationUser>`.
- [ ] Re-run the focused test and require PASS.

## Task 3 — Normalize commercial entities

**Create focused model files**
- Customers: `Customer`, `CustomerAddress`, `CustomerStatus`, `SalesRepresentative`
- Catalog: `ProductCategory`, `Product`, `ProductImage`
- Pricing: `PriceTable`, `PriceTableItem`, `PaymentTerm`, `CustomerPaymentTerm`
- Orders: `Order`, `OrderItem`, `OrderStatus`, `OrderStatusHistory`, `FavoriteProduct`, `SavedOrder`, `SavedOrderItem`

**Rules**
- `Product` references `ProductCategoryId` instead of a category string.
- `PriceTableItem` has unique `(PriceTableId, ProductId)`.
- `CustomerPaymentTerm` has unique `(CustomerId, PaymentTermId)`.
- `FavoriteProduct` uses composite key `(CustomerId, ProductId)`.
- Monetary values use precision `(12,2)`.
- Substitute product relation uses `DeleteBehavior.Restrict`.
- Customer/ApplicationUser relation must not cascade-delete Identity users.
- `OrderItem` stores `ProductNameSnapshot`, `SkuSnapshot`, `UnitPrice`, `Subtotal`.

**TDD steps**
- [ ] Add mapping tests for all foundation entities and relationships; confirm failures.
- [ ] Implement the entities and DbSets.
- [ ] Configure indexes, precision, keys, and relationships in `ApplicationDbContext`.
- [ ] Run all model mapping tests; require PASS.
- [ ] Remove legacy `CommerceModels.cs` only after the new model compiles and compatibility is handled.

## Task 4 — Customer access service and ApprovedCustomer policy

**Create**
- `Services/Customers/ICustomerAccessService.cs`
- `Services/Customers/CustomerAccessService.cs`
- `Authorization/ApprovedCustomerRequirement.cs`
- `Authorization/ApprovedCustomerHandler.cs`
- tests for access service and authorization handler

**Contract**
```csharp
Task<CustomerAccessResult> GetAccessAsync(
    ApplicationUser user,
    CancellationToken cancellationToken = default);
```

Test these cases before implementation: approved active user allowed; Pending denied; Blocked denied; Inactive customer denied; inactive user denied; user without CustomerId denied.

Register policy `ApprovedCustomer`, requiring role `Cliente` plus `ApprovedCustomerRequirement`.

## Task 5 — Price resolution through PriceTable

**Create**
- `Services/Pricing/IPriceService.cs`
- `Services/Pricing/PriceService.cs`
- `Orofoods.Web.Tests/Pricing/PriceServiceTests.cs`

**Contract**
```csharp
Task<decimal?> GetPriceAsync(
    int customerId,
    int productId,
    CancellationToken cancellationToken = default);
```

**TDD cases**
- assigned table + product item => returns table price
- no matching table item => returns null
- customer without table => returns null

Customer-facing pricing must not silently fall back to `Product.BasePrice`.

## Task 6 — B2B registration creates Pending customer

**Create**
- `Services/Identity/ICustomerRegistrationService.cs`
- `Services/Identity/CustomerRegistrationService.cs`
- registration service tests

**Modify**
- Identity Register page model/view under `Areas/Identity/Pages/Account`
- `Program.cs`

**Contract**
```csharp
Task<CustomerRegistrationResult> RegisterAsync(
    CustomerRegistrationRequest request,
    CancellationToken cancellationToken = default);
```

Successful registration must atomically create one `Customer` with `Pending` status, one linked `ApplicationUser`, and role `Cliente`. If user creation or role assignment fails, no partial Customer may remain.

Registration collects company data, responsible person, email, phone/WhatsApp, address, password, and confirmation. Server-side validation is authoritative.

## Task 7 — Customer status gate and Admin approval UI

**Customer area**
- `Areas/Customer/Controllers/DashboardController.cs`
- `Areas/Customer/Views/Dashboard/Index.cshtml`
- `Areas/Customer/Views/Dashboard/Pending.cshtml`

**Admin area**
- `Areas/Admin/Controllers/CustomersController.cs`
- `Areas/Admin/ViewModels/CustomerApprovalViewModel.cs`
- `Areas/Admin/Views/Customers/Index.cshtml`
- `Areas/Admin/Views/Customers/Details.cshtml`

Pending message: `Seu cadastro está em análise.`
Blocked message: `Seu acesso comercial está bloqueado. Entre em contato com a Orofoods.`
Inactive message: `Seu cadastro está inativo. Entre em contato com a Orofoods.`

Admin approval may edit only Status, PriceTableId, CreditLimit, selected PaymentTerm IDs, and SalesRepresentativeId. POST actions use anti-forgery validation. Customer role must not access Admin routes.

## Task 8 — Migrations, idempotent seed, and final verification

**Modify**
- `Orofoods.Web/Data/SeedData.cs`
- `Orofoods.Web/Data/ApplicationDbContext.cs` only if required by migration review
- generated `Orofoods.Web/Data/Migrations/*`

**Seed independently by stable keys**
- roles `Administrador`, `Vendedor`, `Cliente`
- four categories
- approximately twelve demo products
- default price table and price items
- payment terms
- one approved demo customer and one pending demo customer
- one admin, one seller, and linked customer users
- one historical order with explicit SKU/name/unit-price snapshots

Do not return early only because Products already exist. Development demo credentials must come from configuration/user-secrets rather than production hard-coding.

Generate migration:
```bash
dotnet ef migrations add FoundationAndCompanyIdentity --project Orofoods.Web/Orofoods.Web.csproj
```

Review migration before applying. If EF proposes destructive removal of legacy commercial values, insert explicit data-copy SQL before dropping old columns.

Apply to a disposable development database:
```bash
dotnet ef database update --project Orofoods.Web/Orofoods.Web.csproj
```

## Final Verification

Run:
```bash
dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj
dotnet build Orofoods.slnx
dotnet run --project Orofoods.Web/Orofoods.Web.csproj
```

Verify:
- Administrator can access Admin Customers.
- Pending Customer is stopped at status gate.
- Approved Customer reaches Customer Dashboard.
- Customer cannot access Admin routes.
- Customer price comes from PriceTableItem.
- Historical order keeps snapshot values after Product changes.
- Running seed twice creates no duplicates.

## Out of Scope for This Plan

Public institutional redesign, complete catalog UI, quick-order UX, cart, checkout, repeat-order flow, reports, full seller workflow, API/mobile, SQL Server migration, ERP/fiscal integrations, and advanced inventory will be handled in later plans after this foundation is stable.
