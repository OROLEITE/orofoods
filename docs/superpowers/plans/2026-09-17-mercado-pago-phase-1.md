# Mercado Pago Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a testable, secure, idempotent Mercado Pago Checkout Transparente integration using the current Orders API without generating or executing a database migration.

**Architecture:** Extend the existing `Payment` entity into an attempt record, isolate Mercado Pago HTTP/JSON behind `IPaymentGateway`, and coordinate persistence through `PaymentOrchestrationService`. Validate signed webhooks before re-querying the authoritative Order and applying idempotent local status changes.

**Tech Stack:** ASP.NET Core 10, EF Core 10, Npgsql, SQLite test databases, `IHttpClientFactory`, `System.Text.Json`, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-17-mercado-pago-phase-1-design.md`

## Global Constraints

- Use Checkout Transparente and the Mercado Pago Orders API only.
- Preserve all existing migrations, including `NormalizePaymentTermOrdering` unchanged.
- Do not generate or execute any migration or database update.
- Preserve numeric values 0 through 5 of `PaymentStatus`; new values must be explicit and greater than 5.
- Do not store or log an access token, webhook secret, card token, full card number, or CVV.
- Do not write to WMC or use WMC order flags as financial locks.
- Do not commit, push, or deploy.

---

### Task 1: Payment domain and status mapping

**Files:**
- Modify: `Orofoods.Web/Models/Payments/Payment.cs`
- Modify: `Orofoods.Web/Models/Payments/PaymentStatus.cs`
- Create: `Orofoods.Web/Models/Payments/PaymentMethodType.cs`
- Create: `Orofoods.Web/Services/Payments/MercadoPagoStatusMapper.cs`
- Modify: `Orofoods.Web/Data/ApplicationDbContext.cs`
- Create: `Orofoods.Web.Tests/Services/MercadoPagoStatusMapperTests.cs`
- Create: `Orofoods.Web.Tests/Models/PaymentModelTests.cs`

**Interfaces:**
- Produces: `PaymentMethodType`, preserved/new `PaymentStatus` values, and `MercadoPagoStatusMapper.Map(string status, string? detail)`.

- [ ] Write tests asserting every legacy numeric enum value, new status values, model metadata, and literal status mappings.
- [ ] Run the focused tests and confirm failure because members and mapper are absent.
- [ ] Add the minimal model fields, explicit enum values, mapper, and EF indexes required by the tests.
- [ ] Run the focused tests and confirm success.

### Task 2: Orders API gateway

**Files:**
- Create: `Orofoods.Web/Services/Payments/IPaymentGateway.cs`
- Create: `Orofoods.Web/Services/Payments/MercadoPagoOptions.cs`
- Create: `Orofoods.Web/Services/Payments/MercadoPagoApiModels.cs`
- Create: `Orofoods.Web/Services/Payments/MercadoPagoPaymentGateway.cs`
- Create: `Orofoods.Web.Tests/Services/MercadoPagoPaymentGatewayTests.cs`

**Interfaces:**
- Produces: `CreatePixAsync`, `CreateCreditCardPaymentAsync`, `GetOrderAsync`, and `RefundAsync` with gateway-neutral request/result records.
- Consumes: `PaymentStatus` through `MercadoPagoStatusMapper`.

- [ ] Write handler-backed tests for the exact Orders API paths, Bearer authorization, idempotency headers, PIX/card JSON, response parsing, GET, refund, and sanitized exceptions.
- [ ] Run the focused tests and confirm failure because the gateway does not exist.
- [ ] Implement the typed options, minimal JSON contracts, safe exception, and `HttpClient` gateway.
- [ ] Run the focused tests and confirm success.

### Task 3: Webhook signature validation

**Files:**
- Create: `Orofoods.Web/Services/Payments/IMercadoPagoWebhookSignatureValidator.cs`
- Create: `Orofoods.Web/Services/Payments/MercadoPagoWebhookSignatureValidator.cs`
- Create: `Orofoods.Web.Tests/Services/MercadoPagoWebhookSignatureValidatorTests.cs`

**Interfaces:**
- Produces: `bool IsValid(string signature, string requestId, string dataId)` using options-provided secret and a bounded timestamp tolerance.

- [ ] Write literal-vector tests for valid, invalid, malformed, stale, and case-sensitive `data.id` signatures.
- [ ] Run tests and confirm failure because the validator is absent.
- [ ] Implement official manifest parsing, HMAC-SHA256, and `CryptographicOperations.FixedTimeEquals`.
- [ ] Run the focused tests and confirm success.

### Task 4: Payment orchestration and webhook reconciliation

**Files:**
- Create: `Orofoods.Web/Services/Payments/IPaymentApprovalHandler.cs`
- Create: `Orofoods.Web/Services/Payments/NoOpPaymentApprovalHandler.cs`
- Create: `Orofoods.Web/Services/Payments/PaymentOrchestrationService.cs`
- Create: `Orofoods.Web.Tests/Services/PaymentOrchestrationServiceTests.cs`

**Interfaces:**
- Produces: authenticated PIX/card creation, payment lookup, and `ReconcileMercadoPagoOrderAsync`.
- Consumes: `ApplicationDbContext`, `IPaymentEligibilityService`, `IPaymentGateway`, `TimeProvider`, and the no-op approval hook.

- [ ] Write SQLite-backed tests for authoritative amount/ownership, same-key retry reuse, rejected payment, expired PIX, duplicate webhook, and one-time `PaidAt`.
- [ ] Run tests and confirm failure because orchestration is absent.
- [ ] Implement persistence-before-network, gateway result application, idempotent reconciliation, and safe validation.
- [ ] Run the focused tests and confirm success.

### Task 5: HTTP endpoints and safe checkout boundary

**Files:**
- Create: `Orofoods.Web/Controllers/Api/V1/PaymentsController.cs`
- Create: `Orofoods.Web/Controllers/Api/V1/MercadoPagoWebhooksController.cs`
- Create: `Orofoods.Web/ViewModels/PaymentViewModels.cs`
- Modify: `Orofoods.Web/ViewModels/CheckoutViewModel.cs`
- Modify: `Orofoods.Web/Views/Portal/Checkout.cshtml`
- Modify: `Orofoods.Web/wwwroot/js/checkout.js`
- Create: `Orofoods.Web.Tests/Controllers/PaymentsControllerTests.cs`
- Create: `Orofoods.Web.Tests/Controllers/MercadoPagoWebhooksControllerTests.cs`
- Modify: `Orofoods.Web.Tests/Views/CheckoutViewLayoutTests.cs`

**Interfaces:**
- Produces: customer-owned payment create/read routes and public signed webhook route.
- Consumes: only card token, payment-method ID, installments, and order ID from card requests.

- [ ] Write controller/view tests proving ownership, signature rejection, authoritative GET reconciliation, duplicate success, and absence of raw card/CVV model fields.
- [ ] Run tests and confirm the expected failures.
- [ ] Implement DTOs, controllers, and minimal browser token boundary compatible with MercadoPago.js.
- [ ] Run focused tests and confirm success.

### Task 6: Configuration and dependency injection

**Files:**
- Modify: `Orofoods.Web/Program.cs`
- Modify: `Orofoods.Web/appsettings.json`
- Create: `Orofoods.Web.Tests/Configuration/MercadoPagoConfigurationTests.cs`

**Interfaces:**
- Produces: validated `MercadoPagoOptions`, named `HttpClient`, gateway/orchestrator/validator registrations, and no tracked secret values.

- [ ] Write configuration tests for registrations, official base address, and absence of tracked secret values.
- [ ] Run tests and confirm expected failures.
- [ ] Add DI and only non-secret configuration structure.
- [ ] Run focused tests and confirm success.

### Task 7: Eligibility coverage and SQLite schema synchronization

**Files:**
- Modify: `Orofoods.Web.Tests/Services/PaymentEligibilityServiceTests.cs`
- Modify: `Orofoods.Web.Tests/Data/OrderStatusHistoryMigrationTests.cs`
- Modify: `Orofoods.Web.Tests/Data/WmcExportAuditMigrationTests.cs`

**Interfaces:**
- Consumes: current EF Core model through `EnsureCreatedAsync`.

- [ ] Add missing literal eligibility cases for PIX, card, boleto 7/14, blocked credit, and terms over 14 days.
- [ ] Change disposable SQLite schema setup in the two failing tests to `EnsureCreatedAsync` without changing migrations.
- [ ] Run relevant tests and confirm all eligibility/schema cases pass.

### Task 8: Verification and migration proposal

**Files:**
- Inspect all modified and created files; do not create a migration.

**Interfaces:**
- Produces: build/test/diff evidence and the requested PostgreSQL migration preview.

- [ ] Run `dotnet build --no-restore`.
- [ ] Run all Mercado Pago, payment, eligibility, controller, view, and schema tests.
- [ ] Run the complete test suite.
- [ ] Run `git diff --check`.
- [ ] Confirm no migration file, credential, WMC write, commit, push, or deploy was created.
- [ ] Report provider, proposed migration name, tables, columns, types/nullability/defaults, indexes, foreign keys, historical data impact, and approximate PostgreSQL SQL.
