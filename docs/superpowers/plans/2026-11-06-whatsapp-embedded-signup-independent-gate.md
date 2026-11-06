# Independent Embedded Signup Gate Implementation Plan

> **For agentic workers:** Execute inline in the isolated `feat/whatsapp-business-app-coexistence` worktree. Steps use checkbox tracking.

**Goal:** Ensure `WhatsAppBusiness__Enabled=true` continues to operate the existing Cloud API integration without enabling coexistence.

**Architecture:** Add `WhatsAppBusinessOptions.EmbeddedSignupEnabled`, default it to false, and gate every coexistence-only path with it. Keep `Enabled` as the master switch for the existing webhook/send integration; when the new flag is off, use the configured legacy token, Phone Number ID, webhook interpretation, and startup policy.

**Tech Stack:** ASP.NET Core options/MVC, EF Core, xUnit, existing SQLite in-memory tests.

**Spec:** `docs/superpowers/specs/2026-11-06-whatsapp-business-app-coexistence.md`

## Global Constraints

- Do not push, merge, deploy, apply migrations, or change Azure, Meta, Staging, Production, or any database.
- Keep `WhatsAppBusiness__EmbeddedSignupEnabled=false` by default.
- Preserve `WhatsAppBusiness__Enabled` as the existing Cloud API integration switch.
- Never return to persisted coexistence credentials or process coexistence deliveries while the new flag is false.

## Review Focus

- Legacy Cloud API remains enabled with the feature flag off, even when a persisted coexistence connection exists.
- Staging startup does not newly require Azure Data Protection solely because legacy WhatsApp is enabled.
- New history/echo/state-sync handling stays dormant with the feature flag off.
- Embedded Signup cannot be started or completed while the flag is off.

### Task 1: Independent feature gate

**Files:**
- Modify: `Orofoods.Web/Services/Commercial/WhatsAppBusinessOptions.cs`, `Orofoods.Web/appsettings.json`
- Modify: `Orofoods.Web/Program.cs`, `Orofoods.Web/Configuration/StartupEnvironmentPolicy.cs`
- Modify: WhatsApp connection credential providers, webhook parser, signup controller/view, and history worker
- Test: `Orofoods.Web.Tests/Services/WhatsAppEmbeddedSignupTests.cs`, `WhatsAppBusinessTests.cs`, `WhatsAppHistoryDeliveryTests.cs`, and `StartupEnvironmentPolicyTests.cs`

- [x] Add and run failing regression tests for disabled-by-default signup, legacy credential/phone fallback, webhook behavior, and Data Protection gating.
- [x] Implement the independent flag across UI, completion service, connection providers, coexistence webhook handling, worker, and Staging Data Protection readiness.
- [x] Run focused Release tests, full Release build, and staged diff/secret review.
- [x] Commit locally on the feature branch; do not push.
