# SDD ledger — plan: docs/superpowers/plans/2026-09-21-webapp-6-keyvault-storage-managed-identity-plan.md

## Recovery / setup

- Spec: `docs/superpowers/specs/2026-09-21-webapp-6-keyvault-storage-managed-identity-design.md`
- Workspace resolved by `sdd-workspace`: `.superpowers/sdd/2026-09-21-webapp-6-keyvault-storage-managed-identity-plan`
- Existing ledger status on resume: absent; only `plan-path` existed.
- Current checkout: `/home/Orofoods`, branch `feature/mercado-pago-phase-1`, normal checkout (not a linked worktree), with the plan's existing commits and dirty implementation state preserved.
- Ruling: continue in the existing checkout rather than creating a new worktree — the user explicitly requested resumption from the existing state, and uncommitted WEBAPP-6 code would not be present in a new worktree — cost if wrong: weaker filesystem isolation, mitigated by exact-path staging and per-task review.
- Ruling: the user's post-quota execution authorization supersedes the plan's planning-time prohibition on Azure create/update/role-assignment operations, but only for the explicitly listed Staging resources and Tasks 10 onward — cost if wrong: authorized infrastructure mutations would be delayed or performed too broadly; exact resource validation and stop checkpoints constrain scope.

## Reconstructed completed progress

Task 1: complete (pre-quota state explicitly confirmed by user; no redispatch)
Task 2: complete (2026-09-21 Azure API: B1 limit 1, usage 0, available 1; no redispatch)
Task 3: complete (`storofoodsstg01` explicitly confirmed by user; no redispatch)
Task 4: complete (`product-images` private explicitly confirmed by user; no redispatch)
Task 5: complete (`data-protection` private explicitly confirmed by user; no redispatch)
Task 6: complete (`kv-orofoods-stg-01` explicitly confirmed by user; no redispatch)
Task 7: complete (`dp-orofoods-stg` explicitly confirmed by user; no redispatch)
Task 8: complete (PostgreSQL secret stored and temporary credential intentionally retained, explicitly confirmed by user; no redispatch)
Task 9: complete (authorized secret slots only; Meta, Mercado Pago and WMC remain disabled; no redispatch)

## Pre-flight consistency scan

### Task self-consistency

| Task | Internal check | Finding / ruling |
|---|---|---|
| 1 | Inventory, names, network and checkpoint align | Consistent; completed pre-quota. |
| 2 | Quota query gates Task 10 | Historical `0` is superseded by current Azure result `limit=1`, `usage=0`; Task 10 is unblocked. |
| 3 | Storage creation and secure baseline validation align | Consistent; completed pre-quota. |
| 4 | Private image container and later delivery requirement align | Consistent; authenticated delivery remains Task 16. |
| 5 | Separate private key-ring container aligns with later provider | Consistent; completed pre-quota. |
| 6 | RBAC vault and bootstrap network model align | Consistent; completed pre-quota. |
| 7 | RSA wrap/unwrap Key aligns with Data Protection provider | Consistent; completed pre-quota. |
| 8 | Secret import and delayed local-file deletion align | Consistent; deletion remains gated by Tasks 17–18. |
| 9 | Names are mapped without placeholder secrets or integration calls | Consistent; completed pre-quota. |
| 10 | Plan/App creation, runtime, HTTPS and Always On align | User additionally mandates VNet Integration here; include it after Web App creation and before Task 11, without touching the PostgreSQL subnet. |
| 11 | System identity precedes identity-scoped RBAC | Consistent. |
| 12 | Minimum vault/key roles and scopes align | Consistent. |
| 13 | Minimum container-scoped roles align | Consistent; stop before any account-scope fallback. |
| 14 | Key Vault reference and non-secret settings align | Consistent; validation must not print setting values. |
| 15 | Azure provider, fail-fast and no-network tests align | Consistent. |
| 16 | Azure Blob provider and private delivery path align | Consistent; must not solve delivery by making the container public or adding permanent SAS. |
| 17 | Local build/test/publish are explicit, but deployed smoke tests require a deployment | Ruling: local publish is authorized; no Azure deploy is authorized because neither the plan checklist nor spec authorizes one and the spec calls deploy a later separate stage — cost if wrong: runtime smoke tests remain incomplete and final status is PARTIAL until separately authorized. |
| 18 | Credential removal depends on successful runtime checks | Preserve the temporary credential if Task 17 cannot prove runtime PostgreSQL connectivity and Key Vault reference resolution. |

### Shared files and interfaces

