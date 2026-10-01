# Mercado Pago Point Concurrency Hardening Plan

> **For agentic workers:** Use `superpowers:executing-plans` inline. Steps use checkbox syntax for tracking.

**Goal:** Prevent duplicate logical Point charges for one order while allowing independent orders and application instances to proceed safely.

**Architecture:** Replace the process-wide semaphore with a reference-counted per-OrderId gate. Serialize claim and status-write transactions across instances with `SELECT ... FOR UPDATE` on the existing `Orders` row; use PostgreSQL `ReadCommitted` after acquiring that lock so the next query sees the previous holder's commit. Keep HTTP outside both lock scopes and retain the persisted Payment idempotency key.

**Tech Stack:** .NET 10, EF Core/Npgsql, SQLite in-memory test fakes, xUnit.

**Spec:** `docs/architecture/specs/point-concurrency-hardening.md`

## Global Constraints

- External calls: 0.
- No credentials, migrations, deploys, Azure or Production changes.
- No new migration unless an existing-row PostgreSQL lock cannot provide the required guarantee; stop before creating one if needed.
- Provider calls happen only after attempt commit and outside DB locks/transactions.
- Keep authoritative status, amount and external-reference checks unchanged.

## Review Focus

- Same order and distinct request nonces: one persisted attempt and one provider idempotency key.
- Independent orders: keyed local gates and database row locks do not block one another.
- Another app instance/process restart: attempt and idempotency key are recovered from `Payment`.
- Lost provider response: reusing the identical key and payload yields the same logical operation.
- PostgreSQL conflict or serialization error during claim/apply: controlled retryable result, never HTTP 500.

---

### Task 1: Per-order local gate

**Files:**
- Modify `Orofoods.Web/Services/Payments/PointPaymentAttemptGate.cs`
- Create `Orofoods.Web.Tests/Services/PointPaymentAttemptGateTests.cs`

**Produces:** `AcquireAsync(int orderId, CancellationToken)` blocks concurrent holders for the same order, allows different order IDs concurrently, and releases unused entries safely after cancellation/disposal.

- [x] Test same-order mutual exclusion and different-order parallel acquisition.
- [x] Run the focused gate tests and observe the current global gate fail the different-order case.
- [x] Implement keyed, reference-counted semaphore entries; verify cancellation/disposal releases references.
- [x] Run focused gate tests.

### Task 2: Cross-instance database lock and controlled conflicts

**Files:**
- Create `IPointPaymentOrderConcurrencyLock.cs` and `PointPaymentOrderConcurrencyLock.cs`
- Modify `PointPaymentOrchestrationService.cs`, `Program.cs`, `MercadoPagoWebhooksController.cs`
- Modify `PointPaymentOrchestrationServiceTests.cs`

**Produces:** On PostgreSQL, `AcquireAsync(orderId)` requires an active transaction and locks the matching existing `Orders` row with `FOR UPDATE`; this component uses `ReadCommitted`. SQLite uses a test-only no-op DB lock and `Serializable` isolation. Any other provider fails closed until it has a lock implementation.

- [x] Test that claim/apply request the lock by `OrderId`, that an injected PostgreSQL serialization conflict returns a controlled service result, and that no provider call occurs before claim commit.
- [x] Implement row-lock acquisition before reading payment attempts in both claim and status-apply transactions; keep network calls outside the transactions.
- [x] Replace the global gate in both claim/apply paths with keyed acquisition.
- [x] Map retryable Point reconciliation conflicts to HTTP 503 for webhook retry.
- [x] Run focused claim/reconciliation tests.

### Task 3: Same-order, recovery and independent-order coverage

**Files:**
- Modify `Orofoods.Web.Tests/Services/PointPaymentOrchestrationServiceTests.cs`
- Modify `Orofoods.Web.Tests/Controllers/MercadoPagoWebhooksControllerTests.cs`
- Modify `Orofoods.Web.Tests/Integration/MercadoPagoWebhookHttpHarnessTests.cs` only if needed.

**Produces:** Fake-backed proof for 2/10 simultaneous same-order requests, independent orders, persistence/retry after timeout and lost response, service recreation, webhook arriving before create result persistence, duplicate/out-of-order webhook, and no more than one logical external order for each stored idempotency key.

- [x] Add fake-backed tests for same-order concurrency, independent orders, lost response/restart, and webhook-before-create recovery.
- [x] Run focused tests and verify new scenarios.
- [x] Run focused tests and complete Release restore/build/full suite.
- [x] Run `git diff --check`; confirm no migration was created by this task, both payment feature flags remain false, and external calls remain zero.

## Final report

Report lock scope, whether the singleton remains, the PostgreSQL-backed guarantee, migration requirement, crash windows A-E, test results, `HTTP 500 predictable conflicts`, and external-call count. Do not commit, push, deploy, or run migrations.
