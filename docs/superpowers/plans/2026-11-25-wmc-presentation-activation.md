# WMC confirmation presentation and controlled active-product import Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make new manually imported active WMC products active in the Orofoods cadastro while adding a read-only, source-separated confirmation presentation for WMC data, local inventory, activation flags, and commercial blockers.

**Architecture:** Keep the import service responsible for the approved new-product `IsActive` assignment and existing-product preservation. Add a separate `WmcProductConfirmationPresentationService` and display-only records that compose the already selected candidates with a fresh read-only WMC stock snapshot and `AsNoTracking` local product/inventory state. Feed the optional presentation collection into the existing WMC page without changing POST field names, antiforgery, selection keys, or reimport authorization.

**Tech Stack:** ASP.NET Core MVC/Razor, C# records, EF Core `AsNoTracking`, Firebird read-only `IWmcProductReader`, xUnit, existing WMC CSS variables and responsive media queries.

**Spec:** `docs/superpowers/specs/2026-11-25-wmc-presentation-diagnostics-design.md`

**Related but excluded design:** `docs/superpowers/specs/2026-11-25-admin-navigation-state-design.md`. The approved conventional navigation design is not implemented in this worktree.

## Global Constraints

- New manual imports with WMC `SITUACAO = 'A'` set `Product.IsActive=true`.
- `Product.IsAvailable` must not be set to `true` automatically.
- Existing products preserve their local `IsActive` value; no mass activation/deactivation.
- `Product.IsWmcActive` continues to follow the official WMC situation in the manual flow.
- The already imported product `101900` receives no direct or retroactive update.
- No `ProductInventory` row is created or changed by this work.
- Do not change `WmcSyncService`, automatic stock synchronization, pricing/unit rules, inventory quantities/reservations, BIMBO Staging, order integration, or PCT/CX export.
- Presentation reads use `AsNoTracking` and never call persistence or activation APIs.
- Administrative navigation remains conventional MVC/Razor; no internal tab workspace or menu interception is implemented.
- Missing WMC/local data is displayed as unavailable/not informed, never as an invented zero or unit conversion.
- Preserve POST contracts, antiforgery, selected product keys, reimport authorization, dark mode, compact layout, responsive behavior, and ciano buttons.
- Do not execute real imports, migrations, database writes, push, PR, merge, or deploy.
- Preserve `WmcSync__Enabled=false`.

## Review Focus

- Active new product with zero/missing WMC stock: becomes active in cadastro, remains commercially unavailable, and readiness remains blocked; covered by Task 1 import tests.
- Existing locally inactive product with active WMC situation: `IsActive` remains unchanged while `IsWmcActive` follows WMC; covered by Task 1 regression tests.
- Missing local inventory row or manual adjustment: presentation shows “não informado”/manual protection and never writes; covered by Task 2 presentation tests.
- WMC/local read failure or partial match: cards render explicit unavailable markers without inventing values or changing confirmation POST behavior; covered by Task 2 and Task 3 tests.
- Inactive/invalid WMC selection and duplicate candidate keys: existing selection validation and inactive-product protection remain unchanged; covered by existing controller/security tests and Task 3 regression assertions.

---

### Task 1: Apply the approved new-product activation rule

**Files:**
- Modify: `Orofoods.Web/Integrations/Erp/Wmc/WmcProductImportService.cs` in the new-product construction path only.
- Test: `Orofoods.Web.Tests/Services/WmcProductImportServiceTests.cs`.
- Test: `Orofoods.Web.Tests/Controllers/WmcProductSelectionControllerTests.cs` where the AVIAÇÃO `101900` fixture is already defined.

**Interfaces:**
- Consumes: `WmcProductRules.ClassifySituation`, the existing manual `ImportAsync` flow, and the existing `WmcManualProductImportReadinessEvaluator`.
- Produces: a new product with `IsActive=true` only when the validated WMC row is active; `IsAvailable` remains false; existing products retain their local `IsActive`.

- [ ] **Step 1: Write failing import tests**

  Add tests for:

  - active new AVIAÇÃO `101900` results in `IsActive=true`, `IsAvailable=false`, `IsWmcActive=true`, no automatic `ProductInventory` row, and `WmcInitialLoadReady=false` with the known local-availability blocker;
  - active WMC row mapped onto an existing locally inactive product updates `IsWmcActive` but leaves `IsActive=false` and `IsAvailable` unchanged;
  - inactive WMC input remains rejected before any product activation.

