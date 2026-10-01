# Card on Delivery + Mercado Pago Point Phase 2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Add driver and terminal assignment management, block completion of unpaid Card on Delivery orders, and define a fail-closed Point provider boundary without external calls.

**Architecture:** Add independent `Driver`, `PaymentTerminal`, and historical `DriverPaymentTerminalAssignment` entities. Reuse `Payment` for the future Point attempt and reference its assignment history; use a separate disabled `IPointPaymentProvider`. Enforce delivery completion in `AdminOrderService` and expose internal CRUD only to Administrators.

**Tech Stack:** ASP.NET Core MVC, EF Core, PostgreSQL and SQLite migration sets, xUnit.

**Spec:** `docs/architecture/specs/card-on-delivery-point-phase2.md`

## Global Constraints

- “Feature flag: OFF”.
- “Não implementar comunicação real com Mercado Pago Point nesta fase.”
- “External calls nesta fase: ZERO”.
- “Não executar migration em Staging. Não executar em Production.”
- “Não fazer commit, push ou deploy sem autorização separada.”
- “Não armazenar access token; secret; dados de cartão; credenciais.”
- “A mudança de status para Delivered deve ser rejeitada” for Card on Delivery unless the corresponding `PaymentStatus` is `Approved`.
- “Não aplicar essa regra a CASH / À vista ou outras modalidades existentes.”

## Review Focus

- Inactive drivers, terminals, or assignments must never be charge-eligible; cover in Task 1.
- Reassigning a terminal must retain its old assignment and prevent concurrent active owners; cover in Task 1.
- A Card on Delivery order with no persisted `Payment` must be treated as unpaid; cover in Task 3.
- Admin POST endpoints must reject non-administrators even when called directly; cover in Task 2.
- Point payment methods must remain fail-closed and make no HTTP request when disabled or called directly; cover in Task 4.

## Plan Decisions and Constraints

- Do not reuse `SalesRepresentative` or add an Identity driver role; the approved design is an independent operational driver registry without login.
- Preserve terminal assignment history by setting `EndedAt`; never delete an assignment.
- A terminal may have only one active assignment. Multiple terminals per driver remain allowed, as approved in the inspected design; each payment attempt references the exact historical assignment.
- A terminal without a nonblank `DeviceId`, an active assignment, an active terminal, or an active driver is not eligible for future Point use. Store/POS IDs remain optional metadata until real values are supplied.
- Deactivating a driver or terminal closes its current assignments in the same operation, retaining their history.
- Use `Payment` as the source of truth for approval. If no CardOnDelivery payment exists, block `Delivered`. Select the latest matching payment if historical rows exist.
- Add `Payment.DriverPaymentTerminalAssignmentId` as a nullable FK for future attribution; do not add a duplicate payment-attempt table.
- Keep both feature switches false: existing `Payments:CardOnDeliveryEnabled` and new `Payments:MercadoPagoPointEnabled`.
- Create a PostgreSQL migration only. The active App Service provider is PostgreSQL; the legacy SQLite migration snapshot predates the `Payments` table, so do not widen this feature into an unrelated SQLite schema rebuild. SQLite behavior remains covered through `EnsureCreated` model tests.
- Do not commit at task boundaries or at completion because the user explicitly prohibited commits; record this deviation in the ledger.

## Interfaces

- Task 1 produces `Driver`, `PaymentTerminal`, `PaymentTerminalProvider`, `DriverPaymentTerminalAssignment`, `IDriverPaymentTerminalService`, and `IPaymentTerminalEligibilityService`.
- Task 2 consumes those models/services and produces Administrator-only MVC management for drivers, terminals, and assignment history.
- Task 3 consumes the existing `AdminOrderService`, `Payment`, `PaymentTerm.Code`, and `PaymentStatus`; it returns a controlled status-update result for the controller to display.
- Task 4 produces `IPointPaymentProvider` and a `DisabledPointPaymentProvider` that always returns a controlled disabled result and has no HTTP dependency.
- Task 5 maps the approved schema in the active PostgreSQL migration provider, registers the services/options, verifies migration scripts, and runs all requested validation commands.

Exact produced contracts: `IDriverPaymentTerminalService.AssignAsync(int driverId, int terminalId, CancellationToken)`, `EndAsync(int assignmentId, CancellationToken)`, `DeactivateDriverAsync(int driverId, CancellationToken)`, and `DeactivateTerminalAsync(int terminalId, CancellationToken)` return `AssignmentResult(bool Succeeded, DriverPaymentTerminalAssignment? Assignment, string? ErrorMessage)`; `IPaymentTerminalEligibilityService.CanBeUsedForPointPayment(DriverPaymentTerminalAssignment)` returns `bool`; `Payment.DriverPaymentTerminalAssignmentId` is nullable and points to the exact historical assignment; `AdminOrderService.UpdateStatusAsync` returns `AdminOrderStatusUpdateResult(bool Succeeded, string? ErrorMessage)`; `IPointPaymentProvider` exposes `CreatePaymentAsync`, `GetPaymentStatusAsync`, and `CancelPendingPaymentAsync`, all returning `PointPaymentResult` with a `Disabled` state when Point is unavailable.

Exact produced contracts: `IDriverPaymentTerminalService.AssignAsync(int driverId, int terminalId, CancellationToken)` and `EndAsync(int assignmentId, CancellationToken)` both return `AssignmentResult(Succeeded, Assignment, ErrorMessage)`; `IPaymentTerminalEligibilityService.CanBeUsedForPointPayment(DriverPaymentTerminalAssignment)` returns `bool`; `Payment.DriverPaymentTerminalAssignmentId` is nullable and points to the exact historical assignment; `AdminOrderService.UpdateStatusAsync` returns `AdminOrderStatusUpdateResult(bool Succeeded, string? ErrorMessage)`; `IPointPaymentProvider` exposes `CreatePaymentAsync`, `GetPaymentStatusAsync`, and `CancelPendingPaymentAsync`, all returning `PointPaymentResult` with `Disabled` state when Point is unavailable.

---

### Task 1: Driver, terminal, assignment history, and service invariants

**Files:**
- Create: `Orofoods.Web/Models/Payments/PaymentTerminal.cs`
- Create: `Orofoods.Web/Models/Payments/PaymentTerminalProvider.cs`
- Create: `Orofoods.Web/Models/Delivery/Driver.cs`
- Create: `Orofoods.Web/Models/Payments/DriverPaymentTerminalAssignment.cs`
- Create: `Orofoods.Web/Services/Payments/IDriverPaymentTerminalService.cs`
- Create: `Orofoods.Web/Services/Payments/DriverPaymentTerminalService.cs`
- Create: `Orofoods.Web/Services/Payments/IPaymentTerminalEligibilityService.cs`
- Create: `Orofoods.Web/Services/Payments/PaymentTerminalEligibilityService.cs`
- Modify: `Orofoods.Web/Models/Payments/Payment.cs`
- Modify: `Orofoods.Web/Data/ApplicationDbContext.cs`
- Test: `Orofoods.Web.Tests/Services/DriverPaymentTerminalServiceTests.cs`
- Test: `Orofoods.Web.Tests/Services/PaymentTerminalEligibilityServiceTests.cs`

