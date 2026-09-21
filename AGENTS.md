# NoxStock Agent Guide

## Purpose and stack

NoxStock is a production-oriented multi-tenant SaaS for stock, purchasing, sales/POS, documents,
current accounts, accounts, currency-aware pricing and host-managed licensing.
It is a .NET 10 / ASP.NET Core ABP 10.4 layered monolith using Razor Pages,
EF Core and PostgreSQL. The solution is `NoxStock.slnx`.

## Source of truth and context loading

Trust sources in this order: code; tests; schema and migrations; current
configuration/infrastructure; canonical architecture docs; ADRs; active task
specifications; Memory Bank; archive. Start here, then the nearest
path-specific `AGENTS.md`, an applicable task, and relevant architecture/ADRs.
Do not load the full Memory Bank by default.

Search before reading broadly or modifying. Make the smallest coherent diff;
do not reformat or alter unrelated files. Update canonical documentation when a
change alters an invariant or an accepted decision. Dated verification counts,
benchmarks and implementation logs belong in task/release evidence, not here.

## Layer boundaries

- `Domain.Shared`: contracts shared across layers: enums, permissions,
  localization, constants.
- `Domain`: entities, domain services, seeders and settings; no Web dependency.
- `Application.Contracts`: DTOs and application-service interfaces.
- `Application`: authorized use cases and transactional workflow orchestration.
- `EntityFrameworkCore`: PostgreSQL mappings, repositories and migrations.
- `HttpApi`: conventional HTTP API module over application contracts.
- `HttpApi.Client`: client integration module.
- `Web`: Razor UI, middleware, authentication and presentation only.
- `DbMigrator`: migration and seed host.

Do not put business posting rules in Razor/JS, bypass application services from
the UI, or introduce a reverse dependency between layers.

## Tenancy, host and authorization

Operational data is tenant-scoped through `IMultiTenant` and ABP data filters.
Require a tenant before commercial work. Never disable tenant filtering or use
`IgnoreQueryFilters` except at an explicitly reviewed host/audit boundary.

Host-owned data includes ABP platform administration, exchange rates and SaaS
licensing. Tenant users must not access host pages/APIs; host sessions must not
operate against a tenant workspace unless the code explicitly changes tenant
context. Preserve the `/t/{tenant}` path plus authenticated tenant-claim model
and middleware ordering in `NoxStockWebModule`.

Every commercial application-service operation needs explicit authorization.
Razor page protection and hidden UI controls are not security boundaries.

## License, stock and money safety

Tenant mutations need the existing `TenantLicenseWrite` boundary; gated
features/limits must also be checked where the operation is performed. Do not
rely solely on middleware or UI for licensing.

Treat approval, payment, POS, transfer, inventory count and cancellation as
commercial posting operations. Preserve atomicity, idempotency where supported,
tenant ownership checks, document status transitions, historical rate/price/cost
snapshots and reversal-based correction. Do not silently recalculate historical
documents using current exchange rates. Do not directly patch materialized
product, account or partner balances outside their workflow.

## Persistence and performance

Schema changes require an EF migration. Review generated migration `Up`,
`Down`, designer and model snapshot; do not rewrite applied migrations. Tenant
data indexes and uniqueness must include tenant scope when appropriate. Measure
high-volume queries against PostgreSQL before adding indexes or claiming a
performance result. Avoid unbounded materialization and per-row queries.

## Verification and secrets

Use deterministic serial commands when practical:

```bash
dotnet restore NoxStock.slnx --disable-parallel
dotnet build NoxStock.slnx -c Release -m:1 --no-restore
dotnet test NoxStock.slnx -c Release -m:1 --no-build --no-restore
```

The EF test project uses SQLite and disables real UoW transactions; it is not
proof of PostgreSQL concurrency or rollback. Browser tests require supplied E2E
credentials; see `test/NoxStock.BrowserTests/package.json` and release docs.

Use the proportionate risk workflow in
`docs/architecture/development-workflow.md`. Tenant isolation, authorization,
licensing, financial/stock posting, concurrency, migrations and production
infrastructure are high-risk changes and require targeted inspection beyond a
normal build/test run.

Never commit, echo or document real passwords, connection strings, certificate
passphrases, tokens or production URLs with embedded secrets. Use runtime
configuration/secret stores. Do not weaken production validation for local use.

## Documentation map

- Architecture: `docs/architecture/`
- Decisions: `docs/decisions/`
- Focused active work: `docs/tasks/active/`
- Operations and validation: `docs/operations/`, `docs/performance/`, `docs/release/`
- Current-state index and historical archive: `memory-bank/`
