# Migration Notes

Last reviewed: 2026-09-10

Current migration files and the EF model snapshot are the source of truth for
schema history. Historical migration narratives are archived.

## Required workflow

1. Change domain model and DbContext mapping deliberately.
2. Generate a new migration with EntityFrameworkCore and DbMigrator projects.
3. Review `Up`, `Down`, designer and model snapshot.
4. Build and run focused tests.
5. Apply through DbMigrator in an isolated/local environment before release.

```bash
dotnet ef migrations list --project src/NoxStock.EntityFrameworkCore/NoxStock.EntityFrameworkCore.csproj --startup-project src/NoxStock.DbMigrator/NoxStock.DbMigrator.csproj --no-build
```

Do not delete or rewrite applied migrations. PostgreSQL-specific index,
transaction and concurrency behavior needs PostgreSQL validation.
