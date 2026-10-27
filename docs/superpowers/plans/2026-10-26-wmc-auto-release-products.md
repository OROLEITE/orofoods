# WMC Automatic Product Release Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (or superpowers:subagent-driven-development) to implement this plan task-by-task.

**Goal:** Mark only correctly imported WMC products as `WmcInitialLoadReady` while preserving commercial data and avoiding mass updates.

**Architecture:** A pure evaluator consumes the current WMC row, the persisted product, its unit snapshot, and available stock. `WmcProductImportService` and `WmcSyncService` call it only for products processed in their current operation; existing manual BIMBO release remains separate.

**Tech Stack:** .NET/C#, EF Core, xUnit, SQLite test infrastructure.

**Spec:** `docs/superpowers/specs/2026-10-26-wmc-auto-release-products.md`

## Global Constraints

- Do not execute imports, migrations, Azure changes, or WMC transmissions.
- Do not modify EAN, DUN, prices, quantities, or unit-conversion data as part of readiness evaluation.
- Preserve intentional inactive/blocked products and existing manual release/audit paths.
- Do not mass-update products that are absent from the current source batch.

## Review Focus

- Active product with positive price, valid units, and stock at minimum becomes ready; test in both import and sync flows.
- Inactive/unknown WMC situation, invalid price, incomplete units, invalid packaging, or insufficient stock stays blocked; tests cover each reason.
- Existing products not included in the current batch keep their readiness value; regression test prevents mass release.
- Existing commercial fields remain unchanged by the readiness decision; assert price, identifiers, quantities, and units.
- Audit records continue to be written for successful and failed operations; assert the existing audit tables/results.

### Task 1: Readiness evaluator

**Files:**
- Create: `Orofoods.Web/Integrations/Erp/Wmc/WmcInitialLoadReadinessEvaluator.cs`
- Test: `Orofoods.Web.Tests/Services/WmcInitialLoadReadinessEvaluatorTests.cs`

**Interfaces:**
- Produces `WmcInitialLoadReadinessResult Evaluate(WmcProductRecord source, Product product, IReadOnlyCollection<WmcProductUnitRecord> units, int availableQuantity, bool inventoryManuallyAdjusted = false)`.

- [x] Write failing tests for valid data, inactive/unknown situation, non-positive price, incomplete/invalid units, invalid packaging, and stock below `MinimumSalesQuantity`.
- [x] Run the focused evaluator tests and verify failure before implementation.
- [x] Implement a side-effect-free evaluator using existing `WmcProductRules`, unit validation, and `Product` commercial-unit semantics; return `IsReady` plus a stable reason code/message.
- [x] Run the focused evaluator tests and verify they pass.

### Task 2: Product import integration

**Files:**
- Modify: `Orofoods.Web/Integrations/Erp/Wmc/WmcProductImportService.cs`
- Test: `Orofoods.Web.Tests/Services/WmcProductImportServiceTests.cs`

**Interfaces:**
- Consumes the evaluator from Task 1.

- [x] Add tests proving a valid newly imported/reimported product becomes ready, invalid source remains false, and an unselected existing product is untouched.
- [x] Run the focused import tests and verify failure before implementation.
- [x] Invoke the evaluator after mapped fields, units, and price are applied, using current inventory and current product state; set readiness only for the selected row and only when the evaluator returns ready.
- [x] Preserve existing audit/result behavior and add readiness reason to the existing item detail without a schema change.
- [x] Run the focused import tests and verify they pass.

### Task 3: Full synchronization integration

**Files:**
- Modify: `Orofoods.Web/Integrations/Erp/Wmc/WmcSyncService.cs`
- Test: `Orofoods.Web.Tests/Services/WmcSyncServiceTests.cs`

**Interfaces:**
- Consumes the evaluator from Task 1.

- [x] Add tests proving `SyncAllAsync` releases only active/valid/stocked products in the current WMC batch and keeps invalid and absent products blocked.
- [x] Run the focused sync tests and verify failure before implementation.
- [x] Replace the unconditional `foreach (var product in wmcProducts) product.WmcInitialLoadReady = true` with per-row evaluation after product and stock stages, restricted to source codes in that run.
- [x] Keep existing sync run/audit persistence and commercial field updates unchanged.
- [x] Run focused sync tests and the existing WMC service regression suite.

### Task 4: Verification

- [x] Run Release build for `Orofoods.slnx`.
- [x] Run focused evaluator/import/sync tests in Release.
- [x] Run `git diff --check` and inspect the complete diff for unrelated changes.
- [x] Report that no import, migration, Azure change, or WMC transmission was executed.