**Interfaces:**
- Produces assignment API: `Task<AssignmentResult> AssignAsync(int driverId, int terminalId, CancellationToken cancellationToken = default)`, `Task<AssignmentResult> EndAsync(int assignmentId, CancellationToken cancellationToken = default)`, `Task<AssignmentResult> DeactivateDriverAsync(int driverId, CancellationToken cancellationToken = default)`, and `Task<AssignmentResult> DeactivateTerminalAsync(int terminalId, CancellationToken cancellationToken = default)`. `AssignmentResult` contains `Succeeded`, `Assignment`, and `ErrorMessage`.
- Produces eligibility API: `bool CanBeUsedForPointPayment(DriverPaymentTerminalAssignment assignment)`.
- `Driver` has `Id`, required `Name`, `IsActive`, `CreatedAt`, and `UpdatedAt`. `PaymentTerminal` has `Id`, `Provider`, nullable `DeviceId`/`StoreId`/`PosId`, `IsActive`, and timestamps. Assignment has `DriverId`, `PaymentTerminalId`, `StartedAt`, nullable `EndedAt`, and `CreatedAt`.

- [x] Write `AssignAsync_RequiresActiveDriverAndTerminal`, `AssignAsync_RejectsSecondActiveOwnerForTerminal`, `EndAsync_PreservesHistoryAndAllowsReassignment`, `DeactivateDriverAsync_EndsAssignmentsWithoutDeletingHistory`, `DeactivateTerminalAsync_EndsAssignmentsWithoutDeletingHistory`, `History_ReturnsEndedAndCurrentAssignments`, `CanBeUsedForPointPayment_RequiresActiveDriverTerminalAssignmentAndDeviceId`, and `Models_DoNotExposeCredentialFields` tests.
- [x] Run the focused tests and confirm failures are for missing behavior/types.
- [x] Implement the models, EF relationships/indexes, assignment service, and eligibility rule. Use a serializable transaction plus a filtered unique index on active `PaymentTerminalId` assignments. Deactivation closes current assignments without deleting them.
- [x] Run focused tests and confirm all assignment invariants pass.

### Task 2: Administrator-only CRUD and assignment management

**Files:**
- Create: `Orofoods.Web/Areas/Admin/Controllers/DriversController.cs`
- Create: `Orofoods.Web/Areas/Admin/Controllers/PaymentTerminalsController.cs`
- Create: `Orofoods.Web/Areas/Admin/Controllers/DriverPaymentTerminalAssignmentsController.cs`
- Create: views under `Orofoods.Web/Areas/Admin/Views/Drivers/`, `PaymentTerminals/`, and `DriverPaymentTerminalAssignments/`
- Modify: `Orofoods.Web/Views/Shared/_AdminNavigation.cshtml`
- Test: `Orofoods.Web.Tests/Controllers/AdminPaymentTerminalManagementTests.cs`

**Interfaces:** Consumes Task 1 models and `IDriverPaymentTerminalService`; controllers are guarded by `[Area("Admin"), Authorize(Roles = "Administrador")]`. Admin forms bind narrow input records without secret or card fields.

- [x] Write `DriversController_RequiresAdministrator`, `PaymentTerminalsController_RequiresAdministrator`, and `DriverPaymentTerminalAssignmentsController_RequiresAdministrator` authorization metadata tests plus `DriverAndTerminalForms_ContainOnlyApprovedFields` and action tests for create/edit/deactivate, assign/end/list history, and duplicate feedback.
- [x] Run focused tests and confirm the missing controllers/guards are reported.
- [x] Implement only the requested basic management: driver name and active state; provider and Device/Store/POS metadata; create/end assignment and history display. Do not make external validation calls.
- [x] Run focused controller tests and confirm route actions and authorization attributes.

### Task 3: Backend Delivered guard and Admin feedback

**Files:**
- Modify: `Orofoods.Web/Services/Orders/AdminOrderService.cs`
- Modify: `Orofoods.Web/Areas/Admin/Controllers/OrdersController.cs`
- Modify: `Orofoods.Web/Areas/Admin/Views/Orders/Details.cshtml`
- Test: `Orofoods.Web.Tests/Services/AdminOrderServiceTests.cs`

**Interfaces:** `UpdateStatusAsync` returns `AdminOrderStatusUpdateResult(bool Succeeded, string? ErrorMessage)`; Admin controller reports the clear message “O pedido utiliza Cartão na Entrega e o pagamento ainda não foi aprovado.”

