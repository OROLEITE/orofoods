# Orofoods Admin Dark Theme Completion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the Admin dark theme for all 38 non-Dashboard, non-WhatsApp views while integrating the shared footer and preserving public and Portal appearance.

**Architecture:** Keep shared theme tokens and Admin footer overrides in `admin-shell.css`; keep navigation, account dropdown, Orders, and module rules in their current owning stylesheets. Add module-specific stylesheets only for audited views without an existing owner, load them only for their Admin view, and scope every dark rule to `html[data-theme="dark"] body.admin-authenticated` plus the owning component/page selector.

**Tech Stack:** ASP.NET Core Razor, CSS, xUnit structural view/CSS tests, .NET 10.

**Spec:** `docs/superpowers/specs/2026-10-06-admin-dark-theme-completion-design.md`

## Global Constraints

- Dashboard is the approved visual reference; do not edit its view or stylesheet.
- Do not change public pages, Portal views/styles, WhatsApp/CRM views/styles, business behavior, controllers, ViewModels, database configuration, migrations, or CI/CD.
- Admin dark selectors require both `html[data-theme="dark"]` and `body.admin-authenticated`; module selectors also require their component/page class. Exclude the WhatsApp inbox using its existing `.whatsapp-inbox-page` marker and do not modify its view/styles.
- Do not add blanket dark rules for `.card`, `.table`, `.modal`, or form controls.
- Preserve routes, IDs, JavaScript hooks, status meaning, button semantics, primary action color, responsive table behavior, sidebar/offcanvas operation, and semantic danger states.
- Keep the shared footer markup and public `site.css` footer rules unchanged; compact and darken the footer only in Admin dark mode.
- Palette: canvas `#0F172A`; surface `#111827`; secondary surface `#1E293B`; border `#334155`; text `#E5E7EB` / `#F8FAFC`; secondary text `#94A3B8` / `#CBD5E1`; link/focus `#38BDF8`; accent `#F59E0B`; success `#10B981`.
- Do not commit, push, open a PR, or deploy.

## Review Focus

- WhatsApp's independent light/dark theme must not inherit Admin overrides, including when the CRM itself selects dark mode; exercise the `.whatsapp-inbox-page` exclusion contract in `AdminDarkThemeContractTests`.
- Public and Portal footer appearance must remain owned by existing public rules; assert `site.css` retains its public `.site-footer` rules and new dark rules require the Admin body scope.
- Shared header CSS loads before Admin CSS and per-view styles can load later; verify the final selectors win without broad `!important` usage.
- Order status colors and primary actions must retain their semantic meaning on dark surfaces; assert all existing status variants and action hooks remain represented.
- Narrow screens must retain the existing mobile order-card/table behavior and compact footer/navigation; review at mobile and tablet breakpoints and preserve structural responsive rules.

---

### Task 1: Lock Admin theme and footer boundaries with structural tests

**Files:**
- Create: `Orofoods.Web.Tests/Views/AdminDarkThemeContractTests.cs`
- Create: `Orofoods.Web.Tests/Views/AdminDarkThemeHistoricalRules.json` (fixed pre-change selector/declaration baseline for the CSS files audited by the scope contract)
- Inspect: `Orofoods.Web/Views/Shared/_Layout.cshtml`, `Orofoods.Web/wwwroot/css/site.css`, `Orofoods.Web/wwwroot/css/admin-shell.css`, `Orofoods.Web/wwwroot/css/header.css`, `Orofoods.Web/wwwroot/css/admin-navigation.css`, `Orofoods.Web/wwwroot/css/admin-orders.css`

**Interfaces:**
- Produces test contracts for the scoped footer, navigation, account dropdown, Orders styles, public footer ownership, and CRM theme boundary; later tasks make these contracts pass.

- [ ] **Step 1: Add failing structural tests** named `AdminFooterDarkRulesRequireAdminThemeAndBody`, `PublicFooterRulesRemainInSiteStylesheet`, `AdminNavigationAndAccountRulesAreScoped`, `AdminOrdersRulesAreScoped`, and `WhatsAppThemeRemainsOutsideAdminDarkRules`. Assert both required root gates, the `body.admin-authenticated:not(:has(.whatsapp-inbox-page))` exclusion, and component/page selectors; public footer declarations remain in `site.css`, and the CRM route's independent theme override remains intact.
- [ ] **Step 2: Run the focused tests** with `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --filter FullyQualifiedName~AdminDarkThemeContractTests`; confirm failures identify the missing rules rather than compilation/setup errors.

### Task 2: Integrate Admin shell, navigation, account dropdown, and footer (section 4)

**Files:**
- Modify: `Orofoods.Web/wwwroot/css/admin-shell.css`
- Modify: `Orofoods.Web/wwwroot/css/admin-navigation.css`
- Modify: `Orofoods.Web/wwwroot/css/header.css`
- Test: `Orofoods.Web.Tests/Views/AdminDarkThemeContractTests.cs`