- [ ] **Step 2: Run the focused tests and confirm the new rule fails**

  Run the WMC import and selection tests with `--no-restore --configuration Release`. The new-product activation assertion must fail against the current `IsActive=false` initializer.

- [ ] **Step 3: Implement the smallest import change**

  Determine the situation before constructing a new `Product`; initialize `IsActive` from `WmcProductSituation.Active` and leave `IsAvailable=false`. Do not change the existing-product branch, inventory handling, readiness evaluator, or automatic sync service.

- [ ] **Step 4: Run the focused tests to confirm the rule passes**

  Re-run the tests from Step 2 and the relevant readiness tests. Expected: all pass, including the existing-product preservation assertions.

- [ ] **Step 5: Commit the isolated behavior change**

  Commit only the service and its tests with a message such as `fix(wmc): activate new active products in cadastro`.

### Task 2: Build the read-only presentation model and composer

**Files:**
- Create: `Orofoods.Web/Models/Integrations/WmcProductConfirmationPresentation.cs`.
- Create: `Orofoods.Web/Integrations/Erp/Wmc/WmcProductConfirmationPresentationService.cs`.
- Test: `Orofoods.Web.Tests/Services/WmcProductConfirmationPresentationServiceTests.cs`.

**Interfaces:**
- Consumes: selected `WmcProductImportCandidate` records, `IWmcProductReader.GetAllByBrandAsync`, `ApplicationDbContext`, and local `ProductInventory` rows.
- Produces: `WmcProductConfirmationPresentationService.BuildAsync(IReadOnlyCollection<short> brandCodes, IReadOnlyList<WmcProductImportCandidate> selectedCandidates, CancellationToken)` returning ordered `WmcProductConfirmationPresentation` records.

  The model contains:

  - `WmcProductImportCandidate Candidate`;
  - `WmcProductConfirmationWmcStock` with raw `EstoqueDisponivel`, `EstoqueAtual`, `EstoqueReservado`, source field labels, primary unit when supplied, and explicit unknown unit semantics;
  - `WmcProductConfirmationLocalState` with product flags, local unit labels, `QuantityOnHand`, `QuantityReserved`, derived non-negative `AvailableQuantity`, and `IsManuallyAdjusted`;
  - read-status and known blocker strings for unavailable/partial data.

- [ ] **Step 1: Write failing presentation tests**

  Use the existing test database and fake WMC reader to cover:

  - new AVIAÇÃO `101900` with active WMC status, WMC quantities, no local product, and no fabricated local values;
  - existing product with local `QuantityOnHand=0`, reservations, inactive/available flags, and manual adjustment;
  - missing inventory row represented as “not informed”, not zero;
  - WMC values displayed with raw field names and no assumed WMC/local unit equivalence;
  - reader/local lookup failure represented as an unavailable presentation state;
  - no tracked entities and no database writes from the composer.

- [ ] **Step 2: Run the presentation tests and confirm they fail**

  Run the new service test class in Release without restore. Expected: compile failure because the presentation records/service do not yet exist.

- [ ] **Step 3: Implement the display-only records**

  Add nullable local state for absent products/inventory. Use explicit source labels (`ESTOQUEDISPONIVEL`, `ESTOQUEATUAL`, `ESTOQUERESERVADO`) and `UnitSemantics = "não confirmada"` unless the source contract provides a unit. Keep official unit/factor/EAN/DUN data sourced from the candidate proposals.

- [ ] **Step 4: Implement the composer with read-only queries**

  Re-query only the selected brand rows through the existing reader, query local products and inventories using `AsNoTracking`, map one or zero local states per selected candidate, derive only `max(onHand-reserved, 0)`, and collect known blockers from candidate block reasons and persisted flags. Catch read failures per source and preserve the candidate with an explicit unavailable marker.

- [ ] **Step 5: Run the presentation tests to confirm they pass**

  Expected: all presentation and no-write assertions pass.

- [ ] **Step 6: Commit the read-only presentation slice**

  Commit only the new model, composer, and tests with a message such as `feat(wmc): add read-only confirmation diagnostics`.

### Task 3: Integrate presentation data without changing POST contracts

**Files:**
- Modify: `Orofoods.Web/Models/Integrations/WmcProductPreview.cs` to add the optional confirmation presentation collection to `WmcPreviewPageModel`.
- Modify: `Orofoods.Web/Areas/Admin/Controllers/IntegrationsController.cs` to compose presentations only for the confirmation GET result.
- Test: `Orofoods.Web.Tests/Controllers/WmcProductSelectionControllerTests.cs`.
- Test: `Orofoods.Web.Tests/Views/WmcProductPreviewViewTests.cs`.

