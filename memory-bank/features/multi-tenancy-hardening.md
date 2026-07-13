# Multi-Tenancy Hardening

Last updated: 2026-07-13

## Project Status

- Current program: SaaS tenant isolation hardening
- Current phase: Program complete; production resolver and data-ownership operations remain future work
- Completed phases: Phase 0 through Phase 11
- Blocked phases: none
- Last successful build: `dotnet build NexStock.slnx -m:1 --no-restore -v:minimal` passed with 0 warnings and 0 errors on 2026-07-13
- Last successful tests: EntityFrameworkCore 31/31 and Web 12/12 passed; Domain and Application projects currently contain no discoverable tests
- Latest migration: `20260713123858_Add_Tenant_Activation`, applied and verified in the local Docker PostgreSQL database

## Tenancy Decisions

- Start with shared database and shared schema.
- Commercial entities use ABP `IMultiTenant`, `ICurrentTenant`, and the standard data filter.
- Input DTOs must not accept or trust `TenantId`.
- Development tenant resolution target is `/t/{tenantName}`.
- Production tenant resolution target is `{tenant}.nexstock.com`.
- Commercial permissions are tenant-only; platform and tenant administration permissions are host-only.
- `TenantId` identifies the business, `BranchId` identifies an internal branch, and `WarehouseId` identifies a stock location.
- Dedicated tenant databases are deferred; the current DbMigrator remains compatible with tenant connection strings for a future hybrid model.

## Known Risks

- Existing commercial rows with `TenantId = null` have unknown ownership and must not be assigned automatically.
- Tenant-owned foreign keys still use GUID-only database relationships; application services now reject missing/cross-tenant references, while composite database constraints remain a later hardening option.
- Historical migrations contain Book/Author records; they are migration history and must not be rewritten.
- Production subdomain resolution and trusted proxy/domain configuration are not implemented.
- Subscription/edition and tenant lifecycle decisions are incomplete.
- Branch/Warehouse will require a future inventory model migration, likely toward tenant-owned warehouse stock rows.

## Implementation Log

PHASE: Phase 0 - Preparation and memory bank
STATUS: COMPLETED
DATE: 2026-07-13

IMPLEMENTED:
- Confirmed the nested ABP solution root and ABP 10.4.1/PostgreSQL stack.
- Located and retained the existing `memory-bank` structure.
- Connected the previous tenant analysis to this hardening program.
- Confirmed Book/Author references are limited to historical migration files.
- Preserved the existing dirty worktree, including unrelated `.cursor/rules` deletions.

CHANGED FILES:
- `memory-bank/features/multi-tenancy-hardening.md`
- `memory-bank/activeContext.md`
- `memory-bank/progress.md`
- `memory-bank/operations/migrations.md`

DATABASE/MIGRATION:
- No schema change in Phase 0.

TESTS:
- `dotnet test NexStock.slnx -m:1 --no-build -v:minimal`: 15 EntityFrameworkCore and 1 Web test passed.

BUILD:
- `dotnet build NexStock.slnx -m:1 --no-restore -v:minimal`: passed with one pre-existing Web test entry-point warning.

RISKS:
- Existing null-tenant commercial data remains unresolved.

NEXT PHASE:
- Phase 1 - Two-tenant automated test infrastructure.

PHASE: Phase 1 - Two-tenant automated test infrastructure
STATUS: COMPLETED
DATE: 2026-07-13

IMPLEMENTED:
- Added reusable Tenant A/Tenant B fixtures with real ABP tenant and identity records.
- Covered cross-tenant product read/update, document read, POS stock mutation, reporting visibility, DTO tenant ownership, host commercial creation, and tenant switching.
- Added a fail-closed `RequireTenantId` primitive and applied it to product creation as the first guard case.
- Made the EF integration test baseline tenant-aware while retaining explicit host scopes for host-owned identity assertions.
- Made optional test secret files conditional so a clean checkout builds without a local secrets file.

