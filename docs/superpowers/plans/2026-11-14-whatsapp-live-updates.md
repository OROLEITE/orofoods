# WhatsApp live updates and notifications

Base: `origin/main` bc85ae2c5696b7a3b38e982a310771c5b9795df2, verified with connected GitHub. Isolated branch `fix/whatsapp-crm-auto-refresh`.

## Diagnosis before edits

- Signed ordinary webhooks persist messages transactionally with external-ID deduplication. No live Production inspection was available; source findings do not identify the deployed process.
- Polling stops entirely without a selected history. A missing time node on a new/empty server-rendered conversation throws before messages reconcile.
- Send follows an HTML redirect and relies on a second polling call; an already running or failed poll can leave a persisted send invisible.
- Window/status changes reload the whole document, discarding drafts and scroll. Notification bell is server-rendered only.
- ReadAt/UserId already persist notification ownership/read state. Conversation state is independent.
- Webhook ignored `contacts[].profile.name`; the conversation had no profile column. Sound is a local preference; Atendimento is a static badge.

## Implementation and verification

- [x] Test webhook/profile matching and JSON send outcomes, then persist `ProfileName` on the conversation, add the PostgreSQL migration artifact, and use the profile/phone labels with a phone fallback.
- [x] Test actual JS reconciliation, missing nodes, send/poll races, lost responses, retries and preservation; implement polling on empty inbox, pending send reconciliation by `clientRequestId`, visible recovery and in-place thread controls.
- [x] Add notification updates/read/open/read-all using existing user state, antiforgery and safe local navigation. Test ownership, persistence and asynchronous UI race/dedup.
- [x] Move sound into notification menu, remove decorative Atendimento badge, keep Nova conversa cyan #0E7490/white and existing responsive layout.
- [x] Run relevant tests and Release build, inspect scope and commit locally. No deployment, migration execution, database operation or Meta/WMC/config changes.

Polling remains the existing transport. Introducing SignalR/SSE would add hosting/reconnect infrastructure outside this correction. Read states and profile names are durable in PostgreSQL after the migration is applied by a separately authorized release operation; this worktree did not execute it.
