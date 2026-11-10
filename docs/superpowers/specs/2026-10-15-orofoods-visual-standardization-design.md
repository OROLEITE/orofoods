# Orofoods Visual Standardization Design

## Goal

Correct the visual inconsistencies described in the supplied prompt across the customer portal, selected Admin pages, shared authenticated navigation and the password-recovery screen. Preserve existing routes, content, form bindings, actions and application behavior.

## Scope and reference

The scope is the existing Razor experience for:

- Customer dashboard, product catalog, cart, customer orders and saved order drafts.
- Shared header, navigation and logo presentation used by authenticated areas.
- Admin ERP Integrations, B2B Customers, Stock and Edit Seller screens.
- Public password recovery, limited to its own form presentation and wording.

The prompt text is available, but its referenced `AjustarSis.docx` and screenshots were not present in the supplied attachment directory or repository. The textual requirements are therefore the implementation reference; screenshot-to-screenshot comparison remains unavailable unless those images are later provided.

## Design approach

Keep the existing Razor layouts and theme system. Correct each page through its existing view and area-specific stylesheets, adding only small page-scoping classes where the current markup cannot safely distinguish a surface. Reuse existing logo assets and shared header components. Use shared palette rules only under the authenticated Portal/Admin roots; isolate password-recovery styles to that page. Do not add a new UI framework, replace layouts, or create broad public-site overrides.

Use the existing light/dark theme state. In dark mode, authenticated surfaces use the supplied palette: background `#0B132B` or `#0F172A`, cards `#111827`, secondary surfaces `#1E293B`, primary text `#F8FAFC`, secondary text `#CBD5E1`, borders `#334155`, primary action `#0E7490` with white text and a visible focus state. Do not use yellow/gold as the primary color for operational actions. Preserve existing light-mode presentation and public pages, except for the isolated password-recovery form. Keep action semantics: primary, secondary, destructive and informational states remain distinct.

## Surface behavior

Customer pages receive compact, aligned cards, search controls, price/unit presentation, order tables and draft actions while retaining all current search, filters, pagination, cart, order and draft interactions. Authenticated navigation and official logo sizing, transparency, proportions and contrast become consistent without changing menu destinations or mobile behavior. Admin forms and tables receive consistent control heights, contrast, status presentation, column alignment and mobile treatment without changing any administrative action. Password recovery receives a centered, responsive Portuguese-language presentation while ASP.NET Identity validation, token handling and submission remain unchanged.

## Responsive and accessibility requirements

Review at 1440px, 768px, 430px, 390px and 360px. Prevent document-level horizontal overflow, clipped controls and overlapping actions; use an appropriate local responsive table treatment where a table cannot fit. Keep touch targets at least 44px where practical, preserve keyboard access and visible focus, and keep labels and accessible names intact. Normal text must meet WCAG AA contrast of at least 4.5:1; meaningful non-text controls must meet 3:1. Status must not rely on color alone.

## Boundaries and validation

Changes are limited to Razor markup needed for visual scoping and CSS. Do not change controllers, models, services, authorization, Identity flow, business calculations, payment, inventory, WMC, database, migrations or integrations. Add or update focused view/style contract tests for the pages changed, run the relevant suites and a Release build, and run `git diff --check`. Perform browser visual checks only if a safe local app and browser automation are available; do not submit or save orders.

## Completion evidence

Report the changed files grouped by screen, focused and Release validation results, responsive/browser coverage actually achieved, any unresolved visual references, and the local commit. Do not push, open a PR, merge or deploy.
