# WMC confirmation presentation and diagnostic design

**Status:** Approved for implementation
**Base:** `origin/main` at `08e532cd031aa1fc5fdd7a1d373ce61973c7b883`
**Worktree:** `C:\Dev\Orofoods\.worktrees\review-wmc-import-stock-ui`

## Intent

Improve the manual WMC product-confirmation screen so an administrator can distinguish official WMC data, the current Orofoods cadastro, and commercial availability before confirming an import. The presentation must explain the AVIAÇÃO `101900` situation while applying the approved activation rule for newly created active WMC products, without changing stock, pricing, order, or synchronization behavior.

## Findings that shape the design

- `WmcProductReader` reads `ESTOQUEDISPONIVEL`, `ESTOQUEATUAL`, and `ESTOQUERESERVADO`, but `WmcProductRecord` has no confirmed stock-unit field. The UI must show the raw WMC quantities with their field names and explicitly state that their unit equivalence to the local inventory is not confirmed.
- Manual `WmcProductImportService.ImportAsync` creates a new product with `IsActive=false` and `IsAvailable=false`, applies `IsWmcActive` from `SITUACAO`, and does not create or update `ProductInventory`. Its readiness evaluator therefore reports `local-product-disabled` for a newly imported product until a separate commercial release occurs.
- Automatic `WmcSyncService.SyncStockAsync` is a separate path that may copy normalized `ESTOQUEATUAL` into `ProductInventory.QuantityOnHand` when `IsManuallyAdjusted` is false. This design does not change that path.
- Local available stock is `max(QuantityOnHand - QuantityReserved, 0)`. A manual inventory adjustment must remain visible as a protection state, not be overwritten or inferred as a WMC quantity.
- The current confirmation already has official unit/price proposals and reimport authorization. The new presentation model will consume those values without changing the existing POST fields, antiforgery token, selection keys, or authorization checkbox.
- By the approved commercial decision, a new manually imported product with `SITUACAO = 'A'` receives `IsActive=true`. This is cadastro activation only; `IsAvailable` remains unchanged/false unless an existing commercial flow explicitly changes it.
- Existing products keep their local `IsActive` value in this change. The manual flow continues to synchronize `IsWmcActive` from the WMC situation, but it does not mass-activate or mass-deactivate existing Orofoods products.
- The already imported AVIAÇÃO `101900` is not updated directly and receives no retroactive activation in this work.

Live Firebird/PostgreSQL values for product `101900` are not inferred from source code. A read-only authorized data query is required to prove the production quantities; no database connection is part of this implementation.

## Goals

- Render one compact product card per selected candidate in the confirmation step, with three diagnostic sections inside each card.
- Group each card into WMC official data, Orofoods cadastro, and commercial availability; the sections use three columns on wide screens, two at the intermediate breakpoint, and one on mobile.
- Show official price, primary/master units, factors, EAN, DUN, `QTDE_EMB`, `QTDE_CONV`, and alternative units already available to the confirmation snapshot.
- Show WMC `ESTOQUEDISPONIVEL`, `ESTOQUEATUAL`, and `ESTOQUERESERVADO` separately from local `QuantityOnHand`, `QuantityReserved`, and derived available quantity.
- Label each stock origin and avoid claiming a unit conversion that the source does not provide.
- Expose `IsWmcActive`, `IsActive`, `IsAvailable`, `WmcInitialLoadReady`, `WmcStockAvailable`, and `IsManuallyAdjusted` with operator-facing labels and known blocker reasons.
- Preserve compact dark-mode styling, responsive behavior, ciano primary buttons, and the existing authorization area.

## Non-goals and anti-goals

- Do not change `WmcSyncService`, `WmcInitialLoadReadinessEvaluator`, pricing rules, unit validation, inventory quantities, reservations, order integration, BIMBO Staging, or PCT/CX export behavior.
- The one authorized import exception is setting `IsActive=true` when the manual import creates a new product whose WMC situation is active. Do not set `IsAvailable=true` as part of that operation.
- Do not release initial load, copy WMC stock, convert units, or alter availability as a side effect of rendering the page.
- Do not change `WmcImportProducts`, `WmcConfirmSelection`, antiforgery, selected keys, `reimportExisting`, or any POST contract.
- Do not execute real imports, migrations, database writes, push, PR, merge, or deploy.

## Administrative navigation boundary

The administrative-navigation alternatives are documented separately in `docs/superpowers/specs/2026-11-25-admin-navigation-state-design.md`. The approved alternative is conventional MVC/Razor navigation with an explicit same-tab/new-browser-tab choice and safe, opt-in recovery of non-sensitive state. No structural navigation, tab workspace, menu interception, or draft-recovery code is part of this WMC worktree.

## Approaches considered

### Approach 1 — Extend the import candidate and render directly in Razor

