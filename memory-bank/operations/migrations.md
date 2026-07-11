# Migration Notes

Last updated: 2026-07-10

## Current Migrations

Known migration order:

1. `20260623145819_Initial`
2. `20260623152045_Add_Brand_Category_Product`
3. `20260623195606_Add_BusinessPartner_Document`
4. `20260710113202_Add_CommercialCore_Remove_BookStore`
5. `20260710121438_Add_Prototype_Ledgers_Dashboard`
6. `20260711090224_Add_Document_Source_Link`

## Important Notes

- `20260710113202_Add_CommercialCore_Remove_BookStore` drops `AppBooks` and `AppAuthors`.
- `20260710121438_Add_Prototype_Ledgers_Dashboard` creates:
  - `AppAccounts`
  - `AppAccountTransactions`
  - `AppStockMovements`
- `20260711090224_Add_Document_Source_Link` adds optional `SourceDocumentId` plus an index and self-reference on `AppDocuments` for offer/order conversion lineage.
- Historical designer files may still contain Book/Author model snapshots. This is expected and should not be treated as live source.
- Do not delete old applied migration files casually.
- If EF tooling removes or rewrites an older migration unexpectedly, restore the migration history before continuing.
- Use `DbMigrator` to apply migrations.

## Migration Workflow

1. Update Domain entities and `NexStockDbContext` configuration.
2. Build EntityFrameworkCore and DbMigrator with `-m:1`.
3. Add migration using EF tooling.
4. Review generated `Up`, `Down`, designer, and model snapshot.
5. Build Web and DbMigrator.
6. Run tests.

## Known Environment Issue

`dotnet ef migrations list` may show:

```text
An error occurred using the connection to database 'NexStock' on server 'tcp://localhost:5433'.
Permission denied
```

This means EF could not query the database state in the current environment. It can still list migrations known from the assembly.
