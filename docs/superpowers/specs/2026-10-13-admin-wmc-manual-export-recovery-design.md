# Admin WMC Manual Export Recovery Design

## Goal

Make manual WMC TXT export identify customers by CNPJ, persist generated files for safe repeat download, and provide an audited rejection and regeneration workflow without enabling automatic WMC integration or marking orders imported automatically.

## Scope and constraints

- Work only in the isolated branch and worktree.
- Do not access or change Azure, Staging, production, PostgreSQL instances, WMC, or existing orders.
- Do not push, merge, deploy, or import a TXT into WMC.
- Preserve the existing automatic WMC settings and flows.
- Integrate the pending NeoGrid v3.2 layout correction without editing its source worktree.
- Keep every Admin action restricted by the existing `Administrador` controller authorization and antiforgery validation.

## Customer identity and TXT layout

`Customer.WmcCode` is optional for manual-export eligibility, diagnosis, and TXT generation. The generator requires a CNPJ that normalizes to exactly 14 digits and writes it to the buyer, invoice, and delivery CNPJ fields of record `019`. Product `WmcCode` and unit remain mandatory. Record `090` contains monetary totals only and never contains a customer code.

Records `019`, `021`, `030`, `040`, and `090` follow the NeoGrid Orders v3.2 positions established by the locally accepted homologation sample and the existing isolated layout worktree.

## Persisted artifact

Add `WmcManualExportArtifact`, related to an order and a unique export attempt. It stores the file name, UTF-8 TXT content, SHA-256 hash, UTC creation time, administrator identity, optional predecessor artifact, and optional regeneration reason. The order-export state points to the current artifact.

The TXT, state transition, and successful audit are saved atomically. Repeat download reads the current artifact, verifies SHA-256, and returns the original bytes without creating another export attempt or changing state/audits.

## State transitions

- `Pending -> Exported`: first successful manual export, allowed only when no successful export history exists.
- `Exported -> Imported`: existing explicit administrative confirmation only.
- `Exported -> Rejected`: administrator records WMC rejection with a required reason.
- `Rejected -> Exported`: administrator regenerates the TXT with a required reason; the new artifact points to the rejected artifact.

`Rejected` is a distinct persistent state. Recording a rejection and regenerating are separate actions. Concurrent state changes are rejected through the existing version concurrency token. A unique attempt identifier prevents duplicate artifacts and audits.

Generation, rejection, regeneration, and repeat download never set `Imported`.

## Existing rejected order recovery

Legacy successful exports, including ORO-2026-000006, have no stored artifact. The UI does not offer repeat download for them. An administrator can record the rejection with its reason while retaining every existing audit, then regenerate a corrected TXT. Only the regenerated artifact is stored.

No data backfill invents missing TXT content, no audit is deleted, and no direct data update is part of this feature.

## Admin interface

On Admin -> Pedidos:

- Show `Enviar ao WMC` only when the order has no prior successful manual export.
- Render the send button as a compact solid success action, approximately 140 by 40 pixels, left-aligned below the WMC state; on hover it becomes a success outline.
- Show `TXT gerado — aguardando importação` while state is `Exported`.
- Offer `Baixar TXT novamente` only when a current, valid artifact exists.
- Offer `Registrar rejeição do WMC` for an exported TXT and require a reason.
- Offer `Regenerar TXT` only in `Rejected` state and require a reason.
- Keep import confirmation explicit.

The Admin order diagnosis shows customer CNPJ as the export identity. Optional customer `WmcCode` may remain informational and cannot block eligibility.

## Audit and error behavior

Add audit outcomes for rejection and regeneration. Rejection audit records the acting administrator, UTC date, reason, file name, and a new attempt identifier. Regeneration records a new successful audit and artifact with the supplied reason and predecessor link.

Invalid state, missing reason, missing artifact, failed integrity verification, generation failure, and concurrency conflicts return specific user-facing errors and do not partially update the database.

## Verification

Tests cover CNPJ eligibility and field positions, absence of customer WMC code, first-export uniqueness, artifact integrity and repeat download, rejection and regeneration authorization contracts, required reasons, audit history preservation, concurrency, explicit import semantics, migration constraints, view labels/actions/styles, focused WMC behavior, full test suite, build, and a locally generated TXT inspection.
