# WhatsApp Template Any-Window Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Permit approved WhatsApp templates in every open CRM conversation while retaining the 24-hour restriction for free-text messages.

**Architecture:** Keep the existing Razor forms and Meta gateway. Change only the template form visibility and the controller checks that currently reject templates during an active service window; closed conversations remain guarded server-side and in the UI.

**Tech Stack:** ASP.NET Core MVC, EF Core test context, xUnit, existing WhatsApp gateway and Razor/CSS/JavaScript.

**Spec:** `docs/superpowers/specs/2026-11-16-whatsapp-template-any-window-design.md`

## Global Constraints

- No migrations, database changes, Azure changes, deployment, or real WhatsApp sends.
- Preserve authorization, antiforgery, Meta approval/parameter validation, history/audit persistence, and error handling.
- Preserve compact dark mode and blue-cyan controls.
- Free text remains allowed only inside the 24-hour window; closed conversations remain blocked.

## Review Focus

- Open conversation with an active window must expose both composers and accept an approved template.
- Open conversation with an expired window must reject free text while accepting an approved template.
- Closed conversation must reject template catalog and sending even if its window state would otherwise permit it.
- Template approval, parameter, permission, antiforgery, and Meta-error paths must remain unchanged.
- Polling must not re-hide the template composer when the service-window flag changes.

### Task 1: Regression contracts

**Files:**
- Modify: `Orofoods.Web.Tests/Services/WhatsAppCrmOperationsTests.cs`
- Modify: `Orofoods.Web.Tests/Services/WhatsAppThemeSurfaceTests.cs`

**Interfaces:**
- Consumes: existing `WhatsAppController`, `WhatsAppApprovedTemplate`, `TrackingGateway`, and Razor markup readers.
- Produces: failing tests for open-window template catalog/send, closed conversation blocking, and template-form visibility independent of `ServiceWindowOpen`.

- [ ] Write the failing tests before production changes:
  - `Template_catalog_is_available_inside_an_open_service_window`.
  - `Open_window_allows_an_approved_template_without_disabling_free_text`.
  - `Closed_conversation_rejects_template_catalog_and_send`.
  - `Approved_template_form_is_visible_for_open_conversations_inside_and_outside_the_service_window`.
- [ ] Run the focused tests and confirm they fail because the current service-window guards reject/hide the template flow.

### Task 2: Backend and view behavior

**Files:**
- Modify: `Orofoods.Web/Areas/Admin/Controllers/WhatsAppController.cs`
- Modify: `Orofoods.Web/Areas/Admin/Views/WhatsApp/Index.cshtml`
- Modify: `Orofoods.Web/wwwroot/js/whatsapp-composer.js` only if the focused regression demonstrates a polling visibility regression.

**Interfaces:**
- Consumes: Task 1 tests and existing Meta gateway contracts.
- Produces: approved-template lookup/send for open conversations, with closed-conversation guards unchanged.

- [ ] Remove only the `WhatsAppServiceWindow.IsOpen` rejection from `Templates` and `SendTemplate`; keep `Status == Closed`, access, approval, parameter, antiforgery, persistence, and error paths.
- [ ] Change the template form's `hidden` condition to depend only on closed status; retain the free-text condition based on closed status or service-window state.
- [ ] Keep polling synchronized with the same open/closed visibility rule; do not loosen the free-text guard.
- [ ] Run the focused tests and confirm all new and existing WhatsApp tests pass.

### Task 3: Verification

**Files:**
- No additional source files.

- [ ] Run the focused WhatsApp tests.
- [ ] Run the complete test suite.
- [ ] Run Release build.
- [ ] Run `git diff --check` and inspect the complete diff for scope.
