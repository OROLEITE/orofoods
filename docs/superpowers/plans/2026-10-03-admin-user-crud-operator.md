# Admin User CRUD and Operator Role Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Complete Admin user management and add an Operator role restricted to the CRM/WhatsApp workflow.

**Architecture:** Keep ASP.NET Core Identity and the existing `ApplicationUser`, `IsActive`, customer and sales-representative links. Extend the current admin user service/controller and protect the CRM through explicit role checks plus conversation-level scope; do not add a parallel identity or audit subsystem.

**Tech Stack:** ASP.NET Core MVC, Identity, EF Core, Razor, xUnit.

**Spec:** `C:\Users\TarcisioSassi\.codex\attachments\969d93f9-a379-4467-b603-f583e9a982f9\Pasted text.txt`

## Global Constraints

- Operador may use CRM/WhatsApp only; all other Admin controllers stay denied to Operador.
- CRUD and all mutations are Administrador-only, use Identity APIs, and validate antiforgery.
- Do not hard-delete users, store/log passwords or reset tokens, or change normalized Identity fields directly.
- Do not create a migration; `IsActive` and all required relationships already exist.
- Do not use Azure/staging/production databases or call Meta, WhatsApp, Mercado Pago, or WMC.
- No general Identity audit exists; document user-management audit events as backlog rather than creating a new audit subsystem.
- Use the supplied light palette (`#F8FAFC`, `#FFFFFF`, `#F1F5F9`, `#E2E8F0`, `#CBD5E1`, `#0F172A`, `#475569`, `#64748B`, `#2563EB`, `#1D4ED8`, `#EFF6FF`, `#16A34A`, `#DCFCE7`) and preserve dark mode; do not add large beige surfaces.
- Keep a complete desktop table, compact tablet layout, and mobile cards/adaptive table without unnecessary horizontal overflow.
- Preserve Cliente, Vendedor, Administrador, and GerenteComercial behavior.
- Keep the existing NU1903 warning for `System.Security.Cryptography.Xml` out of scope.
- Do not merge or deploy.

## Review Focus

- An Operator must never inherit the unscoped administrator conversation query; tests pin list, detail, message, media, and mutation scope.
- An inactive user must lose sign-in and current sessions; tests cover status transitions and stamp renewal.
- Concurrent or malicious form input must not bypass role, linkage, e-mail, password, antiforgery, or last-admin checks.
- Return URLs must not redirect an Operator into a denied or external destination; login tests pin the CRM landing page.
- Existing non-Operator role access and customer/seller links must retain their current behavior; regression tests cover these roles.

---

### Task 1: Identity-backed Admin user operations

**Files:**
- Modify: `Orofoods.Web/Program.cs`
- Modify: `Orofoods.Web/Services/Identity/AdminUserService.cs`
- Modify: `Orofoods.Web/ViewModels/AdminUserViewModels.cs`
- Modify: `Orofoods.Web.Tests/Infrastructure/TestIdentityFactory.cs`
- Test: `Orofoods.Web.Tests/Services/AdminUserServiceTests.cs`

**Interfaces:**
- Keep `AdminUserService` as the application boundary for create, edit, status change, role change, and password reset.
- Map the requested Nome to Identity's existing `UserName`; do not add a name column. Set and validate it through `UserManager`, including uniqueness.
- Load selectable roles from `RoleManager` and validate destination role, customer, and seller IDs against the local context.

- [x] Add failing service tests for create validation, e-mail/username uniqueness, Identity password policy, role validation, edit/link/unlink, activation/deactivation and sign-in denial, reset via Identity token APIs, last active Administrator protection, and Identity concurrency failure handling.
- [x] Run the focused service tests and confirm the new cases fail.
- [x] Implement each operation with `UserManager`/`RoleManager`; update `Email`/`UserName` through Identity APIs, preserve optional links, rotate the security stamp on access/status changes, and prevent removal/deactivation of the last active Administrator.
- [x] Re-run the focused service tests and confirm they pass.

### Task 2: Administrator-only user CRUD and responsive UI

**Files:**
- Modify: `Orofoods.Web/Areas/Admin/Controllers/UsersController.cs`
- Modify: `Orofoods.Web/Areas/Admin/Views/Users/Index.cshtml`
- Modify: `Orofoods.Web/Areas/Admin/Views/Users/Edit.cshtml`
- Create: `Orofoods.Web/Areas/Admin/Views/Users/Create.cshtml`
- Create: `Orofoods.Web/Areas/Admin/Views/Users/ResetPassword.cshtml`
- Modify or create: `Orofoods.Web/wwwroot/css/admin-users.css`
- Test: `Orofoods.Web.Tests/Authorization/AdminUsersAuthorizationTests.cs`
- Test: `Orofoods.Web.Tests/Views/AdminUsersViewTests.cs`

