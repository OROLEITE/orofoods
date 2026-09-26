# WEBAPP-6 Key Vault, Storage and Managed Identity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provision and integrate Staging-only Azure Key Vault, Blob Storage, Data Protection, and App Service Managed Identity without exposing secrets, changing Production, or executing external integrations before their own authorization.

**Architecture:** `kv-orofoods-stg-01` uses Azure RBAC, soft delete, and purge protection. `storofoodsstg01` uses Standard LRS, HTTPS-only, TLS 1.2+, and private containers `product-images` and `data-protection`. After the B1 quota is at least 1, the Linux App Service receives a system-assigned identity. That identity reads Key Vault secrets, uses the Data Protection Key for wrap/unwrap, and reads/writes the two Blob containers. Key Vault and Storage use public endpoints protected by TLS/RBAC in the first Staging pass; Private Endpoints remain a later hardening phase.

**Tech Stack:** Azure CLI 2.90.0, Azure App Service Linux/.NET 10, Azure Key Vault RBAC, Azure Storage Blob, `DefaultAzureCredential`, ASP.NET Core Data Protection, `AzureBlobProductImageStorage`, PostgreSQL Flexible Server, xUnit.

**Spec:** docs/superpowers/specs/2026-09-21-webapp-6-keyvault-storage-managed-identity-design.md

## Global Constraints

- Do not execute this plan during planning review.
- No Azure `create`, `update`, `delete`, role assignment, secret import, or deployment is part of this document creation.
- Never touch `orobi-postgres`, Production resources, Meta, Mercado Pago, or WMC.
- Do not run migrations, copy Development data, create database objects, or change the PostgreSQL server.
- Do not put passwords, tokens, key material, connection strings, Azure auth state, downloads, or temporary credential files in Git.
- Do not print passwords or secrets in commands, logs, test output, or reports.
- Preserve the existing dirty worktree; each implementation task must stage only its own intended files.
- Use explicit checkpoints. Stop when a checkpoint fails; do not silently change SKU, region, network model, or authentication model.
- Staging names: Resource Group `rg-orofoods-stg-brazilsouth`, Key Vault `kv-orofoods-stg-01`, Storage Account `storofoodsstg01`, containers `product-images` and `data-protection`, Data Protection Key `dp-orofoods-stg`.
- Staging PostgreSQL remains `psql-orofoods-stg-01`, database `orofoods`, and public network access `Disabled`.
- Initial Key Vault and Storage network model is public endpoint plus TLS/RBAC, not Private Endpoint.
- If B1 quota remains `0`, stop after pre-quota preparation. Do not choose another App Service SKU automatically.

## Review Focus

- B1 App Service quota must be read-only validated immediately before any App Service task.
- Managed Identity must exist before any identity-scoped RBAC assignment; verify the returned `principalId`.
- RBAC does not bypass a Key Vault or Storage firewall; verify network reachability separately.
- `dp-orofoods-stg` is a Key Vault **Key**, not a Key Vault Secret. Application credentials remain Secrets.
- Secret values must be supplied through a protected interactive/local mechanism and must never appear in shell history, debug output, Git, or reports.
- The current code has typed Storage/Data Protection options and `DefaultAzureCredential`, but the actual Azure Data Protection provider calls still need implementation and focused tests.
- The current `AzureBlobProductImageStorage` uploads and deletes blobs and returns an opaque Blob reference; user-facing delivery requires the approved application endpoint before private images are enabled. Anonymous delivery is limited to `Product.IsActive=true`; Portal preserves `ApprovedCustomer`, Admin preserves `Administrador`; no SAS is used in this phase.

## Task 1: Pre-flight Azure State and Names

**Files:**
- Create: none
- Modify: none
- Test: read-only Azure CLI checks

**Interfaces:**
- Consumes: authenticated subscription `Empresas`, existing Staging resource IDs, current quota state
- Produces: approved inventory and a checkpoint decision

