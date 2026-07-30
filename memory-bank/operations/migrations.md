# Migration Notes

Last updated: 2026-07-24

## Current Migrations

Known migration order:

1. `20260623145819_Initial`
2. `20260623152045_Add_Brand_Category_Product`
3. `20260623195606_Add_BusinessPartner_Document`
4. `20260710113202_Add_CommercialCore_Remove_BookStore`
5. `20260710121438_Add_Prototype_Ledgers_Dashboard`
6. `20260710165212_AddExchangeRates`
7. `20260711090224_Add_Document_Source_Link`
8. `20260711150556_Add_Dynamic_Exchange_Pricing`
9. `20260713123858_Add_Tenant_Activation`
10. `20260715174844_Add_Document_Number_Uniqueness`

Later migrations are retained in source history. The current licensing migration is:

- `20260724154448_Add_SaaS_Licensing`

## Important Notes

- `20260710113202_Add_CommercialCore_Remove_BookStore` drops `AppBooks` and `AppAuthors`.
- `20260710121438_Add_Prototype_Ledgers_Dashboard` creates:
  - `AppAccounts`
  - `AppAccountTransactions`
  - `AppStockMovements`
- `20260711090224_Add_Document_Source_Link` adds optional `SourceDocumentId` plus an index and self-reference on `AppDocuments` for offer/order conversion lineage.
- Historical designer files may still contain Book/Author model snapshots. This is expected and should not be treated as live source.
- `20260713123858_Add_Tenant_Activation` adds `AbpTenants.IsActive` with a safe default of `true`; it does not modify commercial ownership.
- `20260715174844_Add_Document_Number_Uniqueness` adds a unique composite index over `AppDocuments.TenantId`, `Type`, and `DocNumber`.
- `20260724154448_Add_SaaS_Licensing` creates host-owned plan/license/feature/limit/audit tables, a filtered one-current-license-per-tenant index, globally unique license references, and supporting status/audit indexes.
- The licensing migration was applied through Docker DbMigrator on 2026-07-24 for the host database and both existing tenants; the migrator exited with code 0.
- DbMigrator runs the read-only null-tenant ownership audit after migrations and logs counts/sample IDs without assigning data.
- On 2026-07-13 the local Docker PostgreSQL migration history was queried directly and all nine migrations were present.
- `dotnet ef migrations has-pending-model-changes` reported no model/snapshot drift.
- The current DbMigrator container completed a second idempotent run successfully; its runtime image now includes the GSS library required by Npgsql diagnostics.
- Docker Compose was rebuilt and started successfully on 2026-07-13; DbMigrator completed with exit code 0 and Web health status returned HTTP 200.
- Do not delete old applied migration files casually.
- If EF tooling removes or rewrites an older migration unexpectedly, restore the migration history before continuing.
- Use `DbMigrator` to apply migrations.

## Migration Workflow

1. Update Domain entities and `NoxStockDbContext` configuration.
2. Build EntityFrameworkCore and DbMigrator with `-m:1`.
3. Add migration using EF tooling.
4. Review generated `Up`, `Down`, designer, and model snapshot.
5. Build Web and DbMigrator.
6. Run tests.

## Local Docker Verification

- Compose exposes PostgreSQL on host port `5449`; service-to-service connections use `postgres:5432`.
- Use `docker --context default compose run --rm db-migrator` to apply and audit migrations inside the Compose network.
- Host-side appsettings that point to `localhost:5433` do not describe the current Compose port and cannot be used to infer Docker migration status.