**Interfaces:**
- Consumes Task 1's selectors and scope assertions.
- Produces Admin-only shell tokens/footer, navigation interaction states, and account dropdown states.

- [ ] **Step 1: Add footer/nav/account assertions** for `#0F172A` or `#111827`, top border `#334155`, text/link contrast, focus/hover, compact logo and spacing, and responsive footer stacking; assert all selectors contain both Admin gates, the WhatsApp exclusion, and their component selector.
- [ ] **Step 2: Run the focused contract tests** and confirm the new assertions fail before the CSS changes.
- [ ] **Step 3: Implement scoped shell and footer rules** in `admin-shell.css`; keep public footer rules and `_Layout.cshtml` markup untouched.
- [ ] **Step 4: Implement scoped nav and account menu states** in `admin-navigation.css` and `header.css`, retaining sidebar dimensions, active accent, existing dropdown markup, keyboard focus, and public/Portal styles.
- [ ] **Step 5: Run the focused contract tests** and confirm the shell, footer, navigation, and dropdown assertions pass.

### Task 3: Complete Admin Orders dark surfaces (CSS/cascade section 5)

**Files:**
- Modify: `Orofoods.Web/wwwroot/css/admin-orders.css`
- Modify only for a page-scoped styling hook: `Orofoods.Web/Areas/Admin/Views/Orders/Details.cshtml` (add `admin-order-details-page` to its root section; preserve routes, IDs, data attributes, and behavior).
- Inspect: `Orofoods.Web/Areas/Admin/Views/Orders/Index.cshtml`, `Orofoods.Web/Areas/Admin/Views/Orders/Details.cshtml`
- Test: `Orofoods.Web.Tests/Views/AdminDarkThemeContractTests.cs`
- Existing test coverage to preserve: `Orofoods.Web.Tests/Views/OrderWmcAuditViewTests.cs`

**Interfaces:**
- Consumes shared tokens from Task 2.
- Produces scoped dark list/detail, WMC, form, table, badge, history, filter, and action surfaces without changing markup behavior. Use `.admin-orders-page` for the list and `.admin-order-details-page` for the existing detail root; do not style nonexistent modals.

- [ ] **Step 1: Add failing Orders assertions** for the list surfaces and readable `.admin-page-header .eyebrow` contrast, detail cards/alerts/forms/history/WMC/items, each existing status class, responsive table/action hooks, and the detail page scope marker. Confirm whether any modal exists in the current views before adding modal rules.
- [ ] **Step 2: Run focused contract tests** and confirm the missing Orders dark selectors fail.
- [ ] **Step 3: Add only `admin-order-details-page` to the Details root section** as a CSS scope marker, then implement page-scoped rules in `admin-orders.css`; document cascade specificity/order inline only where necessary and avoid `!important` unless a confirmed Bootstrap rule requires it.
- [ ] **Step 4: Run focused contract plus `OrderWmcAuditViewTests`** and confirm both dark contracts and existing WMC structure pass.

### Task 4: Complete Customers and Commercial modules

**Files:**
- Modify as indicated by the audited view classes: `wwwroot/css/commercial.css`, `wwwroot/css/admin-customers.css`.
- Inspect: all in-scope views in Commercial and Customers.
- Test: `Orofoods.Web.Tests/Views/AdminDarkThemeContractTests.cs` and relevant existing module view tests.

**Interfaces:**
- Consumes shared tokens and boundaries from Tasks 1–2.
- Produces dark page-specific surfaces and control states using the Commercial and Customers stylesheet owners.

- [ ] **Step 1: Record a view-to-owner audit** for every Commercial and Customers page, including inline `Head` styles and later cascade sources; extend the fixed historical selector/declaration baseline only for the relevant existing owner rules before adding new dark rules.
- [ ] **Step 2: Add structural assertions** for remaining light surfaces, controls, empty/validation states, disabled/focus/hover states, and scoped root/component selectors in each audited module. Keep the historical baseline fixed and do not derive exceptions from current CSS at test runtime.
- [ ] **Step 3: Run focused tests** and confirm the newly added module assertions fail on the current light rules.
- [ ] **Step 4: Correct only the remaining Admin dark surfaces** in the owning stylesheets; preserve geometry, behavior, public selectors, and existing responsive rules.
- [ ] **Step 5: Run focused and existing Commercial/Customers view tests**; confirm these module contracts pass.

### Task 5: Complete Products and Inventory modules

**Files:**
- Modify: `wwwroot/css/admin-products.css`.
- Create only if the audit confirms no suitable owner: `wwwroot/css/admin-inventory.css`.
- Modify: Inventory view `Head` section only if `admin-inventory.css` is needed.
- Inspect: all in-scope Products and Inventory views.
- Test: `Orofoods.Web.Tests/Views/AdminDarkThemeContractTests.cs` and relevant existing view tests.

**Interfaces:**
- Consumes shared tokens and selectors from Task 2.
- Produces dark page-specific surfaces and control states using the Products owner and, only if needed, an Inventory-specific owner.