- [ ] Confirm subscription `Empresas`, `brazilsouth`, and Resource Group `rg-orofoods-stg-brazilsouth` using `az account show` and `az group show`.
- [ ] Confirm the existing PostgreSQL server is `psql-orofoods-stg-01`, database `orofoods`, and public network access is `Disabled`.
- [ ] Confirm the App Service subnet is `10.20.1.0/26`, empty, delegated to `Microsoft.Web/serverFarms`, and attached to `vnet-orofoods-stg`.
- [ ] Validate global availability of `kv-orofoods-stg-01` and `storofoodsstg01` with read-only name checks.
- [ ] Validate the containers and Data Protection Key do not already exist in the Staging resources unless a prior approved run created them.
- [ ] Confirm no Production resource is being targeted.
- [ ] Checkpoint: record `PASS` only when every identity, region, network, and name matches; otherwise stop without mutation.

## Task 2: Validate App Service Quota Gate

**Files:**
- Create: none
- Modify: none
- Test: read-only quota/capability query

**Interfaces:**
- Consumes: Microsoft.Web regional quota for `brazilsouth`
- Produces: `QUOTA_READY=true` only when B1 limit is at least 1

- [ ] Query the App Service B1 quota without creating a plan.
- [ ] Record the previous known state (`Current Limit: 0`, `Current Usage: 0`) and revalidate it against the latest Azure response.
- [ ] If the limit is `0`, stop App Service tasks and leave Storage/Key Vault pre-quota tasks available.
- [ ] If the limit is at least `1`, continue later with exactly B1 and one worker; do not autoscale or substitute another SKU.
- [ ] Checkpoint: require an explicit quota result before Task 9.

## Task 3: Create Staging Storage Account

**Files:**
- Create: none
- Modify: none
- Test: Azure read-only property validation

**Interfaces:**
- Consumes: Task 1 name and region validation
- Produces: `storofoodsstg01` with secure baseline settings

- [ ] Create `storofoodsstg01` in `rg-orofoods-stg-brazilsouth`, `brazilsouth`, with Standard performance and LRS redundancy.
- [ ] Set HTTPS-only, minimum TLS 1.2, and public blob access disabled during creation.
- [ ] Keep Shared Key access disabled if all required tooling and runtime paths support Entra/RBAC; if a required bootstrap command cannot operate without it, stop and record the narrow exception for approval rather than enabling it silently.
- [ ] Apply tags `Application=Orofoods`, `Environment=Staging`, `Region=BrazilSouth`, `ManagedBy=AzureCLI`.
- [ ] Validate SKU, replication, TLS, HTTPS-only, public access, tags, and public network mode with `az storage account show`.
- [ ] Rollback: correct an incorrect property in place; do not delete the account automatically.
- [ ] Checkpoint: continue only when the account is Staging-only and no public anonymous blob access is possible.

## Task 4: Create Private Product Images Container

**Files:**
- Create: none
- Modify: none
- Test: read-only container access-level validation

**Interfaces:**
- Consumes: `storofoodsstg01`
- Produces: private container `product-images`

- [ ] Create `product-images` with anonymous public access disabled.
- [ ] Validate its access level is private and no permanent SAS is created.
- [ ] Record that the current `AzureBlobProductImageStorage` needs the approved application-mediated delivery path before production-like private images can be displayed; do not add SAS.
- [ ] Rollback: remove an accidental public access setting in place; do not delete existing product data.
- [ ] Checkpoint: stop if the container is public or if the command requires a credential that would be exposed in history.

## Task 5: Create Private Data Protection Container

**Files:**
- Create: none
- Modify: none
- Test: read-only container access-level validation

**Interfaces:**
- Consumes: `storofoodsstg01`
- Produces: private container `data-protection`

- [ ] Create `data-protection` with anonymous public access disabled.
- [ ] Validate it is separate from `product-images` and contains no application images or secrets.
- [ ] Do not upload a key ring until the application provider configuration and identity permissions pass their checkpoints.
- [ ] Rollback: correct public access or metadata in place; do not delete the container automatically.
- [ ] Checkpoint: require a private container before Data Protection integration.

## Task 6: Create Staging Key Vault

