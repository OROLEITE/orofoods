# API and Integration Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Provide a versioned mobile-ready API and ERP integration foundation while preserving MVC routes and PostgreSQL.

**Architecture:** Add `/api/v1` controllers that return DTOs rather than EF entities. Create provider-neutral integration contracts and persist ERP state on orders; concrete ERP transport remains disabled until the WMC layout is supplied.

**Tech Stack:** ASP.NET Core MVC 10, EF Core 10, PostgreSQL, ASP.NET Core Identity, JWT bearer authentication.

## Global Constraints

- Preserve public, portal and admin MVC routes.
- Use PostgreSQL migrations for persisted model changes.
- Do not send data to an ERP without a configured provider.
- Protect all API resources except token issuance and health checks.

### Task 1: API Foundation

**Files:** Create API DTOs and `/api/v1` catalog, category, customer, order, pricing and payment controllers; modify `Program.cs` and project packages.

- [ ] Add a failing integration test for versioned API routing and authenticated access.
- [ ] Add JWT bearer authentication, API rate limiting and controller mapping.
- [ ] Expose DTO-based read endpoints scoped to the authenticated customer.
- [ ] Run API tests and existing test suite.

### Task 2: ERP Integration State

**Files:** Modify `Order`; create integration contracts, services and entities; add PostgreSQL migration.

- [ ] Add a failing model test for ERP order state and retry metadata.
- [ ] Add provider-neutral ERP contracts and a disabled default implementation.
- [ ] Persist order integration status, external IDs, timestamps and failures.
- [ ] Run migrations and tests against PostgreSQL.

### Task 3: Operational Readiness

**Files:** Modify startup and documentation.

- [ ] Add health checks for application and PostgreSQL.
- [ ] Document environment variables and API authentication configuration.
- [ ] Build Release and run all tests.
