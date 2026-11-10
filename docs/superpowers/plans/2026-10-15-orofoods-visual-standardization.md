# Orofoods Visual Standardization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Correct the scoped visual inconsistencies identified in the prompt across customer, Admin, navigation and password-recovery screens without changing their behavior.

**Architecture:** Keep the current Razor layouts and theme setup. Make page-level adjustments in existing scoped stylesheets, reuse semantic action styles and official transparent logo assets, and override only the password-recovery Razor markup while retaining the ASP.NET Identity UI page model and handler.

**Tech Stack:** ASP.NET Core Razor Pages/MVC, CSS, xUnit, .NET 10.

**Spec:** `docs/superpowers/specs/2026-10-15-orofoods-visual-standardization-design.md`

## Global Constraints

- Preserve routes, bindings, forms, filters, pagination, prices, calculations, permissions, Identity handlers/tokens, payments, inventory, WMC and integrations.
- Preserve light mode and public page styling; scope authenticated dark rules to existing Portal/Admin roots and recovery rules to its own view.
- Use `#0E7490` with white text for primary actions; maintain distinct secondary, destructive and informational states.
- Meet 4.5:1 contrast for normal text, 3:1 for meaningful non-text controls, visible keyboard focus and 44px touch targets where practical.
- Cover 1440px, 768px, 430px, 390px and 360px; do not introduce document-level horizontal overflow.
- Do not add dependencies or change controllers, services, models, configuration, database, migrations, seeds or external systems.
- Do not push, open a PR, merge or deploy.

## Review Focus

- Shared header styles must not leak into public pages; test public header routes, logo asset and theme scope in Task 1.
- Portal dark styling must not break existing light-mode pages; test both theme scopes in Task 2.
- Long order/customer values and tables must remain usable at 360px without page overflow; cover local table/layout rules in Tasks 2 and 3 and verify in a browser only if a safe local runtime is available.
- Every catalog/cart/order/draft form must keep its current action and input bindings; pin those contracts in Task 2.
- Admin integration status, error details and operational actions (including existing WMC controls) must remain intact; pin them in Task 3.

---

### Task 1: Authenticated navigation and logo

**Files:**
- Modify: `Orofoods.Web/wwwroot/css/header.css`
- Modify: `Orofoods.Web/wwwroot/css/portal-navigation.css`
- Modify: `Orofoods.Web/wwwroot/css/admin-navigation.css`
- Modify: `Orofoods.Web/wwwroot/css/seller-area.css`
- Test: `Orofoods.Web.Tests/Views/AuthenticatedNavigationTests.cs`
- Test: `Orofoods.Web.Tests/Views/GlobalNavigationTests.cs`
- Test: `Orofoods.Web.Tests/Views/SellerPortalVisualContractTests.cs`

**Interfaces:** Consumes the existing `.customer-experience`, `.portal-authenticated`, `.admin-authenticated` and `.seller-area-page` scopes, existing navigation links/active states and `logo-orofoods-transparent.png`; produces consistent scoped colors, spacing, focus and responsive logo/menu presentation without changing destinations.

- [x] **Step 1: Add the failing navigation and logo contracts** `Authenticated_header_styles_preserve_public_theme_and_transparent_branding` in `AuthenticatedNavigationTests` (assert the official transparent logo, public route links, scoped active/hover/focus colors) and `Seller_sidebar_brand_and_menu_keep_accessible_active_state` in `SellerPortalVisualContractTests` (assert current navigation destinations, `aria-current` and mobile menu control).
- [x] **Step 2: Run the two named tests** with `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --filter "FullyQualifiedName~Authenticated_header_styles_preserve_public_theme_and_transparent_branding|FullyQualifiedName~Seller_sidebar_brand_and_menu_keep_accessible_active_state"`; confirm the new assertions fail before the CSS changes.
- [x] **Step 3: Correct only the scoped header, Portal/Admin navigation and Seller shell styles** in the four listed CSS files; preserve public navigation behavior and all Razor route bindings.
- [x] **Step 4: Run `AuthenticatedNavigationTests`, `GlobalNavigationTests`, `SellerPortalShellTests` and `SellerPortalVisualContractTests`; confirm all pass.**
- [x] **Step 5: Commit** as `fix: standardize authenticated navigation visuals`.

### Task 2: Customer dashboard, catalog, cart, orders and saved drafts