**Files:**
- Create: none
- Modify: none
- Test: read-only Key Vault property validation

**Interfaces:**
- Consumes: Task 1 name validation
- Produces: `kv-orofoods-stg-01` with Azure RBAC authorization

- [ ] Create the Key Vault in `rg-orofoods-stg-brazilsouth`, `brazilsouth`, with RBAC authorization enabled.
- [ ] Enable soft delete and purge protection before any real credential is stored.
- [ ] Use the approved public endpoint bootstrap model with TLS; do not add a Private Endpoint in WEBAPP-6.
- [ ] Apply the four Staging tags.
- [ ] Validate RBAC authorization, soft delete, purge protection, location, public network setting, and tags.
- [ ] Rollback: correct configuration in place; do not purge or delete the vault automatically because purge protection is intentional.
- [ ] Checkpoint: no real secret may be imported until the vault properties are verified.

## Task 7: Create the Data Protection Key

**Files:**
- Create: none
- Modify: none
- Test: read-only Key metadata validation

**Interfaces:**
- Consumes: `kv-orofoods-stg-01`
- Produces: Key Vault Key `dp-orofoods-stg` for wrap/unwrap

- [ ] Create an RSA Key named `dp-orofoods-stg` with the key operations required by the Azure Data Protection provider: wrap and unwrap.
- [ ] Do not create a Secret with the same purpose or put key material in a file.
- [ ] Validate key type, enabled state, key operations, vault, and version identifier without printing material.
- [ ] Rollback: disable or rotate the key version after an approved incident; do not purge the vault or key automatically.
- [ ] Checkpoint: record the non-secret key identifier for application configuration.

## Task 8: Import the PostgreSQL Credential Securely

**Files:**
- Create: none
- Modify: none
- Test: Key Vault secret metadata validation

**Interfaces:**
- Consumes: temporary mode-600 PostgreSQL password file outside the repository
- Produces: `orofoods-stg-default-connection` in Key Vault, with no value in logs

- [ ] Confirm the password file is outside the repository and mode `600`; do not print or read it into terminal output.
- [ ] Build the PostgreSQL connection value in a protected process input or secure local mechanism, never as a literal shell argument and never through command history.
- [ ] Store the connection value as `orofoods-stg-default-connection` only after the Key Vault RBAC/bootstrap path is ready.
- [ ] Validate only secret name, enabled state, content type, and version metadata; never retrieve or display the value.
- [ ] Keep the file until the App Service can connect through the private VNet path and the secret is validated in runtime.
- [ ] Rollback: disable or replace the secret version; do not delete the PostgreSQL server or print the old value.
- [ ] Checkpoint: require a successful secret metadata check and a recorded secure file owner/mode.

## Task 9: Define Only Authorized Secret Slots

**Files:**
- Create: none
- Modify: none
- Test: configuration-to-secret mapping review

**Interfaces:**
- Consumes: real configuration classes in `Orofoods.Web/Services/Commercial/WhatsAppBusinessOptions.cs`, `Orofoods.Web/Services/Payments/MercadoPagoOptions.cs`, `Orofoods.Web/Integrations/Erp/Wmc/WmcFirebirdOptions.cs`, and `Orofoods.Web/Program.cs`
- Produces: approved names, not empty secret values

- [ ] Map `Jwt:Key` to `orofoods-stg-jwt-key`.
- [ ] Map `MercadoPago:AccessToken` and `MercadoPago:WebhookSecret` to their approved Key Vault secret names only when the integration is authorized and real values exist.
- [ ] Map `WhatsAppBusiness:AccessToken`, `AppSecret`, and `VerifyToken` to their approved names only when WEBAPP-10/CRM-8 is authorized.
- [ ] Reserve `orofoods-stg-wmc-firebird-password` without creating it because WMC is explicitly out of scope.
- [ ] Keep `PhoneNumberId`, `BusinessAccountId`, `GraphApiVersion`, `MercadoPago:BaseAddress`, and WMC host/port/database/user as non-secret configuration.
- [ ] Checkpoint: no empty placeholder secrets and no Meta/Mercado Pago/WMC calls.

