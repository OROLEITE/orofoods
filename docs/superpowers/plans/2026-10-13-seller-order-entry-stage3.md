# Seller Order Submission Stage 3 Implementation Plan

> **Execution:** Implement inline with `superpowers:executing-plans`, using TDD. No commits; the user requested review before commit.

**Goal:** Atomically convert one authorized seller draft into one received Orofoods order and show its number, without checkout, payment collection, or WMC transmission.

**Architecture:** `SellerOrderDraftSubmissionService` owns one serializable transaction, loads the complete persisted draft and revalidates its owner, active portfolio, customer, address, payment, products, quantities, current prices, stock, credit, and zero discount. A strict transaction-aware path in `OrderPlacementService` shares its commercial rules and reserves stock without opening or committing a nested transaction. The unique `Order.SellerOrderDraftId` relationship and draft version/status provide the idempotency and concurrency gates.

**Tech Stack:** ASP.NET Core MVC, EF Core, PostgreSQL, SQLite tests, xUnit.

**Spec:** User request for “ETAPA 3 — Envio Definitivo de Pedidos do Vendedor Orofoods” (2026-10-13); base commit `d08220446c8804102ff20096381aa753efa7d99a`.

## Global Constraints

- Keep the work on a new isolated branch based on `d082204`; preserve prior stages.
- Do not implement WMC transmission, checkout, PIX/card charging, or alter approved commercial rules.
- Do not run migrations against Staging or production; no push, merge, deploy, or commit.
- Recompute prices and validate all persisted lines server-side; requested discount must equal zero.
- Any invalid line rejects the whole submission and leaves the draft editable.
- `Order`, history, reservation, draft link, and `Submitted` transition commit or roll back together.

## Review Focus

- A malformed or stale line must not be silently dropped, merged, or raised to minimum quantity; test the whole draft remains unsubmitted.
- Simultaneous sends must not double-create orders, reservations, history, or credit usage; test using PostgreSQL 17 disposable.
- A changed owner/portfolio, address, product availability, payment eligibility, stock, or credit must block the full send; a changed price must resolve from the current server-side table and become the persisted order price.
- A failure after inserting the order or reserving inventory must roll back every write and preserve editability.
- `CARD_ON_DELIVERY` must remain local pending bookkeeping only, if retained; assert no payment gateway/PIX/card charge or WMC call.

---

### Task 1: Transaction-aware strict order placement

**Files:**
- Modify `Orofoods.Web/Services/Orders/OrderPlacementService.cs`
- Test `Orofoods.Web.Tests/Services/OrderPlacementServiceTests.cs`

**Interface:** Add a draft-specific method that requires `Database.CurrentTransaction`, receives the draft ID and complete persisted `(ProductId, Quantity)` set, and returns `OrderPlacementResult` without committing. Existing `PlaceAsync` and `PlaceLinesAsync` retain their transaction and cart behavior.

- [x] Write tests first for strict line validation (no invalid-line filtering, duplicate coalescing, or minimum-quantity adjustment) and draft link assignment.
- [x] Run focused tests and observe the expected failure before implementation.
- [x] Extract shared commercial validation/order construction so both existing placement and the new strict path reuse eligibility, current-price resolution, availability, credit, history, and reservation rules.
- [x] Run focused tests and existing `OrderPlacementServiceTests`; verify existing flows remain unchanged.

### Task 2: Atomic draft submission service

**Files:**
- Create `Orofoods.Web/Services/Sellers/SellerOrderDraftSubmissionService.cs`
- Add unit/service tests under `Orofoods.Web.Tests/Services/`
- Register service in `Orofoods.Web/Program.cs` if registration is not automatic

**Interface:** `SubmitAsync(ClaimsPrincipal user, int draftId, int expectedVersion, CancellationToken)` returns a result containing either the submitted draft/order or a typed validation/concurrency error.

- [x] Add tests for successful full submission, owner/portfolio enforcement, zero-discount enforcement, changed address/payment/product/stock/credit, and a price change resolved from the current server-side table rather than browser input.
- [x] Add tests proving rollback leaves no order/history/reservation/credit changes and the draft editable when placement fails.
- [x] Implement one serializable transaction: reload tracked draft+all items, authorize owner and active portfolio, compare status/version, revalidate persisted references and discount, call Task 1's transaction-aware placement path, link order, set `Submitted`/UTC timestamp, increment version, then commit once.
- [x] On repeat, return the already linked order; on concurrency/unique conflict, reload committed state and return that order only when the draft is actually Submitted, otherwise return a clear conflict.
- [x] Run focused service tests and existing draft/placement tests.

### Task 3: POST route, read-only submitted view, and confirmation

**Files:**
- Modify `Orofoods.Web/Areas/Vendedor/Controllers/OrderDraftsController.cs`
- Modify `Orofoods.Web/Areas/Vendedor/Views/OrderDrafts/Editor.cshtml`
- Add/update controller and view contract tests under `Orofoods.Web.Tests/`

- [x] Add contract tests for antiforgery, linked-representative authorization, route, expected-version form field, clear validation/conflict feedback, and order-number confirmation.
- [x] Add `[HttpPost("/Vendedor/Rascunhos/{id:int}/Enviar")]` with `[ValidateAntiForgeryToken]`; submit only draft ID/version, not browser-supplied items or prices.
- [x] Enable the action only for editable `Draft`; render `Submitted` as read-only and link to the existing seller order detail route with the number.
- [x] Run focused controller/view tests and verify no payment orchestration or WMC service is called.

### Task 4: PostgreSQL concurrency and full regression gate

**Files:**
- Add `Orofoods.Web.Tests/Services/SellerOrderDraftSubmissionPostgresIntegrationTests.cs` using a dedicated `OROFOODS_TEST_SELLER_POSTGRES` connection setting.
- Modify migrations only if inspection proves the existing unique draft/order relationship is missing; do not add speculative schema changes.

- [x] Add PostgreSQL integration coverage for two independent contexts submitting the same draft concurrently; sequential replay and rollback are covered by service tests.
- [x] Run it against a new loopback-only PostgreSQL 17 container with no volume and a unique container name; never connect to existing containers/databases.
- [x] Assert exactly one order/link/history/reservation and correct inventory totals under the concurrent send.
- [x] Run `dotnet build Orofoods.slnx -c Release`, focused seller/order tests, full `dotnet test Orofoods.slnx -c Release`, and `git diff --check`.
- [x] Remove only the disposable container created for this test; the Stage 2 checkout remained at its base commit and the unrelated root checkout was not modified.

**Completion boundary:** Leave all Stage 3 implementation and plan changes uncommitted for review. Report any skipped PostgreSQL test or environmental limitation explicitly.