| Tasks | Producer → consumer | Finding |
|---|---|---|
| 1 → 3 | Approved Storage name/region → Storage creation | Aligned; complete. |
| 1 → 6 | Approved Vault name/region → Vault creation | Aligned; complete. |
| 1 → 10 | Region, RG, VNet/subnet inventory → App Service resources | Aligned; revalidate exact targets before mutation. |
| 1 → 17 | PostgreSQL/VNet baseline → private-connectivity smoke | Aligned. |
| 2 → 10 | `QUOTA_READY` → B1 plan creation | Aligned; current result is 1/0/1. |
| 3 → 4 | Storage account → `product-images` | Aligned; complete. |
| 3 → 5 | Storage account → `data-protection` | Aligned; complete. |
| 3 → 13 | Storage resource/container IDs → RBAC scopes | Aligned. |
| 3 → 14 | Storage URI → App settings | Aligned. |
| 3 → 16 | Storage endpoint → Azure image provider | Aligned. |
| 3 → 17 | Storage baseline → runtime tests | Aligned. |
| 4 → 13 | Private image container → contributor role | Aligned. |
| 4 → 16 | Private image container → provider/delivery behavior | Aligned. |
| 4 → 17 | Private image container → CRUD/privacy smoke | Aligned. |
| 5 → 13 | Private key-ring container → contributor role | Aligned. |
| 5 → 15 | Private key-ring container → Data Protection persistence | Aligned. |
| 5 → 17 | Private key-ring container → restart/cookie smoke | Aligned. |
| 6 → 7 | Vault → Data Protection Key | Aligned; complete. |
| 6 → 8 | Vault → PostgreSQL Secret | Aligned; complete. |
| 6 → 12 | Vault/key resource IDs → minimum RBAC | Aligned. |
| 6 → 14 | Vault secret identifier → Key Vault reference | Aligned. |
| 6 → 15 | Vault/key identifier → provider configuration | Aligned. |
| 6 → 17 | Vault endpoint → runtime identity smoke | Aligned. |
| 6 → 18 | Vault metadata → final audit | Aligned. |
| 7 → 12 | Key resource ID → key-scoped Crypto User | Aligned. |
| 7 → 14 | Versioned Key identifier → app configuration | Aligned. |
| 7 → 15 | Key identifier → `ProtectKeysWithAzureKeyVault` | Aligned. |
| 7 → 17 | Key availability → persistence/restart smoke | Aligned. |
| 8 → 14 | Connection-string Secret → App Service reference | Aligned. |
| 8 → 17 | Referenced Secret → PostgreSQL connectivity | Aligned. |
| 8 → 18 | Validated runtime Secret → temporary-file deletion gate | Aligned. |
| 9 → 14 | Authorized slots → App settings allow-list | Aligned; no Meta/MP/WMC secrets. |
| 10 → 11 | Web App → system-assigned identity | Aligned. |
| 10 → 14 | Web App → settings target | Aligned. |
| 10 → 17 | Host/VNet integration → runtime smoke target | Plan gap resolved by the user-mandated VNet integration addition to Task 10. |
| 11 → 12 | `principalId` → Key Vault assignments | Aligned. |
| 11 → 13 | `principalId` → Storage assignments | Aligned. |
| 11 → 14 | System identity → Key Vault references | Aligned. |
| 11 → 17 | System identity → runtime access checks | Aligned. |
| 12 → 14 | Secret read permission → Key Vault reference resolution | Aligned. |
| 12 → 15 | Crypto permission → Data Protection provider | Aligned. |
| 12 → 17 | Vault permissions → runtime smoke | Aligned. |
| 13 → 15 | Data Protection container permission → key-ring I/O | Aligned. |
| 13 → 16 | Image container permission → blob CRUD | Aligned. |
| 13 → 17 | Blob permissions → runtime smoke | Aligned. |
| 14 ↔ 15 | `Program.cs`/Azure Data Protection settings contract | Shared-file ordering is safe: Task 14 configures cloud metadata; Task 15 owns code changes. |
| 14 ↔ 16 | `Program.cs`/Storage settings contract | Shared-file ordering is safe: Task 16 must preserve Task 15 registration. |
| 14 → 17 | App settings → deployed runtime checks | Blocked without separate deploy authorization. |
| 15 → 17 | Data Protection implementation → restart/cookie smoke | Code verification possible; cloud runtime verification blocked without deploy authorization. |
| 16 → 17 | Image storage implementation → blob delivery smoke | Code verification possible; cloud runtime verification blocked without deploy authorization. |
| 17 → 18 | Successful runtime validation → credential deletion/final audit | If cloud runtime checks remain unavailable, preserve the credential and report PARTIAL. |

## Resume point