**Files:**
- Modify: `Orofoods.Web/Views/Portal/Dashboard.cshtml`
- Modify: `Orofoods.Web/Views/Portal/Catalog.cshtml`
- Modify: `Orofoods.Web/Views/Portal/Cart.cshtml`
- Modify: `Orofoods.Web/Views/Portal/Orders.cshtml`
- Modify: `Orofoods.Web/Views/Portal/SavedOrders.cshtml`
- Modify: `Orofoods.Web/wwwroot/css/customer-experience.css`
- Modify: `Orofoods.Web/wwwroot/css/catalog.css`
- Modify: `Orofoods.Web/wwwroot/css/portal-actions.css`
- Modify: `Orofoods.Web/wwwroot/css/portal-dark.css`
- Test: `Orofoods.Web.Tests/Views/CustomerPortalVisualRefinementTests.cs`
- Test: `Orofoods.Web.Tests/Views/CatalogLayoutTests.cs`
- Test: `Orofoods.Web.Tests/Views/OrderHistoryPresentationTests.cs`
- Test: `Orofoods.Web.Tests/Views/PortalCustomerVisualStandardizationTests.cs` (new)

**Interfaces:** Consumes existing `PortalController` view models, form actions and light/dark body classes; produces compact cards, readable prices and filters, semantic button states, responsive order/draft layouts and local overflow handling.

- [x] **Step 1: Add failing contracts** `Dashboard_exposes_the_requested_portal_shortcuts` (assert links to Catalog, SavedOrders, Cart and Orders), `Cart_keeps_empty_state_checkout_and_update_actions` (assert Catalog, Checkout, UpdateCart and RemoveFromCart), `Orders_and_saved_drafts_keep_filters_and_post_actions` (assert order query/status filters and LoadSavedOrder/EditSavedOrder/DeactivateSavedOrder), `Portal_order_and_draft_layout_has_mobile_rules_for_long_content` (assert scoped mobile rules at the existing breakpoints), and `Portal_primary_buttons_keep_white_aa_contrast_in_all_interactive_states` (calculate at least 4.5:1 for normal, hover and focus foreground/background pairs) in `PortalCustomerVisualStandardizationTests`.
- [x] **Step 2: Run those five tests** with `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --filter "FullyQualifiedName~PortalCustomerVisualStandardizationTests"`; confirm they fail on the current view/style contracts.
- [x] **Step 3: Make the smallest Razor/CSS changes** in the listed files to add the requested dashboard shortcuts and improve spacing, contrast, search controls, product cards, cart empty state, order rows and draft actions; keep all existing `asp-action`, inputs and model values.
- [x] **Step 4: Run `PortalCustomerVisualStandardizationTests`, `CustomerPortalVisualRefinementTests`, `CatalogLayoutTests`, `PortalDarkThemeTests` and `OrderHistoryPresentationTests`; confirm the new and existing theme/action contracts pass.**
- [x] **Step 5: Commit** as `fix: refine customer portal visual consistency`.

### Task 3: Admin Integrations, B2B Customers, Stock and Edit Seller

**Files:**
- Modify: `Orofoods.Web/Areas/Admin/Views/Integrations/Index.cshtml`
- Modify: `Orofoods.Web/Areas/Admin/Views/Customers/Index.cshtml`
- Modify: `Orofoods.Web/Areas/Admin/Views/Inventory/Index.cshtml`
- Modify: `Orofoods.Web/Areas/Admin/Views/SalesRepresentatives/Edit.cshtml`
- Modify: `Orofoods.Web/wwwroot/css/admin-integrations.css`
- Modify: `Orofoods.Web/wwwroot/css/admin-customers.css`
- Modify: `Orofoods.Web/wwwroot/css/admin-shell.css`
- Modify: `Orofoods.Web/wwwroot/css/admin-registrations.css`
- Modify: `Orofoods.Web/wwwroot/css/primary-action-buttons.css`
- Test: `Orofoods.Web.Tests/Views/AdminVisualStandardizationTests.cs` (new)
- Test: `Orofoods.Web.Tests/Views/AdminDarkThemeContractTests.cs`
- Test: `Orofoods.Web.Tests/Views/AdminPrimaryActionTests.cs`
- Test: `Orofoods.Web.Tests/Views/IntegrationDashboardViewTests.cs`

**Interfaces:** Consumes existing Admin view models, action forms, status classes and `.admin-authenticated` theme root; produces consistent compact filters/tables/forms and responsive actions without changing Admin routes or operations.

