# Mercado Pago Point Concurrency Hardening

Approved scope from the user's request on 2026-09-30.

## Objective

Before any official Mercado Pago call, harden Point payment concurrency so same-order attempts cannot create distinct logical charges, unrelated orders are not serialized, retries survive process restarts, conflicts return controlled results, and multiple application instances remain safe.

## Constraints

- Use fakes only; external calls remain zero.
- Do not configure credentials, apply migrations, deploy, change Azure, or touch Production.
- Do not create a migration unless the chosen guarantee requires one; if required, stop before creating it.
- Persist a payment attempt and its idempotency key before calling the provider.
- Keep database transactions and locks out of the HTTP call.
- Continue requiring authoritative provider status, matching amount and external reference before approval.
- Add concurrency coverage for same order (2 and 10 callers), different orders, retries after timeout/lost response/restart, controlled conflicts, webhook races/replays/out-of-order events, and one logical Point charge per attempt.

## Race handling details

- Refresh the tracked `Payment` and its `Order` after acquiring the order lock and before applying a provider status. The request scope may retain stale entities while it waits on provider I/O.
- If a webhook's first Point lookup races with persistence of `GatewayOrderId`, the online-payment fallback must exclude Point payments. An as-yet-unmatched order remains retryable (HTTP 503) until a later webhook can route through Point reconciliation.
- Gate entry reference acquisition and retirement must be atomic; no caller may enter an entry after it has begun retirement.

## Current Model

`Payment` is the persisted attempt entity; there is no `PaymentAttempt` table. A normal CardOnDelivery order starts with one pending `Payment` row. Existing indexes include a unique global `Payment.IdempotencyKey` and a non-unique `Payment.OrderId` index; no index enforces one active payment per order.

## Required Outcome

Use a per-OrderId in-process gate only as a local optimization and a PostgreSQL row lock on the existing `Orders` row as the cross-instance serialization point. Persist/claim the attempt in a short transaction, commit, call the provider with the persisted idempotency key, then persist authoritative status in another short transaction. No schema migration is expected if this existing-row lock proves sufficient.
