# WMC Generic Official Units Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the manual WMC product importer accept valid official units from `PRODUTOS` and `PRODUTOS_UNCONV` for every brand without requiring PC/CX.

**Architecture:** Add a manual-only unit validator and manual readiness evaluator. Keep the shared automatic readiness evaluator, BIMBO staging rules, order unit resolver, price contract, and local commercial fields unchanged. Adapt the import proposal model and preview to describe primary/master units generically.

**Tech Stack:** C#/.NET 10, ASP.NET Core MVC/Razor, EF Core, xUnit, existing in-memory test infrastructure.

**Spec:** `docs/superpowers/specs/2026-11-25-wmc-generic-official-units-design.md`

## Global Constraints

- Accept official WMC units for all brands without requiring PC/CX.
- Preserve `WmcSync__Enabled=false` and do not alter automatic synchronization.
- Do not change prices except direct `PRECOVENDA` application under the existing price contract.
- Preserve local commercial fields, physical stock, orders, transaction, rollback, idempotency, and explicit reimport confirmation.
- Reject inactive products before any manual import write, including direct/manual POST selection.
- Do not execute real imports, migrations, database changes, deploys, push, PR, or merge.

## Review Focus

- AVIAÇÃO with `UN` primary and `CX` master must be eligible without PC; test in `WmcProductImportServiceTests`.
- A valid non-PC master and `PT`/`CP` alternatives must reconcile exact factors and barcodes; test in `WmcProductImportServiceTests`.
- Missing/duplicate master, invalid factor, invalid supplied GTIN, and mismatched `PRODUTOS.UN` must block without replacing local unit data; test validator and service.
- `QTDE_EMB` and `QTDE_CONV` must persist independently without price or commercial conversion; test service.
- Automatic PC/CX readiness and BIMBO staging/order rules must remain unchanged; retain and run existing evaluator, BIMBO, and order tests.

### Task 1: Record design and create generic validation contracts

**Files:**
- Create: `docs/superpowers/specs/2026-11-25-wmc-generic-official-units-design.md`
- Create: `docs/superpowers/plans/2026-11-25-wmc-generic-official-units.md`
- Create: `Orofoods.Web/Integrations/Erp/Wmc/WmcManualProductUnitValidator.cs`
- Create: `Orofoods.Web/Integrations/Erp/Wmc/WmcManualProductImportReadinessEvaluator.cs`
- Modify: `Orofoods.Web/Models/Integrations/WmcProductPreview.cs`
- Test: `Orofoods.Web.Tests/Services/WmcManualProductUnitValidatorTests.cs`
- Test: `Orofoods.Web.Tests/Services/WmcManualProductImportReadinessEvaluatorTests.cs`

**Interfaces:**
- Produces `WmcManualProductUnitValidation.Validate(WmcProductRecord, IReadOnlyCollection<WmcProductUnitRecord>, bool)` with normalized primary/master resolution and explicit reasons.
- Produces `WmcManualProductImportReadinessEvaluator.Evaluate(WmcProductRecord, Product, IReadOnlyCollection<WmcProductUnitRecord>, bool, int, bool)`.
- Expands `WmcProductUnitImportProposal` with generic primary/master factor and WMC quantity fields while retaining compatibility aliases for existing PC/CX assertions.

- [ ] Write failing tests for AVIAÇÃO `UN/CX`, PT/CP alternatives, missing PC acceptance, duplicate master, invalid factor/GTIN, and manual readiness.
- [ ] Run focused tests and verify they fail because the generic validator/evaluator and proposal fields do not exist.
- [ ] Implement the minimal validator, manual readiness evaluator, and proposal model contracts.
- [ ] Run focused tests and verify they pass.

### Task 2: Use generic validation in the manual import service

**Files:**
- Modify: `Orofoods.Web/Integrations/Erp/Wmc/WmcProductImportService.cs:60-620`
- Test: `Orofoods.Web.Tests/Services/WmcProductImportServiceTests.cs`

**Interfaces:**
- Consumes the validator and manual readiness interfaces from Task 1.
- Produces generic preview reasons, generic field changes, official primary/master identifiers, exact WMC quantities, and safe unit reconciliation.

- [ ] Add failing service tests for AVIAÇÃO, BIMBO, PT/CP, quantities, direct inactive selection, malformed unit data, and preservation of local commercial fields.
- [ ] Run the focused service tests and verify the new cases fail with PC/CX blocking or dropped quantities.
- [ ] Replace PC/CX-only proposal construction and barcode checks with the manual validator result.
- [ ] Apply `Product.Unit`, `WmcAlternateUnit`, `WmcPackageQuantity`, and `WmcConversionQuantity` only from a valid official snapshot; never write commercial PCT/CX fields.
- [ ] Use the manual readiness evaluator while leaving `WmcSyncService` unchanged.
- [ ] Update generic change details and preserve price, inactive, transaction, reimport, and rollback paths.
- [ ] Run focused service and readiness tests and verify all pass.

### Task 3: Adapt the preview and view assertions

**Files:**
- Modify: `Orofoods.Web/Areas/Admin/Views/Integrations/Wmc.cshtml:450-570`
- Modify: `Orofoods.Web.Tests/Views/WmcProductPreviewViewTests.cs`

- [ ] Add failing assertions for generic primary/master labels and `QTDE_EMB`/`QTDE_CONV` display.
- [ ] Run the view tests and verify the old PC/CX-only labels fail the new assertions.
- [ ] Render generic unit/master summary and proposal details without changing filtering, pagination, selection, confirmation, or multiple-brand behavior.
- [ ] Run view-focused tests and verify they pass.

### Task 4: Release verification and scope audit

**Files:**
- No production or infrastructure files.

- [ ] Run `dotnet build Orofoods.slnx --no-restore --configuration Release` using the temporary package cache parameter when needed.
- [ ] Run focused WMC tests in Release.
- [ ] Run the complete `Orofoods.Web.Tests` suite in Release and record pass/skip counts.
- [ ] Verify `WmcSync__Enabled=false`, no migrations were added, no database or deploy command was executed, and automatic/BIMBO/order files are unchanged.
- [ ] Inspect `git diff`, changed-file list, and final commit SHA.
