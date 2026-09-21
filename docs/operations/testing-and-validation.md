# Testing Strategy

Last reviewed: 2026-09-10

## Test layers

- `Domain.Tests`: narrow domain invariants.
- `Application.Tests`: shared application test base; currently limited direct
  tests.
- `EntityFrameworkCore.Tests`: primary workflow/integration coverage on SQLite.
- `Web.Tests`: middleware, authorization and presentation/security behavior.
- `BrowserTests`: credentialed Playwright private-beta/regression flows.

## Standard validation

```bash
dotnet restore NoxStock.slnx --disable-parallel
dotnet build NoxStock.slnx -c Release -m:1 --no-restore
dotnet test NoxStock.slnx -c Release -m:1 --no-build --no-restore
```

Run the narrowest affected project/test first. Browser execution requires the
environment variables declared by its test files and a suitable runtime.

## Important limitation

The EF test module uses in-memory SQLite and disables real UoW transactions.
It verifies workflows but not PostgreSQL query plans, filtered indexes, locking,
concurrent writes or rollback. See the PostgreSQL concurrency task before
changing commercial posting or migrations.
