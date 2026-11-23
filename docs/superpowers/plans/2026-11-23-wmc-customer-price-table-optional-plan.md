# WMC Customer Price Table Optional Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans or superpowers:subagent-driven-development to implement this plan task-by-task.

**Goal:** Allow manual WMC customer imports without requiring or assigning a price table while preserving seller validation, cadastral validation, duplicate prevention, safe blocking, approved dark-mode UI copy, and automated confirmation coverage.

**Architecture:** Keep `Customer.PriceTableId` nullable and remove price-table readiness from `WmcCustomerImportService`. Add a distinct `Blocked` preview disposition for global seller-readiness failures, leaving `Conflict` exclusively for row-level cadastral conflicts. Update the existing WMC view labels and preserve the current antiforgery/confirmation flow.

**Tech Stack:** ASP.NET Core MVC, EF Core, PostgreSQL model metadata, Razor, vanilla JavaScript, xUnit.

**Spec:** User authorization in the conversation, including no real imports, migrations, database/Firebird/Azure changes, deployment, or WMC order-integration changes.

## Global Constraints

- Do not execute real WMC imports or change PostgreSQL, Firebird, Azure, credentials, or deployment state.
- Do not assign or create a price table during customer import.
- Keep seller WMC-code validation, CNPJ validation, duplicate prevention, antiforgery, and the conflict block.
- Preserve the approved dark layout and WMC order integration.

## Review Focus

- A valid customer with an active WMC seller imports without a price table and leaves `PriceTableId` null.
- A missing/inactive/duplicate seller is a global block, not a cadastral conflict.
- Duplicate WMC/CNPJ records remain individual conflicts and prevent import.
- The UI exposes blocking and conflict reasons without enabling an unsafe confirmation.
- The controller confirmation path remains covered without making a real import.

### Task 1: Service model and readiness rules

**Files:**
- Modify: `Orofoods.Web/Integrations/Erp/Wmc/WmcCustomerImportService.cs`
- Delete or remove usage: `Orofoods.Web/Integrations/Erp/Wmc/WmcCustomerImportOptions.cs`
- Modify: `Orofoods.Web/Program.cs`, `Orofoods.Web/appsettings.json`
- Test: `Orofoods.Web.Tests/Services/WmcCustomerImportServiceTests.cs`

- [ ] Add failing tests for import without a price table and distinct seller blocking.
- [ ] Run focused tests and confirm the expected failures.
- [ ] Remove price-table readiness and preserve nullable `PriceTableId`.
- [ ] Add `Blocked` disposition/count and retain row-level conflicts.
- [ ] Run focused service tests.

### Task 2: UI naming and blocked-preview presentation

**Files:**
- Modify: `Orofoods.Web/Areas/Admin/Views/Integrations/Wmc.cshtml`
- Modify: `Orofoods.Web/wwwroot/js/wmc-integration-center.js` only if required by the existing interaction contract.
- Test: `Orofoods.Web.Tests/Views/WmcCustomerImportViewTests.cs`, `Orofoods.Web.Tests/Views/WmcIntegrationCenterViewTests.cs`

- [ ] Add failing view assertions for the approved labels and blocked summary.
- [ ] Update visible text only, preserving layout/classes and antiforgery.
- [ ] Show global blocking reason at the top and individual row reasons in the table.
- [ ] Keep the customer import action unavailable when blocked or conflicted.
- [ ] Apply equivalent product terminology.

### Task 3: Confirmation flow tests and verification

**Files:**
- Modify: `Orofoods.Web.Tests/Controllers/WmcCustomerPreviewControllerTests.cs`
- Add/modify: focused controller/service tests as needed.

- [ ] Add a controller test for rejected confirmation and service invocation contract without a real import.
- [ ] Run focused tests, build, and the full test suite available for the solution.
- [ ] Review the diff and confirm no database, migration, Firebird, Azure, order-integration, or deploy changes.
