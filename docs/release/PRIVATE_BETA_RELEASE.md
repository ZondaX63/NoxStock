# NoxStock Private Beta Prototype Release

Last updated: 2026-07-18

This runbook is for the first controlled NoxStock private beta. It is not a target-scale production certification. The beta target is 5-10 active tenants, limited users, and 5,000-10,000 products per tenant.

## Release Decision

Status: private beta candidate after target-environment secret rotation and target-environment backup/restore proof.

Completed evidence in this repository:

- B1 release configuration was verified in an isolated Production-mode TLS/reverse-proxy Compose stack.
- B2 encrypted PostgreSQL backup and clean restore rehearsal passed.
- B3 critical browser smoke test passed for tenant login/logout, product/OEM search, POS sale/cancel, invoice approval, collection/payment, dashboard, TR/EN/theme, tenant A/B isolation, and inactive tenant denial.
- B4 local gate checks passed for build, existing .NET tests, browser smoke evidence, anonymous commercial endpoint 401/403 coverage, health endpoints, and restore evidence.
- B5 pilot limits and known constraints are documented here and in the README.

Evidence files:

- `docs/release/evidence/browser-smoke.json`
- `docs/release/evidence/restore-rehearsal.txt`
- `docs/operations/security-secret-rotation.md`

## Pilot Scope

- Tenant limit: 5-10 active tenants.
- Product limit: 5,000-10,000 product cards per tenant.
- User concurrency: 5-10 active users per tenant.
- Supported access model: host admin creates and manages tenant companies; tenant users enter through `/t/{tenant-name}/Account/Login`.
- Supported currency model: products can be bought in TRY/USD/EUR and sold in a selected sale currency using the current exchange-rate flow.
- Supported critical flows: tenant login/logout, product create/search including OEM, POS sale and cancellation, sales invoice creation/approval, collection/payment, account and current-account movement checks, dashboard overview, TR/EN switching, dark/light theme.

## Out Of Scope For This Beta

- New Warehouse, Branch, WarehouseStock, StockReservation, and stock-transfer domain rollout.
- e-Invoice/e-Archive integrations.
- CRM automation.
- Advanced accounting.
- Broad report refactors.
- Cursor pagination, cache rollout, and large query/index refactors beyond measured fixes.
- 100,000 products per tenant or 100-300 concurrent user claims.

## Runtime Environment Variables

Set these values from a runtime secret source. Do not commit values to Git, tickets, screenshots, logs, or the Memory Bank.

Required for `docker-compose.production.yml`:

- `NOXSTOCK_DB_PASSWORD`
- `NOXSTOCK_ADMIN_PASSWORD`
- `NOXSTOCK_PUBLIC_URL`
- `NOXSTOCK_CERTIFICATE_PASSPHRASE`
- `NOXSTOCK_STRING_ENCRYPTION_PASSPHRASE`
- `NOXSTOCK_OPENIDDICT_PFX_PATH`
- `NOXSTOCK_TRUSTED_PROXY_IPS`

Optional:

- `NOXSTOCK_HTTP_PORT`
- `NOXSTOCK_FORWARDED_HEADER_LIMIT`
- `NOXSTOCK_BACKUP_DIR`
- `NOXSTOCK_BACKUP_RETENTION_DAYS`
- `NOXSTOCK_BACKUP_PASSPHRASE_FILE`
- `NOXSTOCK_BACKUP_COMPOSE_FILE`
- `NOXSTOCK_BACKUP_DB_SERVICE`
- `NOXSTOCK_BACKUP_DATABASE`
- `NOXSTOCK_BACKUP_DB_USER`
- `DOCKER_CONTEXT`

Production must not start in Development mode. The Web and DbMigrator fail closed when `App__RequireProductionEnvironment=true` and the runtime environment is not Production.

## Secret Rotation Checklist

Before opening the beta:

- Rotate every real secret that has ever existed in the repository history or local examples.
- Generate a fresh PostgreSQL password.
- Generate a fresh host admin password.
- Generate and mount a production OpenIddict certificate at `/app/openiddict.pfx`.
- Generate a fresh OpenIddict certificate passphrase.
- Generate a fresh ABP string-encryption passphrase.
- Verify the trusted reverse proxy IP list.
- Verify HTTPS public URL values for `App__SelfUrl` and `AuthServer__Authority`.
- Keep PostgreSQL unavailable from the public network.

Use `docs/operations/security-secret-rotation.md` as the detailed rotation checklist.

## Deploy Commands

Build and run migrations:

```bash
docker compose -f docker-compose.production.yml build
docker compose -f docker-compose.production.yml up db-migrator
```

Start the private beta stack:

```bash
docker compose -f docker-compose.production.yml up -d postgres web
docker compose -f docker-compose.production.yml ps
```

The production Compose file binds Web to loopback by default and expects an external trusted HTTPS reverse proxy in front of it.

## Health Checks

From the application host:

```bash
curl -fsS -H 'X-Forwarded-Proto: https' http://127.0.0.1:8080/health/live
curl -fsS -H 'X-Forwarded-Proto: https' http://127.0.0.1:8080/health/ready
```