## Task 10: Create the App Service Plan and Web App After Quota

**Files:**
- Create: none
- Modify: none
- Test: read-only resource validation

**Interfaces:**
- Consumes: `QUOTA_READY=true`, `asp-orofoods-stg`, `app-orofoods-stg-01`, Linux `DOTNETCORE:10.0`
- Produces: one public HTTPS Web App eligible for VNet Integration

- [ ] Re-run Task 2 immediately before creation.
- [ ] Create Linux App Service Plan `asp-orofoods-stg` in `brazilsouth`, SKU B1, one worker, with no autoscale.
- [ ] Create Web App `app-orofoods-stg-01` using the exact runtime string returned by `az webapp list-runtimes --os-type linux` for `.NET 10 LTS` (`DOTNETCORE:10.0` at planning time).
- [ ] Do not deploy code, configure application settings, configure custom domain, enable Managed Identity, or add secrets in this task.
- [ ] Enable HTTPS Only and Always On only after the plan and app exist and the SKU reports those capabilities.
- [ ] Validate hostname, Linux kind, runtime, HTTPS Only, Always On, one instance, and absence of deployment/application settings.
- [ ] Rollback: stop and report any quota/runtime error; do not change SKU or delete the plan/app automatically.
- [ ] Checkpoint: continue only when the Web App exists and its quota-supported configuration is correct.

## Task 11: Enable System-Assigned Managed Identity

**Files:**
- Create: none
- Modify: none
- Test: read-only identity metadata validation

**Interfaces:**
- Consumes: existing `app-orofoods-stg-01`
- Produces: `principalId` for the system-assigned identity

- [ ] Enable only the Web App system-assigned identity.
- [ ] Capture `principalId` and tenant context without printing tokens.
- [ ] Confirm the identity is attached to `app-orofoods-stg-01` and no user-assigned identity was created.
- [ ] Rollback: disable the system identity only if the task is explicitly rolled back and no role assignments depend on it; do not delete a separate identity because none is planned.
- [ ] Checkpoint: no RBAC assignment proceeds without the exact principal ID.

## Task 12: Grant Minimum Key Vault RBAC

**Files:**
- Create: none
- Modify: none
- Test: read-only role assignment validation

**Interfaces:**
- Consumes: Web App `principalId`, Key Vault resource ID, Key ID
- Produces: identity access to secrets and Data Protection crypto operations

- [ ] Assign `Key Vault Secrets User` to the Web App identity at the Staging Key Vault scope.
- [ ] Assign `Key Vault Crypto User` at the `dp-orofoods-stg` Key scope only.
- [ ] Do not assign Owner, Contributor, or Key Vault Administrator.
- [ ] Validate role definition names, principal ID, scope, and assignment state with read-only queries.
- [ ] Rollback: remove only an incorrect role assignment by exact ID; do not delete the vault or key.
- [ ] Checkpoint: identity can be authorized without granting management-plane access.

## Task 13: Grant Minimum Storage RBAC

**Files:**
- Create: none
- Modify: none
- Test: read-only role assignment validation

**Interfaces:**
- Consumes: Web App `principalId`, Storage Account and container resource IDs
- Produces: Blob data access for images and Data Protection

- [ ] Assign `Storage Blob Data Contributor` at `product-images` container scope for upload, read, replace, and delete.
- [ ] Assign `Storage Blob Data Contributor` at `data-protection` container scope for key-ring read/write.
- [ ] If the platform cannot use container scope for the required assignment, stop and record explicit approval before using account scope.
- [ ] Validate role names, scopes, principal ID, and assignment state; do not use account keys.
- [ ] Rollback: remove only the incorrect assignment by exact ID; do not delete containers.
- [ ] Checkpoint: both containers remain private and identity permissions are data-plane only.

## Task 14: Configure App Service Key Vault References

**Files:**
- Modify: `Orofoods.Web/Program.cs` only if runtime validation requires a code-side configuration contract
- Test: App Service configuration read-only validation and startup smoke test

