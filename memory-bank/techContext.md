# Technical Context

Last updated: 2026-07-10

## Stack

- ASP.NET Core / Razor Pages
- ABP Framework 10.4.1
- .NET 10.0
- Entity Framework Core with PostgreSQL provider
- PostgreSQL
- Mapperly via `Volo.Abp.Mapperly`
- ABP Identity, Permission Management, Audit Logging, Feature Management, Setting Management, Tenant Management, OpenIddict
- Frontend assets managed through ABP web libs and `abp install-libs`

## Solution Location

Main solution root:

```text
.
```

Legacy reference implementation:

```text
../A
```

## Common Commands

Build Web:

```bash
dotnet build src/NoxStock.Web/NoxStock.Web.csproj --no-restore -v:minimal -m:1
```

Build DbMigrator:

```bash
dotnet build src/NoxStock.DbMigrator/NoxStock.DbMigrator.csproj --no-restore -v:minimal -m:1
```

Run tests:

```bash
dotnet test NoxStock.slnx --no-build -m:1
```

List migrations:

```bash
dotnet ef migrations list --project src/NoxStock.EntityFrameworkCore/NoxStock.EntityFrameworkCore.csproj --startup-project src/NoxStock.DbMigrator/NoxStock.DbMigrator.csproj --no-build
```

Add migration:

```bash
dotnet ef migrations add Migration_Name --project src/NoxStock.EntityFrameworkCore/NoxStock.EntityFrameworkCore.csproj --startup-project src/NoxStock.DbMigrator/NoxStock.DbMigrator.csproj
```

## Environment Notes

- In this workspace, plain parallel MSBuild can fail silently with `Build FAILED. 0 Error(s)`. Use `-m:1`.
- VSTest needs local socket access. In sandboxed execution it can fail with `System.Net.Sockets.SocketException (13): Permission denied`.
- EF migration list may show database access permission errors if PostgreSQL on `localhost:5433` is not accessible, but it can still list known migrations.
- Local Web and DbMigrator settings expect PostgreSQL at `localhost:5433` with database `NoxStock`, user `postgres`, password `1121`.
- On 2026-07-10, `pg_isready -h localhost -p 5433` returned `no response`; web server was not started because the app needs the database for a usable prototype.
- No local `docker-compose.yml` was found. Existing container files are `src/NoxStock.Web/Dockerfile` and `src/NoxStock.DbMigrator/Dockerfile`.
- Network access is restricted in the coding environment.

## Technical Constraints

- Do not use runtime SQL table creation endpoints.
- Do not introduce raw SQL for ordinary persistence when EF repositories/migrations can handle it.
- Do not reintroduce BookStore tutorial modules.
- Preserve ABP conventions for modules, permissions, localization, and application services.
- Keep tenant-aware entities implementing `IMultiTenant` where business data is company-scoped.
- Prefer focused tests around business operations instead of broad brittle UI tests.
