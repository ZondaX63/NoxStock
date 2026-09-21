# Technical Context

Last reviewed: 2026-09-10

## Stack

- .NET 10, ASP.NET Core, Razor Pages, ABP 10.4.1
- EF Core with PostgreSQL in production; SQLite is used by the EF test harness
- OpenIddict, ABP Identity/Tenant Management/Permissions/Audit/Settings/Features
- Docker Compose for development, production, release validation, restore and
  isolated performance environments

## Verified commands

```bash
dotnet restore NoxStock.slnx --disable-parallel
dotnet build NoxStock.slnx -c Release -m:1 --no-restore
dotnet test NoxStock.slnx -c Release -m:1 --no-build --no-restore
dotnet ef migrations list --project src/NoxStock.EntityFrameworkCore/NoxStock.EntityFrameworkCore.csproj --startup-project src/NoxStock.DbMigrator/NoxStock.DbMigrator.csproj --no-build
```

Focused unit/domain and SQLite integration commands use the same options with
their project paths:

```bash
dotnet test test/NoxStock.Domain.Tests/NoxStock.Domain.Tests.csproj -c Release -m:1 --no-build --no-restore
dotnet test test/NoxStock.EntityFrameworkCore.Tests/NoxStock.EntityFrameworkCore.Tests.csproj -c Release -m:1 --no-build --no-restore
(cd test/NoxStock.BrowserTests && npm test)
etc/performance/perf.sh baseline
```

For browser tests, use the scripts in `test/NoxStock.BrowserTests/package.json`;
they require runtime E2E credentials. For isolated PostgreSQL performance
validation, use the documented `etc/performance/perf.sh` commands. Do not infer
production credentials or host ports from historical files.

## Infrastructure map

- `docker-compose.yml`: local development.
- `docker-compose.production.yml`: production-oriented deployment.
- `docker-compose.release-validation.yml`: isolated release validation.
- `docker-compose.restore-test.yml`: restore rehearsal.
- `docker-compose.performance.yml`: isolated target-volume profiling.

Use `-m:1` for predictable local builds. VSTest may require local socket access
that sandboxed environments deny.