**Interfaces:**
- Consumes: Key Vault secret IDs, Web App identity, PostgreSQL FQDN
- Produces: `ConnectionStrings__DefaultConnection` and other approved settings without secret literals

- [ ] Store non-secret host/database/user configuration in App Service settings and reference the password through Key Vault.
- [ ] Use the existing application contract `ConnectionStrings:DefaultConnection`; App Service environment naming is `ConnectionStrings__DefaultConnection`.
- [ ] Prefer a single Key Vault reference for the complete connection string (`orofoods-stg-default-connection`) to avoid reconstructing a password-bearing value in shell scripts.
- [ ] Add only approved non-secret settings for JWT issuer/audience, Meta IDs/graph version when authorized, Mercado Pago base URL/public key when authorized, and Storage/Data Protection URIs.
- [ ] Do not add real Meta, Mercado Pago, WMC, JWT, or connection values to tracked files.
- [ ] Validate references resolve as configuration metadata/status, not by printing their values.
- [ ] Rollback: remove or correct only the specific setting; do not disable HTTPS, public-only PostgreSQL, or RBAC.
- [ ] Checkpoint: no secret value appears in App Service configuration output, logs, or Git.

## Task 15: Implement Azure Data Protection Provider

**Files:**
- Modify: `Orofoods.Web/Program.cs`
- Modify: `Orofoods.Web/Models/Configuration/DataProtectionOptions.cs` if the existing typed options need explicit Azure storage/key fields
- Modify: `Orofoods.Web/Orofoods.Web.csproj` for the official Azure Data Protection Blob/Key Vault packages if absent
- Test: `Orofoods.Web.Tests/Infrastructure/ApplicationSecurityConfigurationTests.cs` and a focused Data Protection configuration test

**Interfaces:**
- Consumes: `DataProtection:Azure:BlobUri`, `DataProtection:Azure:KeyVaultKeyIdentifier`, `DefaultAzureCredential`
- Produces: persistent encrypted key ring in `data-protection`, protected by `dp-orofoods-stg`

- [ ] Add only the official ASP.NET Core Azure Data Protection integrations required by the target framework; do not duplicate existing Azure SDK references.
- [ ] When non-Development and `DataProtection:Azure:Enabled=true`, construct `DefaultAzureCredential`, call `PersistKeysToAzureBlobStorage`, and call `ProtectKeysWithAzureKeyVault` with the Key ID.
- [ ] Preserve local Development filesystem key behavior when the Local provider is selected.
- [ ] Fail fast when Azure mode is enabled and Blob URI or Key ID is missing; do not silently fall back to local keys in Staging/Production.
- [ ] Add tests for missing Azure configuration, Local Development behavior, and Azure configuration registration without contacting Azure.
- [ ] Validate key-ring persistence and restart behavior only in a later deployed smoke test.
- [ ] Rollback: revert only the configuration/provider change and keep the Blob container/key intact for investigation.
- [ ] Checkpoint: build and focused tests pass before Storage provider changes.

## Task 16: Configure AzureBlobProductImageStorage

**Current status:** BLOCKED. Resume only after this amended plan is approved and implement the complete private image delivery contract below. Do not mark this task complete during planning or before the implementation review passes.

**Files:**
- Modify: `Orofoods.Web/Services/Storage/IProductImageStorage.cs` with the minimum application-level read contract (`OpenReadAsync` or equivalent)
- Modify: `Orofoods.Web/Services/Storage/LocalProductImageStorage.cs` and `Orofoods.Web/Services/Storage/AzureBlobProductImageStorage.cs` to implement the same read contract
- Create/modify: application media endpoint for `GET /media/products/{imageId:int}` using existing MVC routing and authorization patterns
- Modify: product image View/DTO consumers under Home, Portal, Admin Products, and `Controllers/Api/V1/CatalogController.cs` to generate application URLs
- Modify: `Program.cs` only for required route/registration wiring; do not alter Azure infrastructure configuration
- Test: focused storage, media endpoint, consumer, authorization, cache, and security tests in `Orofoods.Web.Tests`