From the public URL:

```bash
curl -fsS https://YOUR_PUBLIC_NOXSTOCK_HOST/health/live
curl -fsS https://YOUR_PUBLIC_NOXSTOCK_HOST/health/ready
```

Expected Production response is status-only. Detailed health output must remain disabled.

## Backup Command

Create an encrypted PostgreSQL backup:

```bash
export NOXSTOCK_BACKUP_PASSPHRASE_FILE=/runtime/secrets/noxstock-backup-passphrase
export NOXSTOCK_BACKUP_DIR=/var/backups/noxstock
etc/operations/postgres-backup.sh
```

Requirements:

- Backup passphrase is stored outside Git.
- Backup files are stored off-host or replicated off-host.
- Retention is configured with `NOXSTOCK_BACKUP_RETENTION_DAYS`.
- At-rest encryption and access control are handled by the host/provider.

## Restore Rehearsal

Run restore rehearsal into the isolated restore-test Compose project:

```bash
export NOXSTOCK_BACKUP_PASSPHRASE_FILE=/runtime/secrets/noxstock-backup-passphrase
etc/operations/postgres-restore-rehearsal.sh /var/backups/noxstock/noxstock-YYYYMMDDTHHMMSSZ.dump.enc
```

Success requires:

- The encrypted dump decrypts successfully.
- `pg_restore` exits cleanly.
- Current DbMigrator completes.
- Tenant/product/invoice/stock/account integrity checks pass.
- Restore Web starts and `/health/ready` returns healthy.

The last local rehearsal evidence is in `docs/release/evidence/restore-rehearsal.txt`.

## Browser Smoke Test

The Playwright smoke suite expects a running HTTPS validation or beta environment.

```bash
cd test/NoxStock.BrowserTests
export NOXSTOCK_E2E_BASE_URL=https://YOUR_PUBLIC_NOXSTOCK_HOST
export NOXSTOCK_E2E_HOST_USER=admin
export NOXSTOCK_E2E_HOST_PASSWORD="$(cat /runtime/secrets/noxstock-host-password)"
export NOXSTOCK_E2E_TENANT_PASSWORD="$(cat /runtime/secrets/noxstock-tenant-password)"
npm test
```

Do not commit literal passwords. In local validation, export actual values from a runtime-only env file or secret manager.

The test provisions or reuses:

- `atlas-demo`
- `beta-isolation`
- `beta-inactive`

It then validates tenant URL login, logout, product create/OEM search, POS sale/cancel, invoice approval, collection/payment, dashboard, theme and language switching, tenant isolation, and inactive tenant denial.

## Release Gate

Do not open a tenant to beta users unless all items below are green in the target environment:

- `dotnet build NoxStock.slnx -m:1 --no-restore`
- `dotnet test NoxStock.slnx -m:1 --no-build`
- `git diff --check`
- Browser smoke test passes.
- Anonymous commercial endpoints return 401/403.
- Tenant cross-access test passes.
- `/health/live` and `/health/ready` pass through the public HTTPS address.
- PostgreSQL has no direct public port.
- Web and PostgreSQL recover after Compose restart.
- Encrypted backup and clean restore rehearsal pass using a fresh backup from the target environment.
- Production secrets and previously exposed credentials are rotated.

## Pilot Onboarding

1. Confirm the tenant is in the private beta list.
2. Host admin signs in at `/Account/Login`.
3. Create the tenant from Tenant Management.
4. Set tenant status to active.
5. Send the tenant URL `/t/{tenant-name}/Account/Login` through the agreed private support channel.
6. Require tenant admin password change before real operational use.
7. Ask pilot users to report bugs with trace ID, tenant name, page URL, expected action, and timestamp.

## Feedback Collection

Collect feedback in a single private channel or issue tracker with:

- Tenant name.
- User role.
- Page URL.
- Operation attempted.
- Trace ID if an error page is shown.
- Screenshot or screen recording when useful.
- Whether stock, account, current account, or document totals changed.

## Rollback

Application rollback:

```bash
docker compose -f docker-compose.production.yml down
git checkout <last-known-good-tag-or-commit>
docker compose -f docker-compose.production.yml up --build -d postgres web
```

Database rollback only when data or migration changes require it:

```bash
docker compose -f docker-compose.production.yml down
pg_restore --clean --if-exists --no-owner --no-privileges -U postgres -d NoxStock /path/to/verified-backup.dump
docker compose -f docker-compose.production.yml up -d postgres web
```

After rollback:

- Run health checks.
- Run browser smoke test.
- Verify tenant login and one read-only commercial page.
- Preserve sanitized trace IDs and timestamps for incident review.

## Known Technical Debt

- Existing nullable and Mapperly warnings remain.
- Product quantity is still the current stock source; long-term WarehouseStock/StockMovement architecture is documented separately.
- Performance P1.0 proved representative query bottlenecks, but target concurrency is not proven.
- Permission tests include HTTP boundary coverage, but fine-grained per-role authorization still needs expansion.
- Raw performance result artifacts are diagnostic evidence, not beta runtime requirements.
