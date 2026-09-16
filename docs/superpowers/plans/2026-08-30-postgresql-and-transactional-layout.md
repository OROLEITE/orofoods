# PostgreSQL and Transactional Layout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Configure PostgreSQL as the official Orofoods database provider and compact transactional page banners.

**Architecture:** Runtime EF Core uses Npgsql and a PostgreSQL connection string supplied by environment configuration. SQLite stays only in the in-memory test infrastructure. A new migration baseline is generated for PostgreSQL because current migrations contain SQLite annotations.

**Tech Stack:** ASP.NET Core MVC, EF Core 10, Npgsql, PostgreSQL, xUnit, SQLite in-memory tests.

**Spec:** `docs/superpowers/specs/2026-08-30-postgresql-and-transactional-layout-design.md`

## Global Constraints

- Production credentials are not committed.
- Startup does not perform destructive database conversion.
- SQLite remains only in automated tests.
- Cart and checkout use a compact hero; marketing pages remain unchanged.

### Task 1: PostgreSQL Provider

**Files:** Modify `Orofoods.Web/Orofoods.Web.csproj`, `Orofoods.Web/Program.cs`, `Orofoods.Web/appsettings.json`; create `Orofoods.Web/appsettings.Development.example.json`; create `Orofoods.Web.Tests/Configuration/PostgreSqlConfigurationTests.cs`.

- [ ] Write a test asserting project references `Npgsql.EntityFrameworkCore.PostgreSQL`, `Program.cs` calls `UseNpgsql(connectionString)`, and does not call `UseSqlite(connectionString)`.
- [ ] Run `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj --no-restore --filter FullyQualifiedName~PostgreSqlConfigurationTests`; expect failure.
- [ ] Replace the runtime SQLite package with Npgsql provider version compatible with EF Core 10; configure `UseNpgsql(connectionString)`.
- [ ] Use `Host=localhost;Port=5432;Database=orofoods;Username=orofoods;Password=CHANGE_ME` only as an example configuration. Document `ConnectionStrings__DefaultConnection` for runtime secrets.
- [ ] Run the focused test; expect pass.

### Task 2: Migration Runbook

**Files:** Create `docs/database/postgresql-migration.md`, `scripts/import-sqlite-to-postgres.ps1`, `Orofoods.Web.Tests/Configuration/PostgreSqlMigrationArtifactTests.cs`; generate PostgreSQL baseline artifacts under `Orofoods.Web/Data/MigrationsPostgreSql`.

- [ ] Write a test asserting the migration guide and import helper exist; run it and expect failure.
- [ ] Generate a PostgreSQL migration baseline with `dotnet ef migrations add PostgreSqlBaseline --output-dir Data/MigrationsPostgreSql --project Orofoods.Web` while a PostgreSQL connection is supplied.
- [ ] Write the runbook: stop writes, copy SQLite backup, create PostgreSQL database, apply baseline, import in dependency order, validate counts/access/orders, retain backup for rollback.
- [ ] Make the import helper require `-SourceSqlitePath` and `-PostgreSqlConnection`, and refuse a missing source path.
- [ ] Run the focused artifact test; expect pass.

### Task 3: Compact Transactional Heroes

**Files:** Modify `Orofoods.Web/Views/Portal/Cart.cshtml`, `Orofoods.Web/Views/Portal/Checkout.cshtml`, `Orofoods.Web/wwwroot/css/site.css`; create `Orofoods.Web.Tests/Views/TransactionalHeroTests.cs`.

- [ ] Write a theory that requires `page-hero page-hero--compact` in cart and checkout; run it and expect failure.
- [ ] Add `page-hero--compact` to both transactional page hero sections.
- [ ] Add CSS with 42px vertical padding, 34px-to-46px responsive heading, and 12px eyebrow margin.
- [ ] Run the focused view test; expect pass.

### Task 4: Verification

- [ ] Restore packages with `dotnet restore Orofoods.slnx`.
- [ ] Run `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj --no-restore`.
- [ ] Run `dotnet build Orofoods.slnx --no-restore`.
- [ ] Apply the PostgreSQL baseline to an empty PostgreSQL database and run customer login, add-to-cart, checkout, and administrator-access smoke tests before deployment.
