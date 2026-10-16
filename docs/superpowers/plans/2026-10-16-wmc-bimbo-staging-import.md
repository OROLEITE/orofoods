# WMC BIMBO Staging Import Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prepare a Staging-only, audited, idempotent import for the ten approved BIMBO products without running it or enabling sales/export.

**Architecture:** Add a focused `WmcBimboStagingImportService` instead of extending global `WmcSyncService`. The service validates the exact WMC batch before writes, then inserts missing products and direct package stock in a serializable transaction; an Admin-only, anti-forgery POST action is forbidden outside Staging. A persistent run-audit entity records outcomes.

**Tech Stack:** ASP.NET Core MVC, EF Core, PostgreSQL and SQLite migration tracks, xUnit, existing read-only Firebird product reader.

**Spec:** `docs/superpowers/specs/2026-10-16-wmc-bimbo-staging-import-design.md`

## Global Constraints

- WMC brand is `104`; the only accepted codes are `503768`, `503775`, `503777`, `503805`, `503811`, `503813`, `503833`, `503838`, `504338`, and `504342`.
- Orofoods sales/display unit is `PCT`; the provisional price is `1.00` per PCT.
- Copy `ESTOQUEATUAL` directly to `ProductInventory.QuantityOnHand`; do not convert stock.
- New products stay `IsActive = false`, `IsAvailable = false`, and `WmcInitialLoadReady = false`.
- Do not infer EAN-13, DUN-14, logistics-unit, or factor mappings; do not change TXT/export behavior.
- Do not import products, apply migrations, contact production, push, merge, or deploy.

## Review Focus

- Wrong/missing/duplicate WMC rows must reject the batch before product writes (Task 2).
- Fractional, negative, or absent stock must reject rather than be silently rounded or converted (Task 2).
- Existing local products must remain unchanged on replay (Task 2).
- Concurrent requests must not create duplicate normalized WMC codes (Task 2).
- Non-Staging requests must be forbidden before invoking the importer; unauthorized users remain blocked by the existing Admin role policy (Task 3).

---

### Task 1: Persist WMC product-import audit runs

**Files:**
- Create: `Orofoods.Web/Models/Integrations/WmcProductImportRun.cs`
- Modify: `Orofoods.Web/Data/ApplicationDbContext.cs`
- Modify: SQLite and PostgreSQL model snapshots
- Create: matching SQLite and PostgreSQL migration files
- Test: `Orofoods.Web.Tests/Data/WmcProductImportMigrationTests.cs`

**Interface:** `WmcProductImportRun` stores run ID, brand code, actor user ID, start/completion timestamps, outcome, imported WMC codes, skipped WMC codes, and error details. Store code/error collections in bounded text fields without adding a JSON dependency.

- [ ] Write migration contract tests asserting the audit table/columns exist in both migration tracks and match the model.
- [ ] Run `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --no-restore -m:1 --filter FullyQualifiedName~WmcProductImportMigrationTests`; verify failure because the audit entity/migrations are absent.
- [ ] Add the audit model, EF mapping, snapshots, and paired migrations; do not apply either migration.
- [ ] Rerun the focused migration tests and verify they pass.

### Task 2: Implement the fixed-scope transactional importer

**Files:**
- Create: `Orofoods.Web/Integrations/Erp/Wmc/WmcBimboStagingImportService.cs`
- Test: `Orofoods.Web.Tests/Services/WmcBimboStagingImportServiceTests.cs`

**Interface:** `ImportAsync(string? actorUserId, CancellationToken cancellationToken = default)` returns a result containing imported/skipped codes, outcome, and safe error details.

- [ ] Write tests for the approved ten-code scope, active brand 104 validation, direct stock copy, `BasePrice = 1.00`, PCT unit, hidden state, `CODBARRASFAB` mapping to EAN-13 with GS1 check-digit validation, and unset unconfirmed DUN-14/factor fields.
- [ ] Add tests proving invalid/missing/duplicate rows cause no product/inventory writes; existing products are skipped without changed fields; replay is idempotent; transaction failure creates no partial catalog writes.
- [ ] Run only `WmcBimboStagingImportServiceTests`; verify expected failures because the service is absent.
- [ ] Implement validation against `IWmcProductReader.GetAllByBrandAsync(104)`, including valid EAN-13 values, then create only missing products/inventory in one serializable transaction. Use the inactive pending category and the existing normalized WMC-code uniqueness guard. Record success/skip/error audit data.
- [ ] Rerun `WmcBimboStagingImportServiceTests`; verify all cases pass and that the test reader records only read calls.

### Task 3: Add the Staging-only Admin action

**Files:**
- Modify: `Orofoods.Web/Areas/Admin/Controllers/IntegrationsController.cs`
- Modify: `Orofoods.Web/Program.cs`
- Test: `Orofoods.Web.Tests/Controllers/WmcBimboImportControllerTests.cs`

**Interface:** Add `POST WmcBimboImport`; require the controller's existing `Administrador` role, `[ValidateAntiForgeryToken]`, and `IHostEnvironment.IsStaging`. Return `Forbid` outside Staging before calling the service; on Staging, return a summarized outcome without exposing secrets or raw exceptions.

- [ ] Write controller tests that assert non-Staging returns `Forbid` and the importer is not called; Staging invokes it once and returns the expected summary.
- [ ] Run only `WmcBimboImportControllerTests`; verify expected failures because the action is absent.
- [ ] Register the service in DI and implement the guarded action.
- [ ] Rerun `WmcBimboImportControllerTests`; verify authorization/environment behavior and anti-forgery metadata.

### Task 4: Verify migrations and the completed change

**Files:** No additional source files.

- [ ] Run `git diff --check` and inspect the diff to confirm no WMC SQL write, TXT/export change, or unrelated commercial change was added by the import implementation.
- [ ] Run `dotnet build Orofoods.slnx --configuration Release --no-restore -m:1`.
- [ ] Run focused tests for `WmcBimboStagingImportServiceTests`, `WmcBimboImportControllerTests`, `WmcProductImportMigrationTests`, `WmcSyncServiceTests`, and `ProductCommercialUnitTests` in Release.
- [ ] Generate the PostgreSQL migration SQL with the cached `dotnet-ef` tool and inspect it; do not apply it or connect to a database.
- [ ] Report the final branch/worktree, migration status, test/build evidence, unmapped WMC fields, and that no import ran.
