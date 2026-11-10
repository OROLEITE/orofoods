# WhatsApp Business App Coexistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add secure, opt-in Meta Embedded Signup coexistence for the configured Orofoods WhatsApp number and preserve messages from the Business App in the existing CRM.

**Architecture:** Extend existing `WhatsAppBusinessOptions`, Meta gateway, and signed webhook service. Persist the single connected account token encrypted, enqueue large history deliveries durably, and reuse the current conversation/message model with WAMID deduplication and a separate synced-contact table. Retain the existing static token path as fallback.

**Tech Stack:** ASP.NET Core MVC, Facebook JavaScript SDK, EF Core SQLite/PostgreSQL migrations, ASP.NET Core Data Protection, xUnit, SQLite in-memory tests.

**Spec:** `docs/superpowers/specs/2026-11-06-whatsapp-business-app-coexistence.md`

## Global Constraints

- No Meta, Azure, Production, Staging, or real database changes/calls.
- Never log, render, or return App Secret, authorization code, or access token.
- Never call phone-number registration or disconnect endpoints for coexistence onboarding.
- Keep feature disabled until runtime Meta app configuration is provided.
- Use only documented Meta fields and request paths; Graph API version stays externally configurable.
- Preserve current webhook HMAC validation, Cloud API sending, CRM authorization, and conversation history.

## Review Focus

- A callback from an untrusted origin or wrong signup completion event cannot connect a number: test origin/event validation.
- Wrong WABA or Phone Number ID cannot replace the configured number: test mismatch rejection before persistence or Graph calls.
- Replayed echoes/history chunks do not duplicate messages or unread notifications: test duplicate IDs and payload hash.
- Large history synchronization is not lost on process restart and does not make a webhook request import thousands of messages synchronously: test durable enqueue and background processing.
- Business App contacts never create or ambiguously link Orofoods Customer records: test contact upsert and unique-match-only association.

### Task 1: Meta signup configuration and secure connected account

**Files:**
- Modify: `Orofoods.Web/Services/Commercial/WhatsAppBusinessOptions.cs`
- Create: `Orofoods.Web/Models/Commercial/WhatsAppBusinessConnection.cs`
- Create: `Orofoods.Web/Services/Commercial/WhatsAppAccessTokenProtector.cs`
- Modify: `Orofoods.Web/Data/ApplicationDbContext.cs` and `Orofoods.Web/Data/PostgreSqlApplicationDbContext.cs`
- Modify: `Orofoods.Web/Data/MigrationsPostgreSql/PostgreSqlApplicationDbContextModelSnapshot.cs`
- Create: PostgreSQL migration in `Orofoods.Web/Data/MigrationsPostgreSql/`
- Modify: `Orofoods.Web/Program.cs`
- Test: `Orofoods.Web.Tests/Services/WhatsAppBusinessTests.cs`

**Interfaces:**
- Produces a singleton connection record for WABA ID, configured Phone Number ID, display number, encrypted access token, and connect time.
- Produces `IWhatsAppAccessTokenProvider.GetAsync(CancellationToken)` resolving the protected active token first and existing configured token second.
- Produces `WhatsAppAccessTokenProtector.Protect(string)` and `.Unprotect(string)` using a dedicated Data Protection purpose.

- [x] Write tests proving round-trip protection, plaintext absence from persistence, unreadable connected token fail-closed, and fallback to the configured legacy token.
- [x] Run focused tests and verify the new contracts fail before implementation (compiler failure on the missing types/DbSet).
- [x] Implement the model, Data Protection protector, token provider, EF mappings, and PostgreSQL migration without applying it to any database. SQLite test databases use `EnsureCreated` from the shared current model.
- [x] Run focused tests and verify the contracts pass.

### Task 2: Embedded Signup and server-side completion

**Files:**
- Create: `Orofoods.Web/Services/Commercial/MetaWhatsAppEmbeddedSignupClient.cs`
- Create: `Orofoods.Web/Areas/Admin/Models/WhatsAppEmbeddedSignupViewModel.cs`
- Modify: `Orofoods.Web/Areas/Admin/Controllers/WhatsAppController.cs`
- Modify: `Orofoods.Web/Areas/Admin/Views/WhatsApp/Index.cshtml`
- Create: `Orofoods.Web/wwwroot/js/whatsapp-embedded-signup.js`
- Modify: `Orofoods.Web/Program.cs`
- Test: `Orofoods.Web.Tests/Services/WhatsAppBusinessTests.cs`, Admin controller tests

**Interfaces:**
- Consumes Task 1's options, token protector/provider, and connection record.
- Produces Admin-only GET state and antiforgery-protected POST completion accepting only the Meta session event, exchangeable authorization code, WABA ID, and Phone Number ID.
- Produces Meta client methods for the documented authorization-code exchange, WABA subscription, and history sync request; `smb_app_state_sync` requests stay blocked pending official contract review.

- [x] Test the exact Graph resources with fake HTTP handlers, successful and rejected onboarding events, mismatched Phone Number ID, and token secrecy.
- [x] Implement Meta SDK launch with configured App ID/Login Configuration ID and the coexistence feature selection. Do not hard-code permissions.
- [x] Exchange the code server-side, validate the supplied Phone Number ID/display number, persist only a Data Protection-encrypted token, subscribe the app, and request history sync only. Do not request contact state sync or register the phone number until its official contract is validated.
- [x] Run focused tests. No live Meta call, `/register`, or disconnect call was made.