CHANGED FILES:
- `src/NexStock.Domain.Shared/MultiTenancy/CurrentTenantExtensions.cs`
- `src/NexStock.Application/Products/ProductAppService.cs`
- `test/NexStock.EntityFrameworkCore.Tests/EntityFrameworkCore/MultiTenancy/TwoTenantTestContext.cs`
- `test/NexStock.EntityFrameworkCore.Tests/EntityFrameworkCore/MultiTenancy/TenantIsolationTests.cs`
- `test/NexStock.EntityFrameworkCore.Tests/EntityFrameworkCore/NexStockEntityFrameworkCoreTestBase.cs`
- `test/NexStock.EntityFrameworkCore.Tests/EntityFrameworkCore/Applications/ReportingAppServiceTests.cs`
- `test/NexStock.EntityFrameworkCore.Tests/EntityFrameworkCore/Samples/SampleRepositoryTests.cs`
- `test/NexStock.TestBase/NexStockTestConsts.cs`
- `test/NexStock.TestBase/Security/FakeCurrentPrincipalAccessor.cs`
- `test/NexStock.TestBase/NexStock.TestBase.csproj`
- `test/NexStock.HttpApi.Client.ConsoleTestApp/NexStock.HttpApi.Client.ConsoleTestApp.csproj`

DATABASE/MIGRATION:
- No schema change; no migration generated.

TESTS:
- `dotnet test test/NexStock.EntityFrameworkCore.Tests/NexStock.EntityFrameworkCore.Tests.csproj -m:1`: 22/22 passed.
- Seven focused two-tenant isolation scenarios passed.

BUILD:
- `dotnet build NexStock.slnx -m:1 --no-restore -v:minimal`: passed before final fixture-only adjustments; the solution build is repeated at the next checkpoint.

RISKS:
- The full tenant-required guard is still scheduled for Phase 3; only Product creation is guarded at this checkpoint.

NEXT PHASE:
- Phase 2 - Trusted route tenant resolver.

PHASE: Phase 2 - Trusted route tenant resolver
STATUS: COMPLETED
DATE: 2026-07-13

IMPLEMENTED:
- Removed the email-based resolver and its cross-tenant user scan.
- Restricted the resolver chain to current-user followed by `/t/{tenantName}` path resolution; query, form, header, route-key, and cookie defaults are not accepted.
- Added path-prefix rewriting so Razor routes receive the remaining application path.
- Added an activation middleware path fallback that establishes `ICurrentTenant` for endpoints ABP otherwise treats as host.

DATABASE/MIGRATION:
- No schema change.

TESTS:
- Web tests cover trusted path parsing, malformed path rejection, middleware rewriting, resolver order, and real tenant login routing.

PHASE: Phase 3 - Tenant-required commercial guards
STATUS: COMPLETED
DATE: 2026-07-13

IMPLEMENTED:
- Added fail-closed `NexStock:TenantRequired` guards to catalog, stock, current-account, document, POS, reporting, dashboard, settings, alerts, labels, pricing, statement, and audit application-service entry points.
- Global exchange-rate reads/worker updates and host tenant registration remain outside the commercial guard.

TESTS:
- Host calls to product/document/POS/report/dashboard/settings endpoints are rejected consistently.

PHASE: Phase 4 - Host and tenant permission separation
STATUS: COMPLETED
DATE: 2026-07-13

IMPLEMENTED:
- Marked all NexStock commercial permissions and children as `MultiTenancySides.Tenant`.
- Added host-only `NexStock.ExchangeRates.Refresh`.
- Moved scheduled exchange refresh behind an internal updater so the worker does not impersonate a user permission.

PHASE: Phase 5 - Cross-tenant relation validation
STATUS: COMPLETED
DATE: 2026-07-13

IMPLEMENTED:
- Product create/update validates Brand and Category.
- Category create/update validates ParentId and rejects self-parenting.
- Document create/update validates BusinessPartner and every Product for sales and purchase documents.
- Existing account, payment, POS, workflow, and conversion lookups remain tenant-filtered.

TESTS:
- Tenant A cannot use Tenant B brand, parent category, business partner, or purchase-product IDs.
- Tenant A cannot use Tenant B payment accounts in POS or convert Tenant B documents.

PHASE: Phase 6 - Tenant activation
STATUS: COMPLETED
DATE: 2026-07-13

IMPLEMENTED:
- Extended ABP Tenant with `IsActive`, defaulting existing/new rows to true.
- Added HTTP enforcement after tenant resolution; inactive tenants receive 403 before login/commercial execution.
- Registration and test tenant creation set active explicitly.

