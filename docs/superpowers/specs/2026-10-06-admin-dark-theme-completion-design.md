# Orofoods Admin Dark Theme Completion

Date: 2026-10-06

## Objective

Complete the Admin dark theme across every Admin view outside the WhatsApp CRM, using the approved Dashboard as the visual reference. The same pass includes the Admin footer approved separately. Keep public pages and the customer Portal unchanged.

## Current state

- `_Layout.cshtml` sets `data-theme="dark"` for Admin routes. `_AdminLayout.cshtml` adds `body.admin-authenticated` and loads the Admin stylesheets.
- The Admin area contains 42 `.cshtml` files: 40 page files across 19 module folders (including one WhatsApp view) plus two shared view setup files. Audit all **39 non-WhatsApp views across 18 modules**; the Dashboard is the approved reference and is excluded from visual edits, leaving 38 views across 17 modules as correction candidates.
- Dark styling is currently concentrated in the Dashboard, Admin shell, and portions of Users. Other views still use light surfaces from `site.css`, Bootstrap, or module CSS.
- The stylesheet order is significant: global `site.css`, `commercial.css`, and `header.css` load before Admin CSS. `_AdminLayout.cshtml` then loads module stylesheets, and each view may append styles through its `Head` section.
- The shared footer lives in `_Layout.cshtml`. `site.css` supplies its public colors and layout; `header.css` sets a large logo size. Portal and customer experience styles are loaded conditionally and are outside this change.

## Scope

Apply dark styling to Admin shell and all Admin modules other than WhatsApp CRM:

- Shared navigation, account dropdown, and footer.
- Commercial, Customers, Categories, Drivers, Driver Payment Terminal Assignments, Integrations, Inventory, Notifications, Opportunities, Orders, Payment Terminals, Payment Terms, Price Tables, Products, Reports, Sales Representatives, and Users.
- The Dashboard remains the approved reference and is not redesigned. No Dashboard view or stylesheet changes are planned.

Do not change Portal views or styles, WhatsApp/CRM views or styles, business behavior, controllers, ViewModels, database configuration, migrations, or CI/CD.

## Approved visual system

- Canvas: `#0F172A`
- Card/surface: `#111827`
- Secondary surface: `#1E293B`
- Borders: `#334155`
- Primary text: `#E5E7EB` / `#F8FAFC`
- Secondary text: `#94A3B8` / `#CBD5E1`
- Informational links and focus ring: `#38BDF8`
- Orofoods accent: `#F59E0B`
- Success: `#10B981`
- Preserve semantic red danger states and the existing primary button color.

## Design

### Theme boundaries and CSS ownership

Keep one set of dark theme tokens in `admin-shell.css`. Admin-specific rules must be gated by both `html[data-theme="dark"]` and `body.admin-authenticated`; module rules must also use their component or page classes. Exclude the WhatsApp inbox from Admin visual overrides so this work does not change CRM/WhatsApp.

Keep styles with the files that own the components: shell/footer in `admin-shell.css`, navigation in `admin-navigation.css`, account dropdown in `header.css`, orders in `admin-orders.css`, and other modules in their existing CSS files. Add a narrowly named module stylesheet only for views without an appropriate existing owner. Do not add a blanket rule that turns all `.card`, `.table`, `.modal`, or form controls dark across the site.

Where light rules are loaded later or have equal/higher specificity, add a scoped selector in the owning stylesheet and document the cascade reason. Avoid `!important` except where a confirmed Bootstrap rule requires it.

### Shared navigation and account menu

Improve inactive navigation link/icon contrast, hover and focus surfaces, active item contrast, group labels, and chevrons while retaining the existing sidebar dimensions and active accent. Style only the account dropdown rendered by the shared site header in Admin dark mode; cover links, icons, logout, divider, hover, and keyboard focus. Leave public and Portal dropdowns unchanged.

### Orders

Apply the dark palette to all Admin order list and detail surfaces, filters, forms, tables, badges, status history, action groups, WMC areas, and modals present in the existing views. Preserve routes, IDs, JavaScript hooks, status meaning, button semantics, and existing responsive table behavior. Keep main actions in their existing primary color.

### Remaining Admin views

Audit each in-scope view against the loaded CSS and correct only light surfaces/text/control states that remain. Reuse module stylesheets and common tokens. Keep non-dashboard layout geometry and functional behavior unchanged. Include empty states, validation messages, focus/hover/disabled states, and responsive layouts in the audit.

### Admin footer

Keep the shared footer markup and public `site.css` rules unchanged. Add compact dark colors, a subtle top border, smaller Admin-only logo sizing, restrained accent icons, accessible link hover/focus states, and compact responsive spacing. Scope footer rules to Admin dark mode and exclude the WhatsApp inbox.

## Responsive and accessibility requirements

Verify desktop, tablet, and mobile layouts. Preserve current sidebar/offcanvas operation and existing horizontal table wrappers. Keep visible keyboard focus and readable text contrast. Status colors must remain distinguishable on dark surfaces.

## Tests and acceptance

- Add structural tests for Admin-scoped dark navigation, account dropdown, Orders surfaces, and footer rules.
- Assert that public footer styles remain present in `site.css` and that Admin dark selectors require the Admin body scope.
- Run `git diff --check` and `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release` after implementation. The current reference is 669 passing tests; new tests may increase the total.
- Review the final diff to confirm the Dashboard was not redesigned and that no business, database, migration, Portal, CRM/WhatsApp, or CI/CD files changed.

## Delivery boundaries

Work only in the isolated Admin worktree. Do not commit, push, open a PR, or deploy. Report changed files, cascade findings, responsive review, both validation results, remaining risks, and explicit scope confirmations. Number the updated task sections as: footer 4, CSS/cascade 5, responsiveness 6, tests 7, final report 8. Include report bullets for Admin footer changes and confirmation that the public footer was not changed.