**Interfaces:**
- Consumes: `Storage:Provider=AzureBlob`, Storage service URI, private `product-images` container, `DefaultAzureCredential`, `ProductImage` ID/reference, existing `ApprovedCustomer` policy, and `Administrador` role
- Produces: `IProductImageStorage` read/write/delete behavior, application URL `GET /media/products/{imageId:int}`, validated stream/content type, and no direct Blob URI exposure

- [ ] Keep Development default `Storage:Provider=Local` and verify no Azure call occurs in Local mode.
- [ ] Configure Staging to use the `product-images` container and private access.
- [ ] Preserve current file validation, generated names, content-type checks, and delete/replace semantics.
- [ ] Extend `IProductImageStorage` with the minimum application-level read result containing only `Stream`, validated `ContentType`, and strictly necessary filename/metadata; do not expose Azure SDK types to controllers.
- [ ] Implement `OpenReadAsync` (or the approved equivalent) in Local and Azure providers. Local must resolve only its own generated references; Azure must resolve only the configured `product-images` container through `DefaultAzureCredential`.
- [ ] Implement `GET /media/products/{imageId:int}` using a database-controlled `ProductImage` ID. Reject arbitrary Blob URI, path, filesystem path, account, container, external URL, traversal, SSRF, and open-redirect inputs.
- [ ] For anonymous requests, deliver only images whose product has `Product.IsActive=true`; do not use `Product.IsAvailable` as the publication predicate. Preserve `ApprovedCustomer` for Portal access and `Administrador` for Admin access, including inactive products managed by Admin.
- [ ] Return `200` with a stream and validated Content-Type, `404` for missing image/product/blob/reference, and sanitized errors without Storage details, URI, credentials, or tokens.
- [ ] Update Home, public catalog, Portal, Admin Products, `CatalogController`/`ImageUrl`, and all discovered partial/card consumers to emit `/media/products/{imageId}` via `Url.Action`, `LinkGenerator`, or existing equivalent. Never expose `ProductImage.Url` directly and never persist `/media/products/...` or SAS.
- [ ] Apply `Cache-Control: public, max-age=300, must-revalidate` only to anonymous active-product delivery; use `Cache-Control: private, max-age=300, must-revalidate` for Portal/Admin, including inactive products.
- [ ] Keep `product-images` private and use only `DefaultAzureCredential` -> System Assigned Managed Identity. Do not add AccountKey, permanent SAS, dynamic SAS, public access, RBAC, Storage, Key Vault, PostgreSQL, or migration changes.
- [ ] Add tests for existing/missing image and image ID, missing blob/reference, invalid reference, traversal, Content-Type, `Product.IsActive`, public catalog, `ApprovedCustomer`, `Administrador`, inactive products, Local provider, Azure provider without Azure calls, public/private cache, no Blob URI/SAS/Storage key exposure, and preserved replace/delete behavior.
- [ ] Add consumer/source assertions proving no View, DTO, API response, or card renders a private Blob URI.
- [ ] Rollback: select Local only in Development; in Staging fail fast rather than silently writing to ephemeral local storage.
- [ ] Checkpoint: focused tests, security review, build, full test suite, and Release publish pass. Record existing NU1903 warnings separately; do not suppress or attribute them automatically to this task.

## Task 17: Integrated Validation and Security Smoke Tests

**Files:**
- Create: focused integration test or deployment smoke-test notes only when needed
- Modify: no functional production file; Task 16 owns the private delivery implementation
- Test: solution build, unit tests, publish, and authorized Staging smoke tests

**Interfaces:**
- Consumes: configured identity, Key Vault, Blob containers, PostgreSQL private network, deployed application
- Produces: evidence of secure runtime behavior

**Preconditions:** Task 16 is `COMPLETE` and its independent private image delivery review is `PASS`. No Task 17 deployment or runtime smoke test may bypass either condition.