**Interfaces:**
- Consumes: `WmcProductConfirmationPresentationService.BuildAsync` from Task 2.
- Produces: confirmation model data for the Razor view while preserving `WmcImportProducts`, `WmcConfirmSelection`, `selectedProductKeys`, `simulationValidated`, `reimportExisting`, and antiforgery fields.

- [ ] **Step 1: Write failing controller/view contract tests**

  Assert the confirmation model contains one presentation per selected candidate, that selection keys and reimport state are unchanged, and that the controller source still requires antiforgery and the same POST action/field names.

- [ ] **Step 2: Run focused controller/view tests and confirm they fail**

  Expected: missing model property/service integration assertions fail.

- [ ] **Step 3: Integrate the composer in `WmcConfirmSelection`**

  Inject the presentation service, build the optional collection after selection validation, and pass it into `WmcPreviewPageModel`. Keep invalid-selection rendering and the POST import path unchanged.

- [ ] **Step 4: Run focused controller/view tests to confirm they pass**

  Verify selection persistence, inactive blocking, explicit reimport authorization, and antiforgery/POST contracts.

- [ ] **Step 5: Commit the controller/model integration**

  Commit with a message such as `feat(wmc): compose confirmation diagnostics`.

### Task 4: Render compact cards and responsive diagnostics

**Files:**
- Modify: `Orofoods.Web/Areas/Admin/Views/Integrations/Wmc.cshtml` confirmation section only.
- Modify: `Orofoods.Web/wwwroot/css/wmc-integration-center.css` confirmation/card responsive rules only.
- Test: `Orofoods.Web.Tests/Views/WmcProductPreviewViewTests.cs`.

**Interfaces:**
- Consumes: optional confirmation presentations from Task 3 and the existing candidate/summary/reimport model.
- Produces: one compact card per selected product with three WMC/Orofoods/commercial sections, responsive three/two/one-column diagnostics, source-separated stock, known blockers, and preserved authorization controls.

- [ ] **Step 1: Write failing view/CSS tests**

  Assert card headers contain WMC code/description/brand/situation, sections contain official unit/price/factor/EAN/DUN fields, stock origins are distinct, all four flags have labels, unavailable values use the explicit fallback, grid and mobile rules exist, ciano colors remain, and the checkbox/action names remain unchanged.

- [ ] **Step 2: Run the view tests and confirm they fail**

  Expected: new presentation markers and CSS selectors are absent.

- [ ] **Step 3: Implement the compact confirmation markup and CSS**

  Render each card from the display-only model. Keep `reimportExisting` unchecked, the authorization area outside the cards, and all existing hidden selection/antiforgery fields. Use `#0E7490` and `#0B5D75` through the existing WMC variables and collapse the grid at the current mobile breakpoint.

- [ ] **Step 4: Run view tests to confirm they pass**

  Expected: all view/CSS contract tests pass without browser-side writes.

- [ ] **Step 5: Commit the visual slice**

  Commit with a message such as `feat(wmc): present stock and activation diagnostics`.

### Task 5: Full verification and handoff

**Files:**
- No additional source changes unless verification exposes a scoped regression.
- Review: all files changed by Tasks 1–4 and `git diff` against `origin/main`.

- [ ] **Step 1: Run formatting/diff safety checks**

  Run `git diff --check` and verify no `WmcSyncService`, migration, database, order-export, BIMBO, or App Settings files changed.

- [ ] **Step 2: Run focused WMC tests**

  Run the import-service, presentation-service, controller, view, readiness, inventory, and relevant WMC security test classes in Release.

- [ ] **Step 3: Run Release build and complete suite**

    Run `dotnet build Orofoods.slnx --no-restore --configuration Release -m:1` and `dotnet test Orofoods.slnx --no-restore --configuration Release -m:1 --logger "console;verbosity=minimal"`. Serial execution avoids the known shared-project dependency race in the Windows sandbox. Record exact counts and warnings.

- [ ] **Step 4: Verify configuration and worktree**

  Confirm local `WmcSync.Enabled=false`, no real import/migration/database/deploy command was executed, the worktree is clean apart from intentional commits, and capture the final commit SHA.

- [ ] **Step 5: Report risks and 101900 procedure**

  Report that the already imported `101900` was not updated directly. Provide a separate future procedure requiring an explicit read-only data check, a reviewed manual activation action, and rollback evidence before any retroactive change.