DATABASE/MIGRATION:
- Added `20260713123858_Add_Tenant_Activation`; only adds `AbpTenants.IsActive boolean NOT NULL DEFAULT true`.

PHASE: Phase 7 - Global data and tenant settings
STATUS: COMPLETED
DATE: 2026-07-13

IMPLEMENTED:
- ExchangeRate remains global and readable from every tenant.
- Manual refresh is host-only; scheduled refresh uses the internal updater.
- Company settings require a tenant and only write the current tenant setting provider.

TESTS:
- Tenant A and B read the same USD rate but retain separate company settings; host settings writes are rejected.

PHASE: Phase 8 - Registration security
STATUS: COMPLETED
DATE: 2026-07-13

IMPLEMENTED:
- Public tenant registration is closed by default through `App:PublicRegistrationEnabled=false` at both Razor and service boundaries.
- Successful enabled registration redirects to `/t/{tenantName}/Account/Login` and no longer relies on email discovery.
- Duplicate lookup now uses normalized tenant names.

RISKS:
- Before public registration is enabled, rate limiting plus email verification or CAPTCHA must be implemented and reviewed.

PHASE: Phase 9 - Null-tenant dry-run audit
STATUS: COMPLETED
DATE: 2026-07-13

IMPLEMENTED:
- DbMigrator scans ten commercial tables after migration and logs row count plus up to ten sample IDs for null TenantId rows.
- The report explicitly recommends manual ownership mapping and performs no writes.

TESTS:
- Audit reports an unowned product and leaves its TenantId null.

PHASE: Phase 10 - DbMigrator and tenant seed
STATUS: COMPLETED
DATE: 2026-07-13

IMPLEMENTED:
- Verified existing host-schema/host-seed first, then tenant connection schema/tenant-seed order.
- Retained shared-schema behavior and future tenant connection-string compatibility.
- Verified default tenant account/settings seed is idempotent and isolated.

PHASE: Phase 11 - Full verification and closeout
STATUS: COMPLETED
DATE: 2026-07-13

IMPLEMENTED:
- Re-ran the complete build and all discoverable test projects after the tenant hardening changes.
- Added explicit regression coverage for cross-tenant POS payment-account and document-conversion source references.
- Verified the EF model snapshot has no pending model changes.
- Rebuilt the DbMigrator image from current source, applied migrations idempotently, and removed the missing GSS runtime-library warning.

CHANGED FILES:
- `test/NexStock.EntityFrameworkCore.Tests/EntityFrameworkCore/MultiTenancy/TenantIsolationTests.cs`
- `src/NexStock.DbMigrator/Dockerfile`
- `memory-bank/features/multi-tenancy-hardening.md`
- `memory-bank/activeContext.md`
- `memory-bank/progress.md`
- `memory-bank/operations/testing-strategy.md`
- `memory-bank/operations/migrations.md`

DATABASE/MIGRATION:
- `dotnet ef migrations has-pending-model-changes`: no pending model changes.
- Local Docker PostgreSQL reports all nine migrations through `20260713123858_Add_Tenant_Activation`.
- DbMigrator completed successfully on a second run; no data was deleted or reassigned.

TESTS:
- EntityFrameworkCore integration tests: 31/31 passed.
- Web resolver, activation, registration, and smoke tests: 12/12 passed.
- Domain and Application test projects completed successfully but currently contain no discoverable tests.

BUILD:
- Final normal solution build passed with 0 warnings and 0 errors.
- A forced clean rebuild also passed but exposes 153 pre-existing nullable and Mapperly diagnostics; these are tracked as product-hardening debt rather than hidden.
- Current DbMigrator Docker image builds and runs successfully.

RISKS:
- Null-TenantId ownership remains a manual operational decision; the audit is intentionally read-only.
- Composite tenant-aware database foreign keys and uniqueness constraints remain future defense-in-depth work.
- Public registration stays disabled until rate limiting and email verification or CAPTCHA are implemented.
- Production subdomain and trusted-proxy tenant resolution remains deployment work.

NEXT PHASE:
- Resume product hardening, beginning with a live browser workflow pass and inventory warehouse/location foundations.