**Interfaces:**
- Expose GET/POST Create, Edit, and ResetPassword, plus POST Activate/Deactivate; all mutation actions use `[HttpPost]` and `[ValidateAntiForgeryToken]`.
- Index supports query, role and active-state filters and returns a bounded, ordered list with role and link labels.
- Destructive or credential-sensitive actions require a confirmation view/step; never echo passwords or tokens.

- [x] Add failing controller/view tests for administrator access, create/edit flows, search and filters, status actions, password reset confirmation, antiforgery markup, and responsive light/dark presentation.
- [x] Run focused tests to confirm failures.
- [x] Implement controller/view flows using Task 1 service methods and the existing admin design system; group form fields into user data, access, links, and security.
- [x] Re-run focused tests and confirm they pass.

### Task 3: Operator role and CRM-only server authorization

**Files:**
- Create: `Orofoods.Web/Models/Identity/ApplicationRoles.cs`
- Modify: `Orofoods.Web/Data/SeedData.cs`
- Modify: `Orofoods.Web/Areas/Admin/Controllers/UsersController.cs`
- Modify: `Orofoods.Web/Areas/Admin/Controllers/WhatsAppController.cs`
- Modify: `Orofoods.Web/Services/Identity/SalesRepresentativeAccessService.cs`
- Modify: `Orofoods.Web/Views/Shared/_AdminNavigation.cshtml`
- Modify: `Orofoods.Web/Views/Shared/_LoginPartial.cshtml` if it exposes Admin destinations
- Test: `Orofoods.Web.Tests/Authorization/OperatorCrmAuthorizationTests.cs`
- Test: `Orofoods.Web.Tests/Data/SeedDataTests.cs`

**Interfaces:**
- Define compile-time constants for the existing Administrator, Customer, Seller, Commercial Manager, and new Operator role names; use them in touched authorization/login/menu code without mass-editing unrelated controllers.
- Seed the exact role name `Operador` idempotently.
- Only WhatsApp and the minimum endpoints used by its current CRM workflow accept Operador.
- Operator can use the shared WhatsApp conversation queue and its current CRM actions; this explicit scope is limited to CRM/WhatsApp endpoints and never grants access to other Admin controllers or customer administration.
- Add a CSRF-protected Reopen action for authorized closed conversations, returning them to the existing Pending queue flow; leave other status transitions intact.
- Render only CRM > WhatsApp for Operator; preserve menus for other roles.

- [x] Add failing tests for role seeding, Operator WhatsApp access and allowed operations (including reopen), denial of every other Admin section/direct URL, and sidebar contents; include existing Admin, manager, seller, and customer authorization regressions.
- [x] Run focused authorization tests and confirm failures.
- [x] Implement the narrow role/menu/policy changes and explicit shared WhatsApp scope. Hide customer-detail navigation unavailable to Operator; keep only the required customer lookup/link workflow with minimal returned fields.
- [x] Re-run focused authorization tests and confirm they pass.

### Task 4: Operator landing page and full verification

**Files:**
- Modify: `Orofoods.Web/Areas/Identity/Pages/Account/Login.cshtml.cs`
- Modify: relevant login/authorization test files under `Orofoods.Web.Tests`

**Interfaces:**
- After successful login, Operador goes to the real CRM route `/Admin/WhatsApp`; Admin, Cliente, Vendedor and manager redirects remain unchanged.
- A local test account only may be used for manual Development validation; no production credentials or external sends.

- [x] Add failing login tests for Operator landing, safe local return URLs, and preserved existing role redirects.
- [x] Run focused login tests and confirm failures.
- [x] Implement Operator redirect without changing Administrator, Cliente, or Vendedor behavior.
- [x] Run focused CRUD, Identity, Operator, and existing authorization tests; validate a Development account only if its configured database is confirmed local; then run `dotnet build -c Release` and the full suite.
- [x] Inspect `git diff --check`, verify no migration/external side effect, and record counts plus the existing NU1903 warning.
- [x] Commit on `feature/admin-user-crud-and-operator-role`; push/open a PR only if credentials are available; do not merge or deploy.