First incomplete post-quota task: Task 10 — Create the App Service Plan and Web App After Quota, including the explicitly authorized VNet Integration before Task 11.

Baseline before Task 10: `dotnet build --nologo` passed (0 warnings, 0 errors); `dotnet test --nologo --no-build` passed (282/282, 0 failed, 0 skipped).

Task 10: blocked before mutation — read-only preflight passed (B1 limit 1, usage 0, runtime `DOTNETCORE|10.0`, App Service subnet valid/empty/delegated, PostgreSQL public access Disabled, targets absent), but the execution approval layer rejected creation of the persistent paid B1 App Service Plan. It treated the last directly typed trusted request as quota-only and requires a new explicit approval after disclosure that B1 may generate Azure charges. No Azure resource was created or changed; Task 10 remains incomplete and no downstream task was dispatched.

## Fix Round 2 — Task 10 capability evidence

- Scope: evidence/report correction only; no Azure mutation, code change, App Settings change, identity change, or deployment.
- Official evidence added to `task-10-report.md`: Basic supports Linux code/container apps, Virtual Network Integration, and Always On; the ARM site schema defines `httpsOnly` and `siteConfig.alwaysOn`.
- Fresh read-only state: `asp-orofoods-stg` is Linux Basic B1 capacity 1; `app-orofoods-stg-01` is Running; `httpsOnly=true`; `alwaysOn=true`; runtime `DOTNETCORE|10.0`.
- Scoped re-review: SPEC COMPLIANCE PASS, QUALITY PASS, SECURITY PASS; Important finding ADDRESSED; no new Important/Critical findings.
- Historical limitation retained: evidence was obtained after mutation and does not retroactively satisfy the original pre-mutation ordering checkpoint.
- Direct scoped re-review completed after an inconclusive subagent review: plan/app/config/VNet/PostgreSQL read-only checks passed; HTTPS Only and Always On are true; identity is null; no app settings exist.
- Final disposition: SPEC COMPLIANCE PASS, QUALITY PASS, SECURITY PASS; Important finding ADDRESSED; no new Important/Critical findings.
- Task 10 marked COMPLETE. Task 11 is next/ready but was not started.

## Task 11 — System Assigned Managed Identity

- Pre-check passed: `app-orofoods-stg-01` was `Running`, identity was `null`, and no Web App-scope role assignments existed.
- Authorized mutation completed: enabled only `SystemAssigned` identity.
- Result: principal ID `849a73c8-c64a-4681-925a-12819861942d`, tenant ID `0942578f-e863-4d5e-9103-c184f7332eb0`.
- Validation: no user-assigned identity, no App Settings, no Web App-scope RBAC, no deployment; PostgreSQL remained `Ready` with public access `Disabled`.
- Independent reviewer initially misapplied a deployment-evidence criterion; corrected scoped review treated the explicitly prohibited deployment as absent-by-design and returned SPEC COMPLIANCE PASS, QUALITY PASS, SECURITY PASS, no Important/Critical findings.
- Task 11: COMPLETE. Task 12 is next/ready but was not started.

## Task 12 — Minimum Key Vault RBAC for Managed Identity

- Pre-check passed: Web App system-assigned principal `849a73c8-c64a-4681-925a-12819861942d`, Key Vault RBAC enabled, Data Protection Key present.
- Created exactly two assignments:
	- `Key Vault Secrets User` at `kv-orofoods-stg-01` scope.
	- `Key Vault Crypto User` at key `dp-orofoods-stg` scope.
- Validation passed after controlled propagation check. No duplicate or broad roles were added.
- No Storage RBAC, App Settings, secret-value read, PostgreSQL change, deployment, or code change.
- Independent reviewer: SPEC COMPLIANCE PASS, QUALITY PASS, SECURITY PASS; no Important/Critical findings.
- Task 12: COMPLETE. Task 13 is next/ready but was not started.

## Task 13 — Minimum Storage RBAC for Managed Identity

- Pre-check passed: principal `849a73c8-c64a-4681-925a-12819861942d`, StorageV2 `storofoodsstg01`, private containers, public blob access and Shared Key disabled.
- Created exactly two `Storage Blob Data Contributor` assignments, both container-scoped: `product-images` and `data-protection`.
- No Storage Account-scope, resource-group, subscription, or tenant assignment was created.
- No account key, SAS, blob content, secret, App Setting, PostgreSQL, Key Vault, VNet, or OroBI change.
- Independent reviewer first lacked evidence; corrected scoped review verified all exact conditions and returned SPEC COMPLIANCE PASS, QUALITY PASS, SECURITY PASS, no Important/Critical findings.
- Task 13: COMPLETE. Task 14 is next/ready but was not started.