- [x] **Step 1: Add failing contracts** `Integrations_keeps_status_error_and_operational_actions` (assert filters, status, error modal and existing routes), `B2b_customer_list_keeps_filters_status_and_row_actions`, `Inventory_keeps_adjustment_and_wmc_controls` (assert Adjust, ResumeWmcStockSync and ReleaseWmcInitialLoad remain), `Seller_edit_keeps_bound_fields_and_save_cancel_actions`, and `Admin_visual_rules_are_scoped_and_include_mobile_breakpoints` in `AdminVisualStandardizationTests`.
- [x] **Step 2: Run those five tests** with `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --filter "FullyQualifiedName~AdminVisualStandardizationTests"`; confirm they expose the current visual-contract gaps.
- [x] **Step 3: Make scoped visual changes** in the listed Razor/CSS files; retain all current status semantics, form fields, buttons, routes and WMC controls.
- [x] **Step 4: Run `AdminVisualStandardizationTests`, `AdminDarkThemeContractTests`, `AdminPrimaryActionTests` and `IntegrationDashboardViewTests`; confirm all pass.**
- [x] **Step 5: Commit** as `fix: standardize selected admin screens`.

### Task 4: Password recovery presentation using the Identity UI handler

**Files:**
- Create: `Orofoods.Web/Areas/Identity/Pages/Account/ForgotPassword.cshtml`
- Modify: `Orofoods.Web/wwwroot/css/customer-experience.css`
- Test: `Orofoods.Web.Tests/Views/IdentityForgotPasswordVisualTests.cs` (new)

**Interfaces:** Uses the package page model `Microsoft.AspNetCore.Identity.UI.V5.Pages.Account.Internal.ForgotPasswordModel` and its existing `OnPostAsync`; creates no code-behind and changes no token, email, validation or account-enumeration behavior. The local view sets `identity-recovery-page`, reuses existing `auth-*` components and keeps the shared site footer visible.

- [x] **Step 1: Add failing tests** `Forgot_password_view_uses_identity_package_model_and_validated_email_post` (assert the package model, `method="post"`, email binding, validation summary and field validation) and `Forgot_password_view_uses_responsive_auth_components_pt_br_copy_and_site_footer` (assert the page scope, Portuguese copy, existing auth components and that it does not use `auth-screen`).
- [x] **Step 2: Run those tests** with `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --filter "FullyQualifiedName~IdentityForgotPasswordVisualTests"`; confirm they fail because the local page override and visual contract do not yet exist.
- [x] **Step 3: Create the Razor-only page override** using the package model above and the existing `auth-*` presentation components; style it with `.identity-recovery-page` rules in `customer-experience.css`, without applying `auth-screen` rules that hide the shared footer.
- [x] **Step 4: Run `IdentityForgotPasswordVisualTests` and `IdentityLoginValidationTests`; confirm the page contract and existing login validation pass.**
- [x] **Step 5: Commit** as `fix: refine identity password recovery presentation`.

### Task 5: Release verification and branch review

**Files:** No additional product files; review the combined changes from Tasks 1–4.

- [x] **Step 1: Run focused visual suites** for navigation, customer portal, Admin and Identity at once with `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --filter "FullyQualifiedName~AuthenticatedNavigationTests|FullyQualifiedName~GlobalNavigationTests|FullyQualifiedName~SellerPortalVisualContractTests|FullyQualifiedName~PortalCustomerVisualStandardizationTests|FullyQualifiedName~CustomerPortalVisualRefinementTests|FullyQualifiedName~CatalogLayoutTests|FullyQualifiedName~PortalDarkThemeTests|FullyQualifiedName~OrderHistoryPresentationTests|FullyQualifiedName~AdminVisualStandardizationTests|FullyQualifiedName~AdminDarkThemeContractTests|FullyQualifiedName~AdminPrimaryActionTests|FullyQualifiedName~IntegrationDashboardViewTests|FullyQualifiedName~IdentityForgotPasswordVisualTests|FullyQualifiedName~IdentityLoginValidationTests"`.
- [x] **Step 2: Run `dotnet build Orofoods.slnx -c Release`.**
- [x] **Step 3: Run `git diff --check` and review the complete diff** to confirm only scoped Razor/CSS and visual tests changed, with no business, database or integration edits.
- [x] **Step 4: Assess browser validation only if the local environment is safe and browser automation is available; do not create, save or submit orders. Browser rendering was not performed because a safe local runtime and browser automation were not available.**
- [x] **Step 5: Confirm the branch is clean and report commits, changed files, test/build results and any visual limitations.**