- [ ] Run `dotnet build --nologo`.
- [ ] Run `dotnet test --nologo`.
- [ ] Run `dotnet publish Orofoods.Web/Orofoods.Web.csproj -c Release -o /tmp/orofoods-webapp-publish`.
- [ ] Validate the Managed Identity can read an approved Key Vault secret without logging its value.
- [ ] Validate the identity can upload, read through the approved application-mediated delivery path, replace, and delete a test image while `product-images` remains private. Cover anonymous active-product delivery, `ApprovedCustomer` Portal delivery, and `Administrador` delivery for inactive products.
- [ ] Validate the Data Protection key ring is persisted in `data-protection` and remains usable after an App Service restart/redeploy.
- [ ] Validate login cookie continuity across restart; do not use a local filesystem key ring in Staging.
- [ ] Validate PostgreSQL connectivity through App Service VNet Integration without enabling public access or firewall rules.
- [ ] Inspect logs for PostgreSQL password, JWT key, Meta tokens, Mercado Pago tokens, Key Vault values, storage credentials, and Authorization headers.
- [ ] Checkpoint: stop on any secret exposure, anonymous blob access, identity failure, or local-key fallback.

## Task 18: Remove Temporary Credential and Final Audit

**Files:**
- Create: no tracked file
- Modify: no source file unless a tested configuration issue remains
- Test: metadata checks, Git/security audit

**Interfaces:**
- Consumes: successful runtime validation from Task 17
- Produces: no temporary PostgreSQL password file and an auditable Staging configuration

- [ ] Confirm Key Vault secret metadata and App Service reference status without retrieving the secret value.
- [ ] Confirm the application connected to the intended `psql-orofoods-stg-01`/`orofoods` endpoint through the private path.
- [ ] Remove the temporary password file only after those checks pass; never print its contents.
- [ ] Confirm no temporary password file, Azure CLI auth state, downloaded package, secret, or generated output is tracked by Git.
- [ ] Run `git diff --check` scoped to implementation files and `git status --short`.
- [ ] Confirm Staging and Production resource IDs, Key Vaults, Storage Accounts, keys, key rings, and secrets are distinct.
- [ ] Rollback: if validation fails before deletion, retain the protected file and stop; after deletion, rotate the PostgreSQL credential through an authorized controlled procedure rather than restoring the file.
- [ ] Checkpoint: mark WEBAPP-6 complete only after the final audit is clean.

## Commit Strategy

- [ ] Commit each coherent implementation slice separately, for example Storage foundation, Key Vault foundation, App Service identity/RBAC, runtime configuration, and validation tests.
- [ ] Use `git add --` followed by the exact intended paths for every commit and inspect `git diff --cached` before committing.
- [ ] Never stage unrelated existing modifications in `Orofoods.Web`, `Orofoods.Web.Tests`, `Info`, temporary outputs, or Azure CLI state.
- [ ] Use messages that identify the slice, such as `feat: provision staging blob storage foundation` or `feat: configure Azure data protection`.
- [ ] Do not push automatically.

## Final Implementation Acceptance

- [ ] Storage Account is Standard/LRS, HTTPS-only, TLS 1.2+, and public blob access is disabled.
- [ ] Both containers are private and separate.
- [ ] Key Vault uses RBAC, soft delete, purge protection, and Staging-only scope.
- [ ] `dp-orofoods-stg` is a Key Vault Key with wrap/unwrap capability, not a Secret.
- [ ] Only the system-assigned Web App identity receives the minimum Key Vault and Storage data roles.
- [ ] B1 quota was explicitly checked before App Service creation; no automatic SKU substitution occurred.
- [ ] PostgreSQL remains private-only and unchanged.
- [ ] Data Protection uses Azure Blob plus Key Vault in Staging and Local filesystem only in Development.
- [ ] Product image storage uses the Azure provider only in Staging and has the approved application-mediated private delivery path with the anonymous active-product/Portal/Admin authorization matrix.
- [ ] No real secret appears in Git, command history, logs, or reports.
- [ ] Build, tests, publish, identity, secret, Blob, Data Protection restart, cookie, and PostgreSQL private-connectivity checks pass.
- [ ] The temporary PostgreSQL credential file is removed only after successful validation.