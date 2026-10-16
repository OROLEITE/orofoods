# WMC BIMBO Staging Import Design

## Goal

Prepare an administrator-only, repeatable import for the ten approved BIMBO (WMC brand 104) products in Staging, without running the import or enabling sales/export.

## Current state

`WmcProductReader` reads the WMC replica through the read-only `IWmcFirebirdReader`. `WmcProductPreviewService` can preview selected brands. `WmcSyncService.SyncProductsAsync` is a global synchronization that may create unrelated products and update existing catalog metadata, so it is not suitable for this operation.

The current projection selects `CODPRODUTO`, `PRODUTO`, `SITUACAO`, `UN`, stock columns, `CODBARRASFAB`, `CODMARCA`, `UN_ALTERN`, `QTDE_EMB`, and `QTDE_CONV`, among other existing fields. `CODBARRASFAB` is mapped to legacy `Ean`; the reader does not populate `Ean13`, `Dun14`, `CommercialUnitCode`, `LogisticsUnitCode`, or `PackagesPerCase`. The business meaning of `UN_ALTERN`, `QTDE_EMB`, and `QTDE_CONV` has not been confirmed for PCT/CX.

## Approved import contract

- Accept requests only in the Staging environment, through an Administrator-authorized, anti-forgery-protected POST action.
- Scope every request to WMC brand 104 and exactly these product codes: `503768`, `503775`, `503777`, `503805`, `503811`, `503813`, `503833`, `503838`, `504338`, and `504342`.
- Read brand 104 using the existing read-only WMC product reader. Before writing, require exactly one active WMC row for every requested code, the expected brand code, and a valid nonnegative integer `ESTOQUEATUAL` for each. Missing, duplicate, inactive, unknown-status, or invalid-stock input rejects the batch without product/inventory writes.
- In one serializable PostgreSQL transaction, create only local products whose normalized WMC code is absent. Existing products are reported as skipped and never overwritten. A unique normalized WMC-code index remains the final concurrency guard.
- New products use the WMC code and name, a generated `WMC-{code}` SKU, brand 104, Orofoods unit `PCT` (the approved `PC` to `PCT` display mapping), provisional `BasePrice = 1.00`, and the inactive pending-categorization category. Persist `ESTOQUEATUAL` directly as `ProductInventory.QuantityOnHand`; do not multiply, divide, or substitute another stock field.
- Keep `IsActive = false`, `IsAvailable = false`, and initial-load readiness false. Set no EAN-13, DUN-14, logistics unit, or case factor until their WMC mappings are confirmed. Do not alter TXT generation, export eligibility, existing products, prices, images, or WMC data.
- Persist a run audit with actor, brand, start/completion time, outcome, imported codes, skipped codes, and validation/runtime errors. A failed database transaction must leave no partial products/inventory and should record a failure audit after rollback when the database remains available.
- Repeated requests are idempotent: already imported codes are skipped and retain their existing local values.
- The operation is implemented but not invoked in this task. No WMC import, migration application, push, merge, or deployment is permitted.

## Data and migration

Add a small WMC product-import run audit entity and matching migrations for the SQLite and PostgreSQL model tracks, including their model snapshots. The PostgreSQL migration will be compiled and rendered as SQL for inspection, but never applied. No WMC column is added or renamed based on guesses.

## Components

- `WmcBimboStagingImportService`: validates the fixed batch, reads WMC rows, performs the transactional local insert/stock preservation, skips existing products, and records the audit.
- `IntegrationsController`: exposes one Staging-only Administrator POST action and returns a summarized outcome; all other environments return `Forbid` before invoking the service.
- WMC import audit model, EF mapping, SQLite/PostgreSQL migrations, and snapshots.
- Focused tests cover environment/authorization guards, exact brand/code scope, invalid and duplicate source rows, direct stock preservation, provisional price, hidden state, no overwrite, transaction rollback, audit, and idempotent replay.

## Out of scope and release blockers

- Do not discover or assume Firebird column names for EAN-13, DUN-14, logistics unit, or PCT-per-CX factor in this change. `CODBARRASFAB`, `UN_ALTERN`, `QTDE_EMB`, and `QTDE_CONV` remain candidate/raw fields until their semantics are confirmed with WMC.
- Do not publish products, enable orders, generate/modify a PCT TXT layout, or release PCT export until unit/factor and WMC order acceptance are homologated.
- Do not run the import, apply migrations, contact production, or alter WMC.
