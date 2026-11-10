# WMC Automatic Activation Marker Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Guarantee that the automatic WMC JSON worker can process only orders created after a configured Staging high-water mark.

**Architecture:** Add an optional exclusive `Order.Id` cutoff to `WmcJsonOrderOptions`. Fail closed without a nonnegative cutoff; filter both worker candidates and the atomic order claim, and guard the automatic JSON transmitter before it writes an attempt audit. Keep current JSON reconciliation and duplicate protections.

**Tech Stack:** .NET 10, ASP.NET Core hosted services, EF Core, xUnit, SQLite test fixtures.

**Spec:** `docs/superpowers/specs/2026-10-28-wmc-automatic-activation-marker-design.md`

## Global Constraints

- `AutomaticWorkerEnabled` remains `false` by default.
- The Staging cutoff is the read-only `MAX(Order.Id)` captured immediately before activation.
- Orders with `Id <= cutoff` must not be updated, audited, or transmitted by the automatic worker.
- Only approved, pending orders above the cutoff are eligible.
- Preserve existing atomic claims, TXT/audit exclusions, idempotency, HTTP response audit, and `nropedido.php` reconciliation.
- TXT remains manual; do not register or enable the FileDrop retry worker.
- Do not alter Azure, databases, orders, migrations, or send real WMC requests in this implementation.

## Review Focus

- Missing/negative cutoff with both feature flags enabled: no scope, audit, or transmission.
- Historical order at the cutoff: excluded by both candidate query and atomic claim.
- New approved pending order: only the JSON export and reconciliation calls occur.
- TXT artifact, uncertain attempt, confirmation, or non-approved order: existing exclusion remains effective.
- Direct automatic transmitter call: reject missing/old cutoff before recording `Started`.

---

### Task 1: Add fail-closed activation configuration and worker selection tests

**Files:** `Orofoods.Web/Integrations/Erp/Wmc/WmcJsonOrderOptions.cs`, `Orofoods.Web/Integrations/Erp/Wmc/WmcJsonOrderAutomaticBackgroundService.cs`, `Orofoods.Web.Tests/Integrations/WmcJsonOrderAutomaticBackgroundServiceTests.cs`, `Orofoods.Web/appsettings.json`.

- [ ] Add `AutomaticWorkerStartAfterOrderId` as nullable integer configuration; add a worker test proving missing/negative values create no scope even when both enable flags are true.
- [ ] Run that focused test and confirm it fails because the worker currently processes without a cutoff.
- [ ] Add fail-closed validation before scope creation and filter candidate IDs with `Id > cutoff`.
- [ ] Extend the SQLite worker test with a historical approved/pending order at the cutoff and one new approved/pending order above it; verify only the new order is sent and the historic order remains unchanged.
- [ ] Add a default-null setting to `appsettings.json`; keep `AutomaticWorkerEnabled=false`.
- [ ] Run the focused background-service tests.

### Task 2: Enforce the cutoff at atomic claim and JSON transmission

**Files:** `Orofoods.Web/Services/Orders/OrderIntegrationService.cs`, `Orofoods.Web/Integrations/Erp/Wmc/WmcJsonOrderErpIntegration.cs`, `Orofoods.Web/Integrations/Erp/Wmc/WmcAutomaticOrderIntegration.cs`, `Orofoods.Web.Tests/Services/OrderIntegrationServiceTests.cs`, `Orofoods.Web.Tests/Integrations/WmcJsonOrderErpIntegrationTests.cs`, `Orofoods.Web.Tests/Integrations/WmcAutomaticOrderIntegrationTests.cs`.

- [ ] Add tests that direct atomic claims and direct automatic-transmitter calls reject IDs at/below the cutoff and do not begin a WMC audit.
- [ ] Run the focused tests and confirm they fail against the current implementation.
- [ ] Pass the cutoff from the configured worker to `SendAutomaticJsonAsync` and include `Id > cutoff` in its serializable atomic claim.
- [ ] Guard the JSON automatic-worker entry point before creating an audit; reject a missing, negative, or already-reached cutoff.
- [ ] Preserve the existing status, idempotency, TXT artifact, transmission uncertainty, and reconciliation logic; run the focused WMC tests.

### Task 3: Document controlled activation and verify the branch

**Files:** `docs/integrations/wmc-json-order-export.md`, `docs/superpowers/specs/2026-10-28-wmc-automatic-activation-marker-design.md`, `docs/superpowers/plans/2026-10-28-wmc-automatic-activation-marker.md`.

- [ ] Document the setting name, fail-closed behavior, and read-only Staging procedure: query `MAX(Id)`, set that value as the exclusive cutoff, then enable the two JSON settings after merge/deploy.
- [ ] State that the cutoff leaves all older IDs untouched and that TXT remains a manual action.
- [ ] Run focused JSON-worker/status tests, the full Release test suite, Release build, and `git diff --check`.
- [ ] Verify no migration, Azure/config change, order mutation, real WMC transmission, or worker activation occurred.
- [ ] Commit the reviewed changes and push this branch without force; create or update the PR if GitHub permissions allow.
