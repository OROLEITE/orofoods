# WhatsApp Notification Conversation Navigation Design

## Decision

New inbound WhatsApp notifications use `/Admin/WhatsApp?id={conversation.Id}` after the conversation is persisted. The existing controller remains the source of truth for authorization, history loading, and the 24-hour service-window calculation.

## Compatibility

Older notifications without an ID continue using the generic `/Admin/WhatsApp` destination. No notification schema field, migration, Meta rule, or WMC integration is required for navigation.
