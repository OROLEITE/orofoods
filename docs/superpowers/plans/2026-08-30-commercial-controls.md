# Commercial Controls Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent confirmed orders from exceeding available inventory or customer credit while keeping a traceable reservation for every order line.

**Architecture:** Product inventory holds on-hand and reserved case quantities. A reservation service validates and applies inventory and credit changes atomically during order confirmation, then releases them only when an order is cancelled. Admin inventory maintenance uses the same inventory model; API catalog responses expose only customer-safe availability.

**Tech Stack:** ASP.NET Core MVC, EF Core, PostgreSQL, SQLite test migrations, xUnit.

**Spec:** `docs/superpowers/specs/2026-08-30-commercial-controls-design.md`

## Global Constraints

- Store inventory in whole product cases, matching `OrderItem.Quantity`.
- Keep PostgreSQL as the production provider and add a matching SQLite test migration.
- Use Portuguese validation messages in customer-facing MVC views.
- Do not implement a WMC adapter or change existing ERP integration contracts.
- Use `apply_patch` for manual file edits and verify with `dotnet build` plus `dotnet test`.

---

### Task 1: Inventory Persistence Model

**Files:**
- Create: `Orofoods.Web/Models/Inventory/ProductInventory.cs`
- Create: `Orofoods.Web/Models/Inventory/InventoryReservation.cs`
- Create: `Orofoods.Web/Models/Inventory/InventoryReservationStatus.cs`
- Modify: `Orofoods.Web/Data/ApplicationDbContext.cs`
- Modify: `Orofoods.Web/Data/PostgreSqlApplicationDbContext.cs`
- Create: PostgreSQL and SQLite migrations for inventory tables
- Test: `Orofoods.Web.Tests/Models/ProductInventoryTests.cs`

**Interfaces:**
- Produces `ProductInventory(ProductId, QuantityOnHand, QuantityReserved)` and `InventoryReservation(OrderId, ProductId, Quantity, Status)`.
- Produces `ProductInventory.AvailableQuantity` and a unique `(OrderId, ProductId)` reservation index.

- [ ] **Step 1: Write failing model tests**

```csharp
[Fact]
public void AvailableQuantity_SubtractsReservedCases()
{
    var inventory = new ProductInventory { QuantityOnHand = 12, QuantityReserved = 5 };
    Assert.Equal(7, inventory.AvailableQuantity);
}
```

- [ ] **Step 2: Run the focused test and confirm it fails because the model is absent**

Run: `dotnet test Orofoods.Web.Tests --filter FullyQualifiedName~ProductInventoryTests`

- [ ] **Step 3: Add models, relationships, constraints and migrations**

```csharp
builder.Entity<InventoryReservation>()
    .HasIndex(x => new { x.OrderId, x.ProductId })
    .IsUnique();
```

- [ ] **Step 4: Run the focused test and migration tests**

Run: `dotnet test Orofoods.Web.Tests --filter "FullyQualifiedName~ProductInventoryTests|FullyQualifiedName~Migration"`

### Task 2: Reservation and Credit Service

**Files:**
- Create: `Orofoods.Web/Services/Commercial/CommercialValidationResult.cs`
- Create: `Orofoods.Web/Services/Commercial/OrderReservationService.cs`
- Modify: `Orofoods.Web/Services/Orders/CartService.cs`
- Modify: `Orofoods.Web/Program.cs`
- Test: `Orofoods.Web.Tests/Services/OrderReservationServiceTests.cs`

**Interfaces:**
- Produces `Task<CommercialValidationResult> ReserveAsync(Order order)`.
- Produces `Task ReleaseAsync(Order order)`.
- `CommercialValidationResult` contains `IsValid` and a Portuguese `ErrorMessage`.

- [ ] **Step 1: Write failing tests for inventory, credit, PIX and duplicate release**

```csharp
[Fact]
public async Task ReserveAsync_RejectsOrder_WhenInventoryIsInsufficient()
{
    var result = await service.ReserveAsync(order);
    Assert.False(result.IsValid);
    Assert.Contains("Estoque insuficiente", result.ErrorMessage);
}
```

- [ ] **Step 2: Run focused tests and confirm failure**

Run: `dotnet test Orofoods.Web.Tests --filter FullyQualifiedName~OrderReservationServiceTests`

- [ ] **Step 3: Implement atomic reservation and release logic**

```csharp
if (paymentMethod != "PIX")
    customer.CreditUsed += order.Total;
inventory.QuantityReserved += item.Quantity;
```

- [ ] **Step 4: Run focused tests**

Run: `dotnet test Orofoods.Web.Tests --filter FullyQualifiedName~OrderReservationServiceTests`

### Task 3: Checkout and Cancellation Wiring

**Files:**
- Modify: `Orofoods.Web/Controllers/PortalController.cs`
- Modify: `Orofoods.Web/Services/Orders/AdminOrderService.cs`
- Modify: checkout views and view models only if an error needs a model property
- Test: `Orofoods.Web.Tests/Services/AdminOrderServiceTests.cs`
- Test: `Orofoods.Web.Tests/Controllers/PortalControllerCheckoutTests.cs`

**Interfaces:**
- Consumes `OrderReservationService.ReserveAsync(Order)` before a confirmed order is persisted.
- Consumes `OrderReservationService.ReleaseAsync(Order)` on transition to `OrderStatus.Cancelled`.

- [ ] **Step 1: Write failing checkout and cancellation tests**

```csharp
Assert.Contains("Estoque insuficiente", result.ModelState.Values.Single().Errors.Single().ErrorMessage);
```

- [ ] **Step 2: Run focused tests and confirm failure**

Run: `dotnet test Orofoods.Web.Tests --filter "FullyQualifiedName~PortalControllerCheckoutTests|FullyQualifiedName~AdminOrderServiceTests"`

- [ ] **Step 3: Invoke the reservation service in confirmation and cancellation flows**

```csharp
var reservation = await reservationService.ReserveAsync(order);
if (!reservation.IsValid) return View(model);
```

- [ ] **Step 4: Run focused tests**

Run: `dotnet test Orofoods.Web.Tests --filter "FullyQualifiedName~PortalControllerCheckoutTests|FullyQualifiedName~AdminOrderServiceTests"`

### Task 4: Administration and Catalog Availability

**Files:**
- Create: `Orofoods.Web/Areas/Admin/Controllers/InventoryController.cs`
- Create: `Orofoods.Web/Areas/Admin/Views/Inventory/Index.cshtml`
- Modify: `Orofoods.Web/Areas/Admin/Views/Shared/_Layout.cshtml`
- Modify: `Orofoods.Web/Controllers/Api/V1/CatalogController.cs`
- Test: `Orofoods.Web.Tests/Services/InventoryAdministrationTests.cs`

**Interfaces:**
- Produces admin inventory listing and POST adjustment endpoint protected by the existing admin authorization.
- Catalog responses retain `IsAvailable` but calculate it from active product and positive available inventory.

- [ ] **Step 1: Write failing tests for adjustment and catalog availability**

```csharp
Assert.Equal(18, inventory.QuantityOnHand);
Assert.False(productResponse.IsAvailable);
```

- [ ] **Step 2: Run focused tests and confirm failure**

Run: `dotnet test Orofoods.Web.Tests --filter "FullyQualifiedName~InventoryAdministrationTests|FullyQualifiedName~Catalog"`

- [ ] **Step 3: Implement admin adjustment and safe catalog response**

```csharp
inventory.QuantityOnHand = Math.Max(0, requestedQuantity);
```

- [ ] **Step 4: Run focused tests**

Run: `dotnet test Orofoods.Web.Tests --filter "FullyQualifiedName~InventoryAdministrationTests|FullyQualifiedName~Catalog"`

### Task 5: Full Verification

**Files:**
- Modify: tests only as required by the four tasks above.

- [ ] **Step 1: Build application without restoring packages**

Run: `dotnet build Orofoods.Web/Orofoods.Web.csproj --no-restore`
Expected: exit code 0, no compilation errors.

- [ ] **Step 2: Run complete test suite without restoring packages**

Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj --no-restore`
Expected: exit code 0, no failed tests.
