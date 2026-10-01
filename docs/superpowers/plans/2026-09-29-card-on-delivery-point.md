# Cartão na Entrega Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to execute this plan task-by-task. Keep product changes test-first (red, green, refactor) and validate each focused area before moving on.

**Goal:** Add a default-off Card on Delivery payment method for customer and seller-assisted order placement, with a locally recorded pending payment, admin/customer status display, and an explicit WMC export block. No Point terminal or external charge is part of this phase.

**Architecture:** Reuse `PaymentTerm` for selection and `Payment`/`PaymentStatus.Pending` for the local financial record. Add enum value `CardOnDelivery` and feature flag `Payments:CardOnDeliveryEnabled` defaulting to false. Filter the term from eligibility and reject it server-side while disabled. Persist the order and local pending payment in the existing `OrderPlacementService` transaction, which serves both customer and assisted seller flows. Never send this method to the online Mercado Pago orchestration. Explicitly prevent WMC generation with `WMC_CARD_ON_DELIVERY_MAPPING_PENDING`.

**Tech Stack:** ASP.NET Core MVC, EF Core, PostgreSQL/SQLite migration sets, xUnit, existing Mercado Pago and WMC integrations.

### Task 1: Establish regression tests for domain flag, payment record, and WMC guard

**Files:** `Orofoods.Web.Tests/Services/PaymentEligibilityServiceTests.cs`, `Orofoods.Web.Tests/Services/OrderPlacementServiceTests.cs`, `Orofoods.Web.Tests/Integrations/WmcOrderFileGeneratorTests.cs`, plus narrowly scoped implementation files in Tasks 2–3.

1. Add failing eligibility tests: flag off hides and rejects `CARD_ON_DELIVERY`; flag on exposes and accepts it while preserving `CASH` and `CREDIT_CARD` behavior.
2. Add failing placement tests: `CARD_ON_DELIVERY` creates one `Payment` with method `CardOnDelivery`, status `Pending`, correct amount and no gateway metadata; normal methods remain unchanged.
3. Add failing WMC test: a card-on-delivery order is rejected with exact stable marker `WMC_CARD_ON_DELIVERY_MAPPING_PENDING`, even when customer/product mappings exist.
4. Run the focused tests and record expected failures before production edits.

### Task 2: Add payment method feature flag and payment-term seed migration

**Files:** `Orofoods.Web/Models/Payments/PaymentMethodType.cs`, `Orofoods.Web/Services/Customers/PaymentEligibilityService.cs`, `Orofoods.Web/appsettings.json`, `Orofoods.Web/Data/MigrationsPostgreSql/*`, SQLite migration set only if repository conventions require it, and associated tests.

1. Implement the Task 1 eligibility tests first, run red, then add `CardOnDelivery` without renumbering existing enum members.
2. Add `PaymentsOptions.CardOnDeliveryEnabled` (default false) and filter the `CARD_ON_DELIVERY` term when disabled; ensure `ValidateAsync` cannot bypass the flag.
3. Add the configuration section with the safe false default; do not edit deployment settings.
4. Add an idempotent seed migration for `CARD_ON_DELIVERY`, localized display name, zero due days, and stable sort order. This data row is necessary because checkout selects persisted `PaymentTermId`; no new column is needed. Do not run this migration against Staging or Production.
5. Run focused eligibility tests green.

### Task 3: Record local pending payment within order transaction

**Files:** `Orofoods.Web/Services/Orders/OrderPlacementService.cs`, `Orofoods.Web/Services/Payments/PaymentOrchestrationService.cs`, `Orofoods.Web.Tests/Services/OrderPlacementServiceTests.cs`, `Orofoods.Web.Tests/Services/PaymentOrchestrationServiceTests.cs`.

1. Implement Task 1 placement tests first and run red.
2. When the selected persisted term code is `CARD_ON_DELIVERY`, add a `Payment` in the same existing serializable transaction, with no gateway fields or external identifiers. Keep all other payment methods unchanged.
3. Ensure reservation failure and any later persistence failure leave no order or payment partial state.
4. Add a guard at the online orchestration boundary preventing direct PIX/card API attempts for this payment term.
5. Run focused placement and orchestration tests green.

### Task 4: Adapt customer checkout and success display

**Files:** `Orofoods.Web/Controllers/PortalController.cs`, `Orofoods.Web/Views/Portal/Checkout.cshtml`, `Orofoods.Web/Views/Portal/Success.cshtml`, `Orofoods.Web/wwwroot/js/checkout.js`, `Orofoods.Web.Tests/Controllers/PortalControllerCheckoutCardTests.cs` and view tests.

1. Add tests proving the enabled method creates an order, never calls online Mercado Pago, and success output shows “Cartão na entrega” / “Aguardando pagamento” without online CTA. Add a disabled POST rejection test.
2. Confirm card token/payment method fields are validated and submitted only for `CREDIT_CARD`; Card on Delivery requests no card data.
3. Add concise checkout explanatory copy: “Pague no momento da entrega diretamente na maquininha.”
4. Run focused Portal checkout/view tests green.

### Task 5: Adapt seller-assisted checkout

**Files:** `Orofoods.Web/Areas/Vendedor/Controllers/CheckoutController.cs`, `Orofoods.Web/Areas/Vendedor/Views/Checkout/Index.cshtml` (and matching actual checkout view), `Orofoods.Web.Tests/Controllers/SellerCheckoutControllerTests.cs`.

1. Add tests proving Card on Delivery selects the shared local-pending-payment path and never calls Mercado Pago; keep online card flow intact and reject when disabled.
2. Display the same delivery-machine copy and do not solicit card data for this option.
3. Run focused seller checkout tests green.

### Task 6: Show method and pending financial status in Admin

**Files:** `Orofoods.Web/Areas/Admin/Controllers/OrdersController.cs`, `Orofoods.Web/Areas/Admin/Views/Orders/Details.cshtml`, relevant Admin tests.

1. Add a test requiring the order detail to load its local payment row and render the method and pending status.
2. Include payments in the detail query and display “Cartão na entrega” plus “Pagamento pendente” without Point action controls.
3. Run focused Admin tests green.

### Task 7: Block WMC export with explicit pending mapping marker

**Files:** `Orofoods.Web/Integrations/Erp/Wmc/WmcOrderFileGenerator.cs`, `WmcFileDropErpOrderIntegration.cs`, the Admin export and order integration loaders, and WMC tests.

1. Implement the Task 1 WMC test first and confirm red.
2. Ensure WMC loader includes the selected payment term and validator blocks the new code before generating/writing an export.
3. Return stable marker `WMC_CARD_ON_DELIVERY_MAPPING_PENDING` before the adapter's existing-file shortcut; do not invent or emit a WMC condition code.
4. Run focused WMC/export tests green.

### Task 8: Required regression validation and scope review

1. Run restore.
2. Run `dotnet build Orofoods.slnx -c Release --no-restore`.
3. Run the full test suite in Release.
4. Run focused payment, order/customer checkout, Portal, seller checkout, Admin, and WMC tests.
5. Review `git diff --check`, inspect all changed files, confirm default flag false, no production/deployment/config edits, no Point integration, and only the documented minimal seed migration.
6. Do not deploy or execute migrations in Staging/Production; stop after Phase 1.