- [x] Write `CardOnDelivery_PendingPayment_CannotBeDelivered`, `CardOnDelivery_WithoutPayment_CannotBeDelivered`, `CardOnDelivery_ApprovedPayment_CanBeDelivered`, `OtherPaymentTerms_CanBeDeliveredWithoutCardApproval`, and `PendingCardOnDelivery_CanStillBeCancelled` tests. These invoke `AdminOrderService` directly, exercising the backend boundary.
- [x] Run the focused service tests and confirm the expected failures.
- [x] Enforce the guard before order/status history writes; base it on `PaymentTerm.Code == "CARD_ON_DELIVERY"` and latest matching `Payment.Method`/`Payment.Status`. Keep cancellation and all other terms unchanged. Display method/status in Admin order details and controlled feedback on rejection.
- [x] Run focused tests and confirm all status behaviors.

### Task 4: Disabled Point provider boundary and flags

**Files:**
- Create: `Orofoods.Web/Services/Payments/IPointPaymentProvider.cs`
- Create: `Orofoods.Web/Services/Payments/DisabledPointPaymentProvider.cs`
- Create: `Orofoods.Web/Services/Payments/MercadoPagoPointOptions.cs`
- Modify: `Orofoods.Web/Program.cs`
- Modify: `Orofoods.Web/appsettings.json`
- Test: `Orofoods.Web.Tests/Services/DisabledPointPaymentProviderTests.cs`

**Interfaces:** Provider methods accept a `PointPaymentRequest`/payment identifier and return `PointPaymentResult` with `State.Disabled` and a localized safe message. `MercadoPagoPointOptions.Enabled` defaults to `false`. No `HttpClient` is injected into this implementation.

- [x] Write `CreateTerminalPaymentAsync_ReturnsDisabledWithoutExternalCall`, `GetPaymentStatusAsync_ReturnsDisabledWithoutExternalCall`, `CancelPendingPaymentAsync_ReturnsDisabledWithoutExternalCall`, and `DefaultConfiguration_DisablesCardOnDeliveryAndPoint` tests.
- [x] Run tests and confirm missing types/registration fail as expected.
- [x] Implement the provider interface and disabled provider, register it, and ensure both feature flags remain false by default. Do not add any Point HTTP gateway.
- [x] Run focused provider and configuration tests.

### Task 5: PostgreSQL migration and end-to-end verification

**Files:**
- Create: migration under `Orofoods.Web/Data/MigrationsPostgreSql/` for `Drivers`, `PaymentTerminals`, `DriverPaymentTerminalAssignments`, nullable `Payments.DriverPaymentTerminalAssignmentId`, FKs, indexes, and the filtered unique active-terminal index.
- Modify: `Orofoods.Web/Data/MigrationsPostgreSql/PostgreSqlApplicationDbContextModelSnapshot.cs`.
- Modify: `docs/architecture/decisions/ADR-2026-09-29-card-on-delivery-mercado-pago-point.md` (Phase 2 implementation notes only).

- [x] Add `DriverPaymentTerminalMigrationTests` proving PostgreSQL FKs/index/filter names; create the migration without executing it against any database.
- [x] Build the migration script/model and confirm migration checks pass locally.
- [x] Run `dotnet restore Orofoods.slnx`.
- [x] Run `dotnet build Orofoods.slnx -c Release --no-restore`.
- [x] Run focused assignment, admin authorization, status guard, point provider, and PostgreSQL migration contract tests. The assignment/model tests use SQLite `EnsureCreated`.
- [x] Run the complete Release test suite.
- [x] Run `git diff --check`; inspect all changed files, ensure flags are false and confirm no Point HTTP calls, external calls, Azure work, deploy, or migration execution.
- [x] Do not commit, push, or deploy.