### Task 3: Echo and state-sync events

**Files:**
- Modify: `Orofoods.Web/Services/Commercial/WhatsAppConversationService.cs`
- Test: `Orofoods.Web.Tests/Services/WhatsAppBusinessTests.cs`

**Interfaces:**
- Consumes the existing webhook envelope validation, token provider, conversation lookup, media services, and Task 1 storage.
- Parses `smb_message_echoes` as outbound, keyed to the recipient phone, and deduplicated by message ID.
- Keeps `smb_app_state_sync` requests and contact mutations disabled, source-validates and encrypts a delivered event as Blocked, logs the official Meta HTTP 429 blocker, and infers no payload fields.

- [x] Test outbound echoes, recipient conversation association, duplicate delivery, encrypted app-state deferral/logging, and WABA source validation.
- [x] Implement event parsing and persistence while retaining existing inbound `messages` and `statuses` behavior.
- [x] Run focused tests.

### Task 4: Idempotent history import

**Files:**
- Modify: `Orofoods.Web/Services/Commercial/WhatsAppConversationService.cs`
- Test: `Orofoods.Web.Tests/Services/WhatsAppBusinessTests.cs`

**Interfaces:**
- Processes history in the existing signed webhook pipeline, reuses the unique WAMID index for replay deduplication, associates by normalized peer phone, preserves timestamps, and does not create live unread notifications.

- [x] Test duplicate history delivery, timestamp preservation, and no unread increments.
- [x] Queue the encrypted raw history body durably with a unique payload hash, process with leases/backoff, clear payload only after completion, and retain failed payloads for retry/review.
- [x] Raise the bounded request body cap to 3 MiB and import transactionally using existing WAMID deduplication.
- [x] Test payloads over 1 MiB, duplicate body and message IDs, stale lease recovery, and transient DB retry.
- [x] Run focused tests.

### Task 5: Wire coexistence credentials into existing Cloud API and verify

**Files:**
- Modify: `Orofoods.Web/Services/Commercial/MetaWhatsAppBusinessGateway.cs`
- Modify: `Orofoods.Web/Services/Commercial/MetaWhatsAppMediaClient.cs`
- Modify: `Orofoods.Web/Services/Commercial/WhatsAppBusinessOptions.cs`, `Orofoods.Web/appsettings.json` (public ID/phone only, integration disabled)
- Modify: `Orofoods.Web.Tests/Services/WhatsAppBusinessTests.cs`

**Interfaces:**
- Both Cloud API send and media operations consume `IWhatsAppAccessTokenProvider` and a resolved active Phone Number ID while preserving legacy options fallback.

- [x] Test that the connected token and phone ID take precedence in Cloud API send and media clients while preserving legacy fallback settings.
- [x] Wire the provider into send/media clients, keep the legacy phone ID setting separate, and keep `Enabled=false`.
- [x] Run EF design-time model check through `PostgreSqlApplicationDbContextDesignTimeFactory` (no database connection); generated PostgreSQL migrations were inspected but not applied.
- [x] Run focused WhatsApp tests (137 passed), Release build, and `git diff --check`.
- [x] Review the diff for credentials and out-of-scope external changes; no Meta, database, Azure, deployment, or send operation was performed.

### Task 6: Harden Staging readiness and startup

**Files:**
- Modify: `Orofoods.Web/Infrastructure/SecurityHeadersMiddleware.cs`
- Modify: `Orofoods.Web/Services/Commercial/WhatsAppBusinessOptions.cs`
- Modify: `Orofoods.Web/Areas/Admin/Controllers/WhatsAppEmbeddedSignupController.cs`
- Modify: `Orofoods.Web/Services/Commercial/WhatsAppBusinessConnectionService.cs`
- Modify: `Orofoods.Web/Program.cs`
- Test: `Orofoods.Web.Tests/Infrastructure/SecurityHeadersMiddlewareTests.cs`
- Test: `Orofoods.Web.Tests/Services/WhatsAppEmbeddedSignupTests.cs`
- Test: startup configuration tests for migration and Data Protection gates

**Constraints:**
- Add only the Meta JavaScript SDK origin required by the existing loader; retain all existing CSP directives.
- Embedded Signup must stay not-ready when any callback, credential, number, or webhook prerequisite is absent.
- Staging must not run EF migrations at application startup; Development and other non-Production environments retain their existing behavior.
- When coexistence is enabled in Staging, require configured Azure Blob persistence and Key Vault protection for the Data Protection key ring. Keep secrets and tokens out of source, logs, and tests.
- Keep `WhatsAppBusiness:Enabled=false`; block both `smb_app_state_sync` requests and contact processing until the official contract is validated.
- Do not apply migrations, publish, or change Azure, Meta, Staging, or Production.

- [x] Add failing focused tests for CSP origin preservation, complete signup readiness, Staging migration skip/other environment behavior, and the Staging Data Protection prerequisite.
- [x] Run the new tests and confirm they fail for the expected missing guards.
- [x] Implement only the required CSP, readiness, migration, and Data Protection guards.
- [x] Run focused tests and the Release build; inspect the final diff for secrets and unintended environment changes.
