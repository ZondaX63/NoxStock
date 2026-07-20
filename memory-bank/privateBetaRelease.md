# Private Beta Prototype Release

Last updated: 2026-07-18

## Objective

NoxStock development is frozen for new modules and broad optimization work. The active target is a private-beta prototype for 5-10 pilot tenants, limited users, and a recommended 5,000-10,000 products per tenant. Warehouse, reservation, e-Invoice, CRM, advanced accounting, broad report refactors, and target-scale performance work are out of scope for this release gate.

## Phase Status

- B1 - Release configuration: **Complete and runtime-verified**
- B2 - Backup and restore: **Complete and rehearsed**
- B3 - Critical-path browser smoke tests: **Complete with evidence**
- B4 - Release gate: **Complete for local/private-beta candidate validation**
- B5 - Pilot boundaries and release runbook: **Complete**

## B1 Evidence - Release Configuration

Completed on 2026-07-17 in the isolated `noxstock-release-validation` Compose project with runtime-only generated credentials and certificates. No secret value is stored in the repository or this Memory Bank.

- Web and DbMigrator fail closed when `App__RequireProductionEnvironment=true` and the host environment is not Production.
- The running Web container reported `Production`; required secret and OpenIddict certificate validation passed.
- TLS terminated at the isolated Nginx reverse proxy. Only its fixed address `172.31.251.10` is trusted, forwarded headers are limited to one hop, and direct HTTP requests to Web returned an HTTPS redirect.
- `https://localhost:8443/health/live` and `/health/ready` both returned HTTP 200 with production-safe `{"status":"Healthy"}` responses. Readiness includes the database check.
- `/swagger/index.html` resolved to the application 404 path in Production.
- PostgreSQL published no host port. The proxy was loopback-bound for local validation; Web was loopback-bound only for explicit direct-redirect diagnostics.
- PostgreSQL, Web, and proxy use `unless-stopped`. DbMigrator completed once with exit code 0.
- Release configuration integration tests pass as part of the 46-test Web suite, including wrong-environment rejection, required trusted proxy configuration, forwarded-chain budget enforcement, and live/ready health endpoints.

B1 release gate result: **Pass for configuration and local TLS/proxy runtime behavior**. Real deployment credentials and certificates must still be newly generated or rotated; previously exposed repository secrets must never be reused.

## B2 Evidence - Backup and Restore

Completed on 2026-07-17 against a read-only dump of the active local development PostgreSQL database. No source data was mutated.

- `etc/operations/postgres-backup.sh` creates a PostgreSQL custom-format dump, encrypts it with AES-256-CBC/PBKDF2 using a runtime passphrase file, writes a SHA-256 checksum, removes the plaintext temporary dump, and applies configurable retention.
- The backup passphrase and all other secret values remained outside the repository and are not recorded in logs, evidence, documentation, or the Memory Bank.
- `etc/operations/postgres-restore-rehearsal.sh` destroyed only the isolated `noxstock-restore-test` volume, restored the encrypted backup into the clean `NoxStockRestore` database, ran the current DbMigrator, executed tenant/commercial integrity SQL, and started the current Web image.
- DbMigrator completed the host, `asd`, and `asd2` tenant passes successfully.
- Restored counts: 2 tenants, 10 products, 3 invoices, 6 stock movements, and 6 business partners.
- OEM/product, document/partner, document-item/product, stock-movement/product, and account-transaction/account TenantId mismatch counts were all zero.
- Restored Web `/health/ready` returned `Healthy` with a successful database check.
- Evidence is stored in `docs/release/evidence/restore-rehearsal.txt`. The encrypted backup itself remains outside Git under the runtime backup directory.

B2 release gate result: **Pass**. A release must still be blocked if the target deployment cannot repeat this encrypted backup and clean restore procedure.

## B3 Evidence - Critical Browser Smoke Tests

Completed on 2026-07-17 against the isolated HTTPS release-validation environment.

- Browser evidence is stored in `docs/release/evidence/browser-smoke.json`.
- The Playwright suite provisions or reuses `atlas-demo`, `beta-isolation`, and `beta-inactive`.
- Validated host admin tenant provisioning, tenant activation/deactivation, tenant URL login, logout redirect, and tenant login retry.
- Validated product creation with foreign USD purchase cost, TRY sale currency, current exchange-rate pricing, and OEM search for `34116775277`.
- Validated POS sale and POS cancellation with stock, account, document, and reversal movement checks.
- Validated sales invoice creation and approval, collection/payment, current-account balance, and cash/account history checks.
- Validated dashboard data, dark theme, TR/EN language switching, tenant A/B isolation, and inactive tenant denial.
- Captured no browser page errors or HTTP 500 responses in the successful run.

B3 release gate result: **Pass** for critical-path browser smoke. The suite is a private-beta smoke gate, not a full E2E regression matrix.

## B4 Evidence - Release Gate

Current local validation status as of 2026-07-18:

- `dotnet build NoxStock.slnx -m:1 --no-restore` passed with 0 errors and 127 existing nullable/Mapperly warnings.
- `dotnet test NoxStock.slnx -m:1 --no-build` passed outside the sandbox because VSTest requires local socket access: Domain 2/2, EntityFrameworkCore 36/36, Web 46/46. Application and TestBase assemblies contain no discovered tests.
- Web security tests cover anonymous commercial endpoint 401/403 behavior plus host, tenant A, tenant B, tenant-to-host-admin, and inactive-tenant boundaries.
- Backup/restore evidence exists and restore health returned `Healthy`.
- Browser smoke evidence exists and passed.
- `git diff --check` initially found two whitespace issues; they were corrected on 2026-07-18 and must remain clean before commit.
- Docker runtime was not active during the 2026-07-18 review because the current Docker Desktop context socket was unavailable and the default context had no running containers. Target-environment Compose restart must still be repeated before opening a real pilot.

B4 release gate result: **Pass for code/test/documentation candidate state after final diff-check verification**. Target deployment remains blocked until fresh production secrets/certificates are rotated, target HTTPS reverse proxy behavior is verified, target backup/restore rehearsal passes, and Compose restart recovery is verified on the target host.

## B5 Evidence - Pilot Boundaries and Runbook

Completed on 2026-07-18.

- `docs/release/PRIVATE_BETA_RELEASE.md` now documents the NoxStock private beta runbook, release commands, runtime environment variables without values, secret-rotation requirements, backup/restore commands, browser smoke commands, release gate, onboarding, feedback collection, rollback steps, known limitations, and technical debt.
- `README.md` now states the private beta scope, supported critical flows, out-of-scope modules, and runbook link.
- Pilot scope remains 5-10 tenants, limited users, and 5,000-10,000 products per tenant.
- Unsupported beta items remain Warehouse/Branch/WarehouseStock rollout, stock reservation, e-Invoice/e-Archive, CRM automation, advanced accounting, broad report refactors, and target-scale concurrency claims.

B5 release gate result: **Pass**.
