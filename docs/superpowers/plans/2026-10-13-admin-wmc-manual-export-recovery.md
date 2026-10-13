# Admin WMC Manual Export Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Persist manual WMC TXT artifacts and add a safe, audited rejection and regeneration flow while identifying customers by CNPJ under NeoGrid v3.2.

**Architecture:** Apply the isolated NeoGrid layout patch to current `origin/main`, then extend the existing state machine with `Rejected` and a dedicated immutable artifact entity. Keep generation, state, artifact, and audit changes transactional; expose separate Admin POST actions for rejection/regeneration and a read-only GET for verified repeat download.

**Tech Stack:** .NET 10, ASP.NET Core MVC, Entity Framework Core, SQLite test database, PostgreSQL migrations, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-13-admin-wmc-manual-export-recovery-design.md`

## Global Constraints

- Do not access or change Azure, Staging, production, PostgreSQL instances, WMC, or existing orders.
- Do not push, merge, deploy, or enable automatic WMC integration.
- Preserve unrelated worktrees and local changes.
- Do not automatically mark an order imported.
- Every mutating action requires `Administrador`, antiforgery, and concurrency protection.

## Review Focus

- A legacy successful audit without an artifact must block normal export, hide repeat download, and allow the explicit rejection path.
- An artifact whose content does not match its SHA-256 must never be downloaded.
- Two concurrent regeneration requests must create exactly one current artifact and one successful regeneration.
- CNPJ punctuation is normalized, while fewer or more than 14 digits blocks generation with the same diagnostic error.
- A generation or persistence failure after a rejection must leave the rejection history intact and must not set `Imported`.

---

### Task 1: NeoGrid v3.2 customer identity and positional records

**Files:**
- Modify: `Orofoods.Web/Integrations/Erp/Wmc/WmcOrderFileGenerator.cs`
- Modify: `Orofoods.Web.Tests/Integrations/WmcOrderFileGeneratorTests.cs`
- Modify: `Orofoods.Web.Tests/Services/WmcManualExportServiceTests.cs`

**Interfaces:**
- Produces: `WmcOrderFileGenerator.Validate(Order)` requiring a 14-digit normalized customer CNPJ and not requiring `Customer.WmcCode`.
- Produces: `WmcOrderFileGenerator.Build(Order)` with positional records `019`, `021`, `030`, `040`, and `090`.

- [x] Add failing tests proving an order without customer `WmcCode` is eligible, CNPJ is required/normalized, record `019` contains customer CNPJ in every buyer field, and record `090` contains totals rather than customer code.
- [x] Run focused generator/service tests and confirm the new assertions fail for the expected old behavior/layout.
- [x] Apply the two-file layout diff from `fix-wmc-order-txt-record-layout` without modifying that worktree; update validation from customer WMC code to normalized CNPJ.
- [x] Run the focused tests and inspect a locally generated ORO-2026-000006 TXT field by field.

### Task 2: Artifact and rejected-state persistence

**Files:**
- Create: `Orofoods.Web/Models/Integrations/WmcManualExportArtifact.cs`
- Modify: `Orofoods.Web/Models/Integrations/WmcOrderExportStatus.cs`
- Modify: `Orofoods.Web/Models/Integrations/WmcExportAudit.cs`
- Modify: `Orofoods.Web/Models/Integrations/WmcOrderExportState.cs`
- Modify: `Orofoods.Web/Models/Orders/Order.cs`
- Modify: `Orofoods.Web/Data/ApplicationDbContext.cs`
- Modify: `Orofoods.Web/Data/PostgreSqlApplicationDbContext.cs`
- Create/modify: generated SQLite and PostgreSQL migrations and model snapshots under `Orofoods.Web/Data/Migrations*`
- Create: `Orofoods.Web.Tests/Data/WmcManualExportArtifactMigrationTests.cs`

**Interfaces:**
- Produces: immutable `WmcManualExportArtifact` with `AttemptId`, `Content`, `ContentSha256`, creator, predecessor, and regeneration reason.
- Produces: `WmcOrderExportStatus.Rejected`, `WmcExportOutcome.Rejected`, and `WmcExportOutcome.Regenerated`.

- [x] Add failing model and migration tests for relationships, unique attempt/current-artifact constraints, `Rejected` check constraint, field limits, and both providers.
- [x] Run migration tests and confirm failure because the entity/schema is absent.
- [x] Add the entities/configuration and generate provider-specific migrations without applying them to any database.
- [x] Run migration/model tests and inspect generated migration SQL for indexes, constraints, delete behavior, and no fabricated artifact backfill.

### Task 3: Transactional export artifact and repeat download

**Files:**
- Modify: `Orofoods.Web/Services/Integrations/WmcManualExportService.cs`
- Modify: `Orofoods.Web.Tests/Services/WmcManualExportServiceTests.cs`

**Interfaces:**
- Produces: `DownloadAsync(int orderId, CancellationToken) -> WmcManualExportResult`.
- Updates: `ExportAsync(...)` atomically saves an artifact and points state to it.

- [x] Add failing tests for persisted content/hash, atomic first export, blocked normal export after any successful history, valid repeat download without new audit/state, absent legacy artifact, corrupted hash, and no automatic import.
- [x] Run focused service tests and confirm failures match missing persistence/download behavior.
- [x] Implement SHA-256 verification and transactional artifact/state/audit persistence with specific dispositions/errors.
- [x] Run focused service tests and retain current duplicate, stale-processing, and concurrency coverage.

### Task 4: Audited rejection and regeneration state machine

**Files:**
- Modify: `Orofoods.Web/Services/Integrations/WmcManualExportService.cs`
- Modify: `Orofoods.Web/Services/Integrations/WmcExportAuditService.cs`
- Modify: `Orofoods.Web.Tests/Services/WmcManualExportServiceTests.cs`
- Modify: `Orofoods.Web.Tests/Services/WmcExportAuditServiceTests.cs`

**Interfaces:**
- Produces: `RejectAsync(int orderId, string reason, string? userId, string? userEmail, CancellationToken)`.
- Produces: `RegenerateRejectedAsync(int orderId, string reason, string? userId, string? userEmail, CancellationToken)`.

- [x] Add failing tests for required reasons, exported-only rejection, rejected-only regeneration, predecessor link, preserved audits, distinct rejection/regeneration audits, generation failure rollback, and two concurrent regenerations.
- [x] Run focused tests and confirm expected missing-method/state failures.
- [x] Implement guarded transitions and transactional audit/artifact writes using the state version token and unique attempts.
- [x] Run focused tests and verify one winner under concurrency and no transition to `Imported`.

### Task 5: Administrator endpoints and order-list interface

**Files:**
- Modify: `Orofoods.Web/Areas/Admin/Controllers/OrdersController.cs`
- Modify: `Orofoods.Web/Areas/Admin/Views/Orders/Index.cshtml`
- Modify: `Orofoods.Web/Areas/Admin/Views/Orders/Details.cshtml`
- Modify: `Orofoods.Web/wwwroot/css/admin-orders-list.css`
- Create: `Orofoods.Web.Tests/Controllers/AdminWmcManualExportRecoveryTests.cs`
- Modify: `Orofoods.Web.Tests/Views/OrderWmcAuditViewTests.cs`
- Modify: `Orofoods.Web.Tests/Views/AdminOrderDetailsVisualTests.cs`
- Modify: `Orofoods.Web.Tests/Views/AdminDarkThemeContractTests.cs`

**Interfaces:**
- Produces: antiforgery-protected POST actions `RejectWmcTxt` and `RegenerateWmcTxt`.
- Produces: read-only GET action `DownloadWmcTxt`.

- [x] Add failing controller/view tests for Admin authorization, antiforgery, exact labels, send visibility, download availability, reason inputs, rejected-only regeneration, explicit import, and success/outline-hover CSS geometry.
- [x] Run focused controller/view tests and confirm failures correspond to absent endpoints and UI.
- [x] Implement endpoint result handling and the compact WMC action stack; show CNPJ as diagnostic identity and keep optional WMC code informational.
- [x] Run focused controller/view tests and inspect rendered source contracts.

### Task 6: Cross-feature verification

**Files:**
- Modify only files required to correct failures caused by Tasks 1-5.

**Interfaces:**
- Consumes all prior tasks.

- [x] Run all focused generator, service, migration, controller, and view tests; record passed/failed/skipped totals.
- [x] Run `dotnet test Orofoods.slnx --nologo`; record full-suite totals and warnings separately.
- [x] Run `dotnet build Orofoods.slnx --nologo --no-restore`; record errors and warnings separately.
- [x] Generate one local representative TXT, verify record lengths and positional values, and ensure no database or external integration is contacted.
- [x] Review `git diff --check`, `git status`, and the complete diff against the spec; report any deferred issue without push, merge, or deploy.
