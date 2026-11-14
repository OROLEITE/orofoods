# WhatsApp Notification Conversation Navigation Implementation Plan

**Goal:** Route new WhatsApp notifications to the exact conversation that owns the inbound message without changing schema or service-window rules.

**Architecture:** Reuse the existing `WhatsAppConversationService` transaction and `WhatsAppController.Index(id)` authorization/query path. The notification action URL carries the persisted conversation ID; old notifications retain the generic route.

## Preserved regression coverage

- Webhook notification URL contains the persisted conversation ID.
- `Index(id)` loads the requested history and its current service window together.
- An unauthorized seller cannot select another seller's conversation through `Index(id)`.

## Constraints retained

- No Meta 24-hour rule changes.
- Existing access-control and polling behavior remain authoritative.
- No production database, Staging, Meta, WMC, or deployment changes.
