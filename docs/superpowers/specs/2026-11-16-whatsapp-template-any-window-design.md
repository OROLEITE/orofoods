# WhatsApp templates in every open conversation

## Intent

Allow an approved WhatsApp template to be selected and sent from any open CRM conversation. The 24-hour service window continues to govern free-text messages only.

## Goals

- Render `Enviar mensagem modelo` for every open conversation.
- Keep free-text sending available only while the 24-hour service window is open.
- Allow approved-template catalog lookup and sending both inside and outside that window.
- Keep closed conversations blocked, while preserving authorization, antiforgery, Meta approval/parameter checks, audit/history persistence, error handling, and the existing compact dark/cyan UI.

## Non-goals and constraints

- No database schema, migration, seed, Azure, or deployment changes.
- No change to Meta gateway behavior or template approval rules.
- No automatic changes to conversation status or history.

## Approach

Keep the existing separate free-text and template forms. Remove only the `ServiceWindowOpen` visibility/endpoint guard for templates; retain the closed-conversation guard and all existing gateway validation. This is smaller and safer than merging the composers or weakening the free-text guard.

## Acceptance scenarios

1. Open conversation with an active window: both composers are available; free text and an approved template can be sent.
2. Open conversation with an expired window: only the template composer is available; free text remains rejected.
3. Closed conversation: template catalog and template send remain rejected until reopening.
