# Card on Delivery + Mercado Pago Point Phase 3 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a disabled-by-default Mercado Pago Point provider, idempotent order charging and reconciliation, with automated HTTP fake coverage and no live calls unless test credentials are securely available.

**Architecture:** Implement the Point payload in a dedicated provider behind `IPointPaymentProvider`, preserving the existing online Orders API contract. Add a server-side orchestration path that persists attempts and terminal-assignment snapshots before HTTP, then reuse the signed webhook route to fetch and reconcile authoritative Point status. Keep environment gating strict: Point may only be enabled in `Test`.

**Tech Stack:** .NET 10, ASP.NET Core MVC, Entity Framework Core, PostgreSQL model/migrations, xUnit, `HttpMessageHandler` fakes.

**Spec:** `docs/architecture/specs/card-on-delivery-point-phase3.md`

## Global Constraints

- `Payments:CardOnDeliveryEnabled=false` and `Payments:MercadoPagoPointEnabled=false` remain the versioned defaults.
- Point HTTP is permitted only with Mercado Pago Test credentials and virtual device `SBX0000001`.
- Never use or expose production credentials; never log Authorization, secrets, raw sensitive response bodies, or tokens.
- No Staging/Production enablement, Azure changes, deploy, database access, migration execution, commit, push, or Production action.
- No test will make an external HTTP call; use fake handlers. Only consider official Test calls if secure test credentials are actually available, and then report separately.
- Reuse `Payment` as the payment-attempt record and preserve `DriverPaymentTerminalAssignmentId` snapshots.
- Preserve the online PIX/card payload and existing payment flows.
- Do not add a Fase 3 migration unless a test proves an objective schema requirement; if generated, leave unapplied and report it.

## Review Focus

- Environment/configuration mismatch: enabled Point outside `Test` must fail before HTTP; cover in Task 1.
- Timeout or lost create response: retries must reuse the persisted attempt and idempotency key; cover in Task 2.
- Parallel charge requests: only one active attempt/order may call create; cover in Task 2.
- Out-of-order or repeated provider state: no payment regression or duplicate approval side effect; cover in Task 3.
- Assignment change or inactive terminal: reject before external call or preserve already persisted snapshot; cover in Task 2.

---

### Task 1: Point contracts, configuration, and HTTP provider

**Files:**
- Modify: `Orofoods.Web/Services/Payments/IPointPaymentProvider.cs`
- Modify: `Orofoods.Web/Services/Payments/MercadoPagoPointOptions.cs`
- Create: `Orofoods.Web/Services/Payments/MercadoPagoPointPaymentProvider.cs`
- Modify: `Orofoods.Web/Program.cs`
- Test: `Orofoods.Web.Tests/Services/MercadoPagoPointPaymentProviderTests.cs`
- Test: `Orofoods.Web.Tests/Services/DisabledPointPaymentProviderTests.cs`

**Interfaces:**
- Consumes: current `IPointPaymentProvider`, `MercadoPagoPointOptions`, `PaymentGatewayException` patterns.
- Produces: `MercadoPagoPointPaymentProvider(HttpClient, IOptions<MercadoPagoPointOptions>) : IPointPaymentProvider`; Point create/get/cancel requests use Point-specific JSON models and normalized `PointPaymentResult` values.

- [ ] **Step 1: Write failing provider tests** for `type=point`, amount formatted with invariant two decimals, `config.point.terminal_id` composed from a supported `poi_type` and `SBX0000001`, Bearer header present, `X-Idempotency-Key`, create/get/cancel paths, all documented status mappings, malformed response, 4xx/5xx, timeout, cancellation, and sanitized errors. Assert local Store/POS IDs are not emitted as undocumented Point order fields.
- [ ] **Step 2: Run the focused tests and confirm the expected compile/test failure** because the real provider/status contract does not exist.
- [ ] **Step 3: Implement the smallest Point contract/options/provider changes**; add `PaymentStatus.ActionRequired` as a new final enum member without renumbering existing values. Keep default registration disabled and refuse an enabled provider unless `Environment=Test`.
- [ ] **Step 4: Run `dotnet test Orofoods.Web.Tests --configuration Release --filter FullyQualifiedName~MercadoPagoPointPaymentProviderTests` and `... --filter FullyQualifiedName~DisabledPointPaymentProviderTests`; confirm pass.**
- [ ] **Step 5: Confirm the existing online gateway tests pass** with `dotnet test Orofoods.Web.Tests --configuration Release --filter FullyQualifiedName~MercadoPagoPaymentGatewayTests`.

