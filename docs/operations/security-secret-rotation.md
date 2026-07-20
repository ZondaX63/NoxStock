# Production Secret Rotation Runbook

This runbook contains secret types and procedures only. Never record secret values in Git, logs, tickets, screenshots or the Memory Bank.

## Release Gate

Production deployment stays blocked until every item below is completed and verified in the target environment:

- A trusted HTTPS reverse proxy is configured for the public NoxStock URL.
- Production secrets are injected at runtime; no development placeholder is accepted.
- The OpenIddict certificate is mounted read-only at `/app/openiddict.pfx`.
- Database backup and rollback access are verified before rotation.
- Old credentials, certificates, tokens and sessions are invalidated after verification.

## Secret Inventory

- PostgreSQL administrator credential used by migration operations.
- Least-privilege application database credential, when the dedicated role is introduced.
- Host administrator credential and any tenant administrator credentials created from an old default.
- OpenIddict signing/encryption certificate and its passphrase.
- ABP string-encryption passphrase.
- External integration/API credentials, if configured outside the repository.
- ASP.NET Data Protection keys when a compromise is suspected.

## Rotation Order

1. Take an encrypted off-host PostgreSQL backup and verify that it can be read.
2. Create or rotate database credentials, inject them into the secret source, and verify migrator plus Web connectivity.
3. Rotate host and affected tenant administrator credentials; invalidate active sessions.
4. Generate and securely store a new OpenIddict certificate and passphrase; mount it read-only.
5. Rotate the string-encryption passphrase using a data migration plan if encrypted values already exist.
6. Rotate external integration credentials and revoke the previous versions.
7. Redeploy with `docker compose -f docker-compose.production.yml up --build -d` through the approved release process.
8. Revoke old database credentials and retire old certificates only after the smoke checks pass.

## Required Runtime Variables

- `NOXSTOCK_DB_PASSWORD`
- `NOXSTOCK_ADMIN_PASSWORD`
- `NOXSTOCK_PUBLIC_URL` using `https://`
- `NOXSTOCK_CERTIFICATE_PASSPHRASE`
- `NOXSTOCK_STRING_ENCRYPTION_PASSPHRASE`
- `NOXSTOCK_OPENIDDICT_PFX_PATH`

Use a platform secret manager or Docker secrets integration. Do not place production values in `.env` files committed to the repository.

## Verification

- Production configuration fails to start when a required secret, HTTPS URL or certificate is missing.
- The migrator completes once and exits with code 0.
- `/health-status` returns only the aggregate status in Production.
- `/swagger` and `/swagger/index.html` return 404.
- Anonymous commercial APIs return 401; forbidden tenant/host boundary requests return 403.
- Tenant A cannot read or mutate tenant B data.
- Logs and audit records contain no password, bearer token, JWT, connection string or raw exception payload.
- Web login, token issuance, logout, invoice and POS smoke checks pass through the public HTTPS address.

## Rollback

1. Stop the failed Web release while keeping PostgreSQL isolated.
2. Restore the prior application image and compatible certificate/secret versions from the secret manager.
3. Restore the verified database backup only when a migration or data change requires it.
4. Repeat health, authentication and tenant-isolation checks before reopening traffic.
5. Preserve sanitized trace IDs and deployment metadata for incident review; never preserve raw credentials.