## Task 14 — Key Vault References and App Service Settings

- Pre-check passed: Web App Running, SystemAssigned identity, Key Vault/Storage RBAC effective, private PostgreSQL and private Storage baseline intact.
- Configured only approved non-secret settings plus `ConnectionStrings__DefaultConnection` as a versionless Key Vault reference to `orofoods-stg-default-connection`.
- Reference status: `Resolved`, SystemAssigned identity, correct vault and secret metadata; value not read.
- Configured Azure Blob product image metadata and Data Protection metadata; WhatsApp, WMC FileDrop, WMC Firebird, and WMC Sync remain disabled. No MercadoPago Enabled setting was invented.
- No deploy, migration, RBAC change, Storage change, Key Vault change, PostgreSQL change, or code change.
- Independent reviewer first hit an unsupported default API; corrected review used supported `2022-03-01` evidence and returned SPEC COMPLIANCE PASS, QUALITY PASS, SECURITY PASS, no Important/Critical findings.
- Task 14: COMPLETE. Task 15 is next/ready but was not started.

## Task 15 — Azure Data Protection Provider

- Implemented official Azure Data Protection Blob/Keys packages and `DefaultAzureCredential`.
- Staging/Production Azure mode now uses `PersistKeysToAzureBlobStorage` plus `ProtectKeysWithAzureKeyVault` with stable `Orofoods.Web` application name.
- Non-Development Azure mode fails fast when ApplicationName, BlobUri, or KeyVaultKeyIdentifier is missing; Development filesystem key storage remains intact.
- Focused tests passed 8/8; full suite passed 283/283; Release publish succeeded to `/tmp/orofoods-webapp-publish`.
- No Azure runtime/infrastructure mutation, deploy, migration, or external integration occurred.
- Independent reviewer: SPEC COMPLIANCE PASS, QUALITY PASS, SECURITY PASS; no Important/Critical findings.
- Pre-existing NU1903 package vulnerability warnings remain separate and unaddressed.
- No commit created: the touched files contained unrelated pre-existing dirty changes, so exact-path staging would have included unrelated work.
- Task 15: COMPLETE. Task 16 is next/ready but was not started.

## WEBAPP-6 Plan Amendment — Private Product Image Delivery

- Task 16 remains BLOCKED. Original reason preserved: `product-images` is private, while current consumers still render direct Blob references/URLs.
- Approved decision source: spec amendment commit `5f0d0a44c6b56daf1485946203b583e734ca8b76`, application-mediated delivery via `GET /media/products/{imageId:int}`.
- Plan amendment: Task 16 now owns the minimum `IProductImageStorage` read contract, Local/Azure implementations, media endpoint, authorization matrix, consumer URL changes, cache rules, security tests, build/test/publish checkpoint, and independent review.
- Anonymous delivery is limited to `Product.IsActive=true`; Portal preserves `ApprovedCustomer`; Admin preserves `Administrador`, including inactive products. No public Blob access, SAS, AccountKey, migration, or infrastructure change is authorized.
- Task 17 requires `Task 16 COMPLETE` plus private image delivery review `PASS` before deployment or runtime smoke tests.
- Tasks 1-15 remain preserved; no task was reopened or marked complete by this amendment.
- Plan amendment is created; Task 16 must be resumed only after the amended plan is consistent and approved.

## Task 16 — Private Product Image Delivery

- Architectural blocker resolved by approved spec commit `5f0d0a44c6b56daf1485946203b583e734ca8b76` and plan commit `bbbb0d495583bbc7e426dd2cd7df7220617d9c7d`.
- Implemented the provider-neutral `OpenReadAsync` contract, Local/Azure reads, secure `GET /media/products/{imageId:int}`, authorization matrix, cache headers, and application-generated image URLs for all identified consumers.
- ProductImage.Url remains an internal opaque storage reference; no migration, SAS, AccountKey, public Blob access, or direct Blob URL exposure was added.
- Focused Task 16 tests passed 20/20; full suite passed 297/297; Release publish succeeded at `/tmp/orofoods-webapp-publish`.
- Independent review: SPEC COMPLIANCE PASS, QUALITY PASS, SECURITY PASS; no Important/Critical findings.
- Build/test reported 16 pre-existing NU1903 warnings for `System.Security.Cryptography.Xml` 8.0.2; they were not suppressed or attributed to Task 16.
- No Azure mutation, deploy, migration, PostgreSQL, Key Vault, RBAC, VNet, Meta, Mercado Pago, WMC, or OroBI operation was performed.
- Task 16: COMPLETE. Task 17 is READY, subject to its recorded prerequisite and separate deployment authorization.