- [ ] **Step 1: Audit view class names and existing CSS ownership** for Products and Inventory; document whether Inventory needs its own stylesheet.
- [ ] **Step 2: Add failing assertions** for remaining light surfaces/control selectors and verify every selector requires both Admin gates plus its module/page class.
- [ ] **Step 3: Run the focused test** and confirm the new module assertions fail before implementation.
- [ ] **Step 4: Correct Product surfaces in `admin-products.css` and create/load `admin-inventory.css` only if needed.** Cover text/surface/control/empty/validation/interaction states while preserving markup, geometry, and semantic button colors.
- [ ] **Step 5: Run focused contract and existing Product/Inventory view tests**; confirm these module contracts pass.

### Task 6: Complete auxiliary Admin registration modules

**Files:**
- Existing owners: `wwwroot/css/admin-users.css`, `wwwroot/css/admin-registrations.css` where appropriate.
- Create only for confirmed ownerless views: `wwwroot/css/admin-categories.css`, `admin-drivers.css`, `admin-driver-payment-terminal-assignments.css`, `admin-notifications.css`, `admin-opportunities.css`, `admin-payment-terminals.css`, `admin-payment-terms.css`, `admin-price-tables.css`, and `admin-sales-representatives.css`.
- Modify: matching module view `.cshtml` `Head` sections only when a new stylesheet is required.
- Inspect: Categories, Drivers, Driver Payment Terminal Assignments, Notifications, Opportunities, Payment Terminals, Payment Terms, Price Tables, Sales Representatives, and Users.
- Test: `Orofoods.Web.Tests/Views/AdminDarkThemeContractTests.cs` and relevant existing view tests.

**Interfaces:**
- Consumes shared tokens and scope from Task 2.
- Produces dark page-specific surfaces and control states for auxiliary Admin modules without blanket cross-site rules.

- [ ] **Step 1: Audit view class names and CSS ownership** across the listed modules; omit any new stylesheet where an adequate owner exists.
- [ ] **Step 2: Add failing module assertions** for remaining light surfaces and controls; verify both Admin gates plus each module/page selector.
- [ ] **Step 3: Run focused tests** and confirm new assertions fail for missing module rules.
- [ ] **Step 4: Implement module rules in existing owners or create/load only the required module-specific files.** Cover text, surfaces, controls, empty/validation states, hover/focus/disabled states while preserving layout and behavior.
- [ ] **Step 5: Run focused contract and relevant existing view tests**; confirm every module assertion passes.

### Task 7: Complete Reports and Integrations modules

**Files:**
- Modify as indicated by the audit: `wwwroot/css/admin-reports.css`, `wwwroot/css/admin-reports-bi.css`, `wwwroot/css/admin-integrations.css`.
- Inspect: Reports and Integrations views (excluding WhatsApp/CRM).
- Test: `Orofoods.Web.Tests/Views/AdminDarkThemeContractTests.cs`
- Existing tests to preserve: relevant reports/integration view tests.

**Interfaces:**
- Consumes all implementation tasks.
- Produces dark page-specific surfaces and control states for Reports and Integrations.

- [ ] **Step 1: Audit Reports and Integrations views, styles, inline `Head` sections, and cascade order.** Explicitly exclude WhatsApp/CRM styles and views.
- [ ] **Step 2: Add failing scoped module assertions**, run the focused tests, and confirm they fail for missing dark rules.
- [ ] **Step 3: Correct only the remaining dark surfaces and control states** in the established module stylesheets; keep geometry and behavior unchanged.
- [ ] **Step 4: Run focused contract and relevant existing view tests**; confirm these module assertions pass.

### Task 8: Responsive review, acceptance tests, and final report (sections 6, 7, and 8)

**Files:**
- Review all files changed in Tasks 1–7.
- Test: `Orofoods.Web.Tests/Views/AdminDarkThemeContractTests.cs` and relevant existing view tests.

- [ ] **Step 1: Review desktop, tablet, and mobile behavior**; verify sidebar collapse/offcanvas, compact responsive footer, existing horizontal/mobile order table behavior, keyboard focus, contrast, and status colors. Apply only scoped corrections and re-run affected focused tests.
- [ ] **Step 2: Run `git diff --check`** from the isolated worktree; expected: no whitespace errors.
- [ ] **Step 3: Run `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release`**; expected: zero failed tests, with the total reported exactly.
- [ ] **Step 4: Audit the complete tracked and untracked diff**; confirm no Dashboard view/CSS, public footer rules, Portal, WhatsApp/CRM, business/backend, database/migration, or CI/CD files changed. Confirm shared footer markup is unchanged and public `.site-footer` CSS remains as before.
- [ ] **Step 5: Report changed files, cascade findings, responsive review, both validation outputs, remaining risks, Admin footer changes, and explicit confirmation that the public footer was not changed. Do not commit, push, open a PR, or deploy.**
