# PostgreSQL and Transactional Layout Design

## Objective

Make PostgreSQL the official application database for Orofoods, retain SQLite only for automated tests, and reduce the vertical footprint of transactional page banners.

## Scope

- Replace the production EF Core provider from SQLite to Npgsql PostgreSQL.
- Store the production connection string outside source control through environment configuration or user secrets.
- Recreate provider-specific EF Core migrations for PostgreSQL.
- Provide a repeatable data export/import path from the current SQLite database.
- Keep isolated SQLite in-memory tests unchanged where practical.
- Apply a compact hero variant to cart and checkout pages.

## Database Architecture

`ApplicationDbContext` remains the sole persistence boundary. `Program.cs` configures it with `UseNpgsql` using `ConnectionStrings:DefaultConnection`. The default checked-in configuration contains no production credentials; a PostgreSQL example is documented in configuration guidance.

The existing migration history is SQLite-specific because it carries SQLite annotations. A new PostgreSQL migration baseline will be generated from the current model. Existing SQLite migrations remain as historical reference and are not applied to PostgreSQL.

## Data Migration

1. Stop writes to the current SQLite deployment.
2. Create a backup copy of the SQLite database.
3. Create an empty PostgreSQL database and apply the PostgreSQL baseline migration.
4. Import rows in dependency order: identity roles/users, commercial masters, catalog, customer relationships, orders, and auxiliary records.
5. Validate row counts, administrator access, customer access, catalog images, and an order checkout before switching the connection string.
6. Retain the SQLite backup until the PostgreSQL deployment is accepted.

The application code does not run an automatic destructive database conversion at startup. Migration execution and data import are explicit operational steps.

## Configuration and Operations

- Development and production use `ConnectionStrings__DefaultConnection`.
- Local developers may use user secrets; deployment uses environment variables or managed secrets.
- PostgreSQL runs with a dedicated database role limited to the Orofoods database.
- Backups and restore tests are deployment responsibilities documented alongside the connection setup.

## UI Adjustment

Cart and checkout use a `page-hero--compact` modifier. The compact variant reduces vertical padding while preserving the eyebrow, heading, and contextual navigation. Marketing pages retain the larger hero treatment.

## Verification

- Build with the Npgsql provider configured.
- Run all automated tests using SQLite in-memory providers.
- Apply the PostgreSQL migrations to an empty PostgreSQL database.
- Import a representative SQLite backup and verify counts and foreign-key integrity.
- Run customer login, add-to-cart, checkout, and administrator access smoke tests.
- Verify cart and checkout at desktop and mobile widths with no horizontal overflow.

## Non-Goals

- No payment gateway integration.
- No ERP WMC export implementation.
- No automatic production database migration during normal application startup.
