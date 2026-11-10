# Admin Assisted Order Customer Search Implementation Plan

> **For agentic workers:** Execute each task with test-first changes and verify before moving on.

**Goal:** Open Admin order entry directly and select an authorized customer through a bounded, accent-insensitive autocomplete.

**Architecture:** Keep `CustomersController.NewOrder` and `AssistedOrderService` as the order path. Add a scoped, server-paged customer-search action and use a page-level JavaScript controller for accessible keyboard/mouse selection. Selecting a result navigates to the existing order-entry GET, which reloads the server-authorized customer, representative, address, pricing, products, and payment terms.

**Tech Stack:** ASP.NET Core MVC, EF Core, PostgreSQL/SQLite search translation, Razor, JavaScript, xUnit/Jint.

## Global Constraints

- Preserve seller customer scope, pricing, stock, payment, minimum-order, antiforgery, and submission behavior.
- Keep customer administration listing available and remove it only as a required order-entry step.
- Preserve WMC pending-import navigation and Admin, Seller, and Operator permissions.
- Do not change schema, database/Azure configuration, real orders, or WMC transmission behavior.

## Review Focus

- Search filters by trade/legal name, CNPJ, and WMC code; ignores case and accents; returns only authorized active approved customers with a fixed page limit.
- Keyboard Enter and mouse selection navigate to the existing scoped order GET.
- Existing server validations and WMC import action/permissions remain intact.

## Tasks

- [ ] Add failing controller tests for accent-insensitive authorized search, filters, page bounds, and direct no-customer GET.
- [ ] Add failing view/JavaScript tests for direct Admin navigation, keyboard/mouse selection, and product-field focus after selection.
- [ ] Implement the scoped paginated endpoint and direct order-entry start state without changing the administration list.
- [ ] Implement accessible autocomplete, keyboard selection, mouse selection, pagination, and initial product focus.
- [ ] Run focused tests and WMC authorization tests; run the full requested Release build and focused test set; inspect `git diff --check` and final diff.