Add local stock and state fields to `WmcProductImportCandidate` and keep the confirmation rendering in `Wmc.cshtml`. This is the smallest diff, but it couples a read-only presentation concern to the import-service candidate contract and makes it easier for future import logic to depend on display-only state.

### Approach 2 — Separate read-only presentation model and composer (selected)

Keep `WmcProductImportCandidate` and the import service focused on selection/import semantics. Add a `WmcProductConfirmationPresentation` model plus a presentation composer that reads WMC quantities and local products/inventory with `AsNoTracking`, then maps them into display sections. Pass this model to the existing page while retaining all POST fields. This has a few more files but gives the UI an explicit boundary and makes “no writes/no conversion” testable.

### Approach 3 — Client-side card transformation

Reuse HTML data attributes and build cards in JavaScript. This avoids another server model but cannot safely add local inventory/activation data that was not rendered, weakens accessibility and server-side consistency, and would make security/contract tests less direct. It is rejected.

## Presentation contract

The implementation will add display-only records under `Orofoods.Web/Models/Integrations`:

- `WmcProductConfirmationPresentation`: selected candidate, WMC stock snapshot, local cadastro snapshot, and known display blockers.
- `WmcProductConfirmationWmcStock`: raw `ESTOQUEDISPONIVEL`, `ESTOQUEATUAL`, `ESTOQUERESERVADO`, consultation source/time, and an explicit `UnitSemantics` value of `não confirmada` unless the source contract proves otherwise.
- `WmcProductConfirmationLocalState`: product identity, `IsWmcActive`, `IsActive`, `IsAvailable`, `WmcInitialLoadReady`, `WmcStockAvailable`, `ProductInventory` quantities, `AvailableQuantity`, `IsManuallyAdjusted`, and local unit labels.

The page model will receive an optional read-only collection of these presentations for the confirmation step. New products will show “não cadastrado no Orofoods” instead of fabricated local values, plus a separate explanatory note that manual import does not enable commercial availability or create local inventory.

The composer will:

1. Filter by the already validated selected brand/code keys.
2. Re-query the selected WMC rows through the existing read-only reader for confirmation-only stock fields. The card labels this as a confirmation read; the import POST independently re-reads and validates its own input, so the presentation is not treated as an authorization snapshot.
3. Query products and inventories with `AsNoTracking`, keyed by WMC code/product ID.
4. Keep WMC quantities and local quantities in separate fields and labels.
5. Derive only display facts already defined by the domain (`QuantityOnHand - QuantityReserved`, clamped to zero, and known flag/blocker descriptions).
6. Never infer a WMC-to-local unit conversion or write to the database. If a WMC or local read fails, preserve the existing candidate and render an explicit “não disponível nesta consulta” marker rather than blocking or inventing values.

## Visual contract

- Cards use the existing WMC surface, border, dark-mode variables, and ciano primary color (`#0E7490`) with the existing hover color (`#0B5D75`).
- The card header shows WMC code, description, brand, WMC situation, and a concise state badge.
- Sections are visually separated as “Dados oficiais WMC”, “Cadastro Orofoods”, and “Disponibilidade comercial”.
- Official units, factors, EAN/DUN, quantities, and alternative units remain expandable per product.
- Stock labels include their source field and do not display WMC values as local inventory.
- The reimport authorization remains outside the cards and separate from the action buttons; it remains unchecked by default.
- Each product card keeps the three diagnostic sections compact; the sections collapse from three to two to one column at the responsive breakpoints and keep all details readable without horizontal overflow.

## Verification and tests

- Presentation-model/composer tests cover new AVIAÇÃO `101900`, existing products, inactive local products, zero/missing/invalid stock, reserved stock, manually adjusted inventory, and absent local products.
- Controller tests prove the presentation is built for selected products and that existing selection keys, antiforgery, reimport authorization, and POST action names remain unchanged.
- View/CSS contract tests prove the three sections, card grid, responsive breakpoint, ciano colors, official data labels, distinct stock origins, and blocker labels.
- Security tests prove the presentation path uses `AsNoTracking` and does not call save/import/activation APIs.
- Existing import, automatic sync, inventory adjustment, BIMBO, and order-export tests remain unchanged and must continue to pass.

## Open evidence and risks

- Production values for `ESTOQUEATUAL`, `ESTOQUEDISPONIVEL`, `ESTOQUERESERVADO`, and the local `ProductInventory` row for `101900` require a separately authorized read-only query. The UI will remain truthful when those values are absent.
- A new active WMC product will be cadastral-active but remains commercially unavailable until the existing commercial flow changes `IsAvailable`; `WmcInitialLoadReady` and its blockers must remain truthful. Existing product activation is preserved, and no retroactive action is taken for `101900`.
- No change to `WmcSync__Enabled`; the local and deployed setting must remain false.
