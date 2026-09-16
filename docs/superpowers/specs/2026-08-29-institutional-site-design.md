# Orofoods Institutional Site - Design

## Objective

Complete Phase 2 with a responsive public institutional site that explains
Orofoods, exposes a non-commercial product showcase, receives contact
requests, and provides legal pages. Commercial prices and ordering remain
restricted to the approved-customer portal.

## Scope

The public experience will provide these routes through `HomeController`:

| Route | Purpose |
| --- | --- |
| `/` | Existing institutional landing page, updated to link to the public routes. |
| `/produtos` | Searchable public product showcase without prices. |
| `/produtos/{id}` | Product details, availability and commercial call to action. |
| `/sobre` | Orofoods positioning, operation and service differentiators. |
| `/como-funciona` | B2B registration-to-delivery process. |
| `/contato` | Contact form that persists a request after server-side validation. |
| `/privacidade` | Initial privacy policy for registration, contact and portal data. |
| `/termos` | Initial terms of use for the public site and B2B portal. |

## Architecture

`HomeController` remains responsible for public-page orchestration. It will
read active products and product categories from `ApplicationDbContext` and
will not use `PriceService`; public pages must never resolve or render a
customer price.

A new `ContactMessage` model stores submitted requests: name, company,
phone, WhatsApp, email, city, message, consent timestamp and creation time.
`ContactViewModel` carries form validation rules and prevents over-posting.
The POST action checks `ModelState`, persists only the approved fields and
redirects with a one-time success message. No external e-mail, CRM or paid
API integration is included in this phase.

The database change is an additive EF Core migration adding `ContactMessages`.
No existing tables or columns are removed.

## Presentation and Navigation

The existing gold, graphite and cream design tokens, Manrope and DM Sans
typography, and Bootstrap-based mobile breakpoint remain the visual system.
The shared layout will provide navigation for Inicio, Produtos, Sobre nos,
Como funciona, Contato and the authenticated customer area. The footer will
repeat useful institutional and legal links.

The public products page will display product identity, category, packaging,
description and availability. A product detail page may show storage and
ingredient information when present. Both direct visitors to registration or
login for commercial catalog access instead of revealing prices.

Each data-backed public view includes a friendly empty state. Contact form
validation errors stay next to the affected field and a successful submission
shows a confirmation state without resubmitting on refresh.

## Security and Privacy

All contact submissions use anti-forgery validation and server-side data
annotations. The form requires acceptance of the privacy policy; consent is
stored with the request. Razor encoding is retained for all displayed data.
Legal pages state that the content is an initial operational policy and cover
the minimum collection required for B2B registration, portal access and
contact handling.

## Tests and Verification

Tests will be introduced before production behavior for:

- Rejecting a contact submission without privacy consent.
- Persisting a valid contact request with its consent timestamp.
- Returning only active products from the public showcase query.
- Refusing an unavailable or inactive product detail route.

The phase is verified with the focused test suite, `dotnet test`,
`dotnet build Orofoods.slnx`, and applying migrations to a new temporary
SQLite database. Responsive behavior will be checked at desktop and mobile
widths through the rendered views and existing Bootstrap breakpoints.

## Out of Scope

- Sending e-mail, WhatsApp messages or CRM records.
- Commercial prices, cart operations or checkout for anonymous visitors.
- Product administration, image upload and favorites.
- Legal review by counsel or third-party policy generators.
