# Administrative navigation and state recovery design

**Status:** Approved for a later, separate implementation
**Scope:** Administrative MVC/Razor navigation only; excluded from the WMC worktree implementation

## Intent

Reduce accidental loss of administrative filters, selections, and unsent form work while preserving the current server-rendered MVC/Razor contracts. The design must not turn navigation into an implicit write, weaken authentication or authorization, bypass antiforgery, or alter commercial, WMC, order, or database rules.

## Current architecture

- `_AdminLayout.cshtml` wraps each administrative response with the fixed sidebar and shared layout.
- `_AdminNavigation.cshtml` emits ordinary role-aware links; the active item is computed from the current route.
- `admin-navigation.js` currently persists only sidebar collapse state.
- Administrative GET actions use route/query parameters for filters and paging. POST actions use normal MVC model binding, antiforgery validation, authorization, and redirects or validation views.
- There is no generic internal-tab host, fragment contract, draft store, or browser automation suite dedicated to navigation.

## Alternatives

### Alternative 1 — Internal application tabs

Keep several server-rendered screens mounted in an application tab workspace, using either iframes or a new fragment/AJAX contract.

Benefits:

- Closest to an ERP-style experience.
- The live DOM can preserve filters, selections, and unsent forms while the tab remains open.

Costs and risks:

- Requires a tab registry, canonical tab keys, close confirmation, focus/accessibility behavior, responsive tab layout, and history rules.
- Iframes duplicate the layout and scripts; fragment/AJAX loading requires changes to many views, redirects, validation responses, downloads, and page-specific JavaScript.
- Antiforgery and authorization must remain server-enforced for every form submission; no client-side tab action may submit a form.
- Requires browser-level tests for navigation, dirty forms, responsive behavior, redirects, and authentication expiry.

### Alternative 2 — Conventional navigation with explicit browser-tab choice (selected)

Keep normal MVC/Razor navigation and add a clear same-tab/new-browser-tab choice for safe GET destinations. Preserve filters in query strings where possible and add opt-in `sessionStorage` recovery only for explicitly whitelisted, non-sensitive state.

Rules:

- Never persist antiforgery tokens, passwords, secrets, or sensitive form payloads.
- Do not intercept or auto-submit POST forms when changing screens.
- Clear recovered state after successful submission and tolerate storage being unavailable or cleared.
- Use `beforeunload` only to warn about a genuinely changed form; the server remains the source of truth.
- Keep the existing role-aware links, route URLs, authorization, antiforgery, dark mode, fixed menu, and responsive CSS intact.

Benefits:

- Smallest change surface and lowest regression risk for the current MVC application.
- Existing URLs, bookmarks, history, redirects, validation views, and server-side security remain authoritative.
- Browser tabs provide multiple concurrent screens without duplicating the server-rendered layout inside another document.

Limitation: the application does not own a single internal tab strip, and each screen must opt into safe state recovery rather than receiving an unsafe generic form serializer.

## Recommendation and rollout

Implement Alternative 2 first. Start with filter-heavy GET pages and a small set of forms that explicitly declare safe state fields. Add focused JavaScript contract tests and browser validation before expanding the allowlist. If an internal ERP tab strip remains necessary, prototype Alternative 1 on one read-heavy screen only, with a fragment contract and browser tests, before considering broader adoption.

## Explicit non-goals for this design

- No changes to WMC import, stock, activation, prices, units, orders, or synchronization.
- No database schema or data changes.
- No automatic form submission, background save, or client-side authorization.
- No implementation of this design in `review/wmc-import-stock-ui`.
