# Mercado Pago Phase 1 Design

## Scope and constraints

Add a test-environment integration for Mercado Pago Checkout Transparente through the Orders API. The implementation covers PIX, credit-card tokens, payment persistence, status normalization, idempotency, authenticated payment endpoints, signed order webhooks, and automated tests. It does not migrate or update a database, deploy, commit, write to WMC, release WMC billing, or change the configured production database provider.

The application currently configures PostgreSQL through `UseNpgsql` and `PostgreSqlApplicationDbContext`. SQLite remains test infrastructure and is created from the EF Core model with `EnsureCreatedAsync`. Every existing migration is preserved byte-for-byte, including the empty `NormalizePaymentTermOrdering` migration.

## Existing components to preserve

- `Payment`, `PaymentStatus`, `PaymentService`, `IBoletoProvider`, and `PendingBoletoProvider` remain the foundation for receivables and the future WMC boleto flow.
- `PaymentEligibilityService` remains the backend authority for the three-purchase rule, `CreditBlocked`, and the absolute fourteen-day limit.
- Existing numeric values of `PaymentStatus` remain unchanged: `Pending=0`, `Issued=1`, `Paid=2`, `Overdue=3`, `Cancelled=4`, and `Failed=5`.
- WMC mappings remain documentation only: PIX `CODCPAGTO=1`, boleto 7 days `2`, boleto 14 days `3`, and card `61`.

## Payment model

`Payment` represents one payment attempt. A single Orofoods order may therefore have more than one attempt, while each attempt has one unique persisted `IdempotencyKey`. The existing internal `OrderId` retains its name; Mercado Pago identifiers use the unambiguous names `GatewayOrderId` and `GatewayPaymentId`.

The model adds gateway, external reference, idempotency, PIX presentation, expiration, update, and non-sensitive card metadata fields. It never stores a card token, full card number, CVV, password, or other PCI-sensitive values. Legacy boleto fields remain present.

New internal statuses receive explicit values greater than five: `Processing=6`, `Approved=7`, `Rejected=8`, `Refunded=9`, and `Expired=10`. Mercado Pago strings are isolated in one mapper.

## Gateway boundary

`IPaymentGateway` exposes creation of PIX and credit-card Orders, retrieval of an Order, and refund of an Order transaction. `MercadoPagoPaymentGateway` is the only component that knows Mercado Pago JSON and uses a named `HttpClient` from `IHttpClientFactory`.

Only these current Orders API endpoints are used:

- `POST /v1/orders`
- `GET /v1/orders/{id}`
- `POST /v1/orders/{order_id}/refund`

Mutable requests carry `X-Idempotency-Key`. Creation uses `type=online`, `processing_mode=automatic`, an Orofoods external reference, the authoritative order total, and one payment transaction. PIX sends `id=pix` and `type=bank_transfer`; it does not send a PIX key. Card sends only the browser-generated token, payment-method identifier, type `credit_card`, and installments.

`MercadoPagoOptions` obtains `AccessToken`, `PublicKey`, and `WebhookSecret` from configuration providers such as User Secrets or environment variables. Secret values are absent from tracked settings and logs.

## Orchestration and idempotency

`PaymentOrchestrationService` validates ownership, the selected payment term, amount, and allowed method from server-side data. It creates and saves a pending attempt and its idempotency key before calling Mercado Pago. A retry carrying the same attempt key returns or resumes that attempt and reuses its key. The unique model index is the final concurrency guard.

Successful gateway responses update external identifiers, mapped status, PIX display fields, expiration, allowed card metadata, and timestamps. Failures set `Failed` only when the outcome is known; ambiguous transport failures leave the attempt retryable with the same key.

## APIs and checkout

Authenticated customer endpoints create PIX/card attempts and read a payment owned by the current customer. Request DTOs do not accept amount, internal status, gateway identifiers, full card data, or CVV. The server derives all authoritative values.

The existing checkout stops posting raw card fields. It uses the current official browser mechanism compatible with Checkout Transparente and the Orders API to obtain a token, then sends only the token and permitted metadata. Missing browser configuration keeps card submission unavailable without exposing credentials. PIX payment details are returned through a safe response DTO for QR Code, copy-and-paste code, amount, expiration, and status.

## Webhook

The public webhook accepts Mercado Pago order notifications. It validates `x-signature` using the official HMAC-SHA256 manifest containing `data.id`, `x-request-id`, and `ts`, compares hashes in constant time, and rejects malformed or invalid requests without disclosing the reason.

After signature validation, it ignores payload status and amount, calls `GET /v1/orders/{id}`, locates the internal attempt by `GatewayOrderId`, verifies the external reference and amount, and applies the mapped authoritative state. Repeated delivery is a no-op; `PaidAt` is assigned only when first reaching `Approved`. Logs contain internal IDs, gateway order IDs, and correlation IDs but no secrets or card tokens. There is no WMC side effect; a no-op approval hook documents the future extension point.

## Testing

Tests cover preserved enum values, status mapping, official HTTP paths/payloads/idempotency headers, absence of forbidden card fields, PIX response mapping, retry key reuse, duplicate webhook handling, one-time `PaidAt`, approved/rejected/expired states, signature validation and constant-time comparison behavior, eligibility rules, and controller ownership. External HTTP is replaced by deterministic handlers; no credential or live Mercado Pago call is used.

The two baseline SQLite migration tests currently fail because they run old SQLite migrations whose schema lacks `CreditBlocked`. They will be changed to build their disposable schema from the current EF Core model with `EnsureCreatedAsync`, without adding or rewriting a SQLite migration.

## Deferred database artifact

The EF Core model may be changed now, but no migration is generated or executed. Before generation, the exact PostgreSQL tables, columns, types, nullability/defaults, indexes, unique constraints, foreign keys, impact on existing `Payment` rows, and approximate SQL will be presented for explicit authorization.