### Task 2: Persisted charge orchestration and concurrency guard

**Files:**
- Create: `Orofoods.Web/Services/Payments/PointPaymentOrchestrationService.cs`
- Create: `Orofoods.Web/Services/Payments/PointPaymentAttemptGate.cs`
- Create: `Orofoods.Web/Services/Payments/IPointPaymentOrchestrationService.cs` if needed to keep MVC independently testable.
- Modify: `Orofoods.Web/Data/ApplicationDbContext.cs` only if existing relationships/indexes prove insufficient.
- Test: `Orofoods.Web.Tests/Services/PointPaymentOrchestrationServiceTests.cs`
- Test: `Orofoods.Web.Tests/Services/PointPaymentOrchestrationServiceTests.cs`

**Interfaces:**
- Consumes: `IPointPaymentProvider.CreateTerminalPaymentAsync(PointPaymentRequest, CancellationToken)`, `GetPaymentStatusAsync(string, CancellationToken)`, `Payment`, and `IPaymentTerminalEligibilityService`.
- Produces: `Task<PointPaymentOperationResult> StartChargeAsync(int orderId, int assignmentId, string requestKey, CancellationToken)` and `Task<PointPaymentOperationResult> RefreshAsync(int paymentId, CancellationToken)`; amount, terminal metadata, active assignment and persisted provider idempotency key are loaded/generated server-side. `requestKey` is a server-rendered per-form operation nonce used only for repeat-submit de-duplication, not for authorization.

- [ ] **Step 1: Write failing tests** for non-Point order/method, disabled flag, wrong environment, invalid amount/order state, inactive driver/terminal/assignment, changed assignment, one-active-attempt reuse, persisted assignment snapshot, timeout/retry with the same saved key, duplicate submit nonce, and two simultaneous calls for one order producing one provider order even when the forms have different nonces.
- [ ] **Step 2: Run only `PointPaymentOrchestrationServiceTests` and `PointPaymentConcurrencyTests`; verify expected missing-service/behavior failures.**
- [ ] **Step 3: Implement serializable per-order attempt acquisition.** Reuse the initial pending `CardOnDelivery` Payment row for the first charge; preserve old attempts and create a new attempt only after a definitive final result. Persist the generated provider key, external reference, amount and active assignment before calling the provider; derive the provider key from the order/payment attempt, not the browser nonce. Map database serialization/unique conflicts to controlled domain outcomes. Never hold a database transaction open during network I/O.
- [ ] **Step 4: Run the focused orchestration/concurrency tests; verify each above invariant.**
- [ ] **Step 5: Run `dotnet test Orofoods.Web.Tests --configuration Release --filter FullyQualifiedName~PaymentOrchestrationServiceTests` to protect existing online payments.**

### Task 3: Point status reconciliation and existing webhook

**Files:**
- Modify: `Orofoods.Web/Services/Payments/PaymentOrchestrationService.cs` or extract a small shared reconciliation service if required by tests.
- Modify: `Orofoods.Web/Services/Payments/MercadoPagoWebhookSignatureValidator.cs` and its interface/options for the separate Point test webhook secret.
- Modify: `Orofoods.Web/Controllers/Api/V1/MercadoPagoWebhooksController.cs`
- Test: `Orofoods.Web.Tests/Services/PointPaymentStatusMappingTests.cs`
- Test: `Orofoods.Web.Tests/Services/MercadoPagoWebhookSignatureValidatorTests.cs`
- Test: `Orofoods.Web.Tests/Controllers/MercadoPagoWebhooksControllerTests.cs`
- Test: `Orofoods.Web.Tests/Integration/MercadoPagoWebhookHttpHarnessTests.cs`

**Interfaces:**
- Consumes: Point provider status results and current webhook signature/reconciliation contracts.
- Produces: Point-specific reconciliation by persisted Gateway/order ID; only authenticated webhook IDs trigger a provider GET. Reconciliation verifies external reference and amount, applies a monotonic transition, and invokes approval side effects once.

- [ ] **Step 1: Write failing tests** for every Point state (`created`, `at_terminal`, `processed/accredited`, `failed`, `canceled`, `expired`, `action_required`, `refunded`, unknown), repeated notification, invalid signature, unknown order, amount/reference mismatch, concurrent poll/webhook, and old events after final status.
- [ ] **Step 2: Run focused tests and confirm failures are due to missing Point reconciliation/transition behavior.**
- [ ] **Step 3: Implement gateway-aware reconciliation in the existing webhook path.** Accept the existing online signature secret or the separately configured Point Test secret; never trust incoming payload status or amount; query the correct provider from persisted Gateway/order identity. Only `processed` with a provider payment marked `accredited` maps to `Approved`. Apply transitions under the existing serializable transaction and avoid duplicate approval callbacks.
- [ ] **Step 4: Run focused mapper, signature, controller, and webhook harness tests; confirm pass.**

### Task 4: Admin operation and delivery guard UX

**Files:**
- Modify: `Orofoods.Web/Areas/Admin/Controllers/OrdersController.cs`
- Modify: `Orofoods.Web/Areas/Admin/Views/Orders/Details.cshtml`
- Modify: `Orofoods.Web/Services/Orders/AdminOrderService.cs` only if the backend guard needs Point-specific status inclusion.
- Test: `Orofoods.Web.Tests/Controllers/AdminPointPaymentTests.cs`
- Test: `Orofoods.Web.Tests/Services/AdminOrderServiceTests.cs`
- Test: `Orofoods.Web.Tests/Views/AdminPointPaymentDisplayTests.cs`

**Interfaces:**
- Consumes: `IPointPaymentOrchestrationService`, current `AdminOrderService`, and Admin role policy.
- Produces: antiforgery-protected Admin POST actions to start/query/cancel a Point attempt; details view shows pending/processing/action-required/final state and requires selection of an active driver-terminal assignment.

- [ ] **Step 1: Write failing controller/view/service tests** for Admin authorization, antiforgery-compatible POST route behavior, Point flag disabled, missing/inactive assignment, statuses rendered, and a direct `Delivered` POST rejected for unpaid CardOnDelivery while CASH remains allowed.
- [ ] **Step 2: Run the focused tests and verify expected missing-action/guard failures.**
- [ ] **Step 3: Implement the minimal server-side Admin actions and view state.** UI disablement is convenience only; `AdminOrderService` remains final authority for Delivered. Predictable provider/domain conflicts return controlled status/TempData, never HTTP 500.
- [ ] **Step 4: Run focused Admin/controller/view tests and existing `AdminOrderServiceTests`.**

### Task 5: Registration, full regression verification, and final review

**Files:**
- Review/modify: `Orofoods.Web/Program.cs`
- Review/modify: `Orofoods.Web/appsettings.json` only to confirm both flags remain false; do not add secrets.
- Review: all changed files and `docs/architecture/specs/card-on-delivery-point-phase3.md`

**Interfaces:**
- Consumes: completed Tasks 1–4.
- Produces: default-safe DI registration, verified feature flags, and final test evidence.

- [ ] **Step 1: Add/verify fake-backed service registration and confirm Point remains disabled by default.**
- [ ] **Step 2: Run restore:** `dotnet restore Orofoods.sln`.
- [ ] **Step 3: Run Release build:** `dotnet build Orofoods.sln --configuration Release --no-restore`.
- [ ] **Step 4: Run focused payment/webhook/concurrency test set, then complete suite:** `dotnet test Orofoods.Web.Tests --configuration Release --no-build`.
- [ ] **Step 5: Run `git diff --check`; inspect exact diff for forbidden configuration/Azure/migration/secret changes; verify flags false and external-call count zero.**
- [ ] **Step 6: Report the exact Phase 3 result.** Do not commit, push, deploy, run migrations, access Staging/Azure, or use official test endpoints during this pass.

## Plan self-review

- Spec coverage: provider and payload (Task 1); idempotency, retries, assignment snapshot and concurrency (Task 2); mapping, webhook signature/deduplication/out-of-order behavior (Task 3); Admin UX, role protection and Delivered backend guard (Task 4); flags and full verification (Task 5).
- Schema: no new entity/column is planned; `ActionRequired` is appended to the enum. If tests reveal a database invariant requires a new partial unique index, stop and record a separate unapplied migration rather than silently expanding schema scope.
- Interfaces: Task 2's orchestration methods are the only new service contract consumed by Tasks 3–4; final names may be adjusted only if the codebase requires it, with the same behaviors and test contracts preserved.
- External callback: official webhook delivery is not simulated through a tunnel; fake harness covers signature and reconciliation. Remote tests remain blocked until credentials and an already available test callback URL exist.
- API payload: the current official Create Point Order reference documents `config.point.terminal_id`; StoreId/PosId stay local terminal metadata and are not serialized into the order request.
- No commit step is present because the user has not authorized commits.
