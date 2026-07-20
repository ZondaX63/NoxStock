# Scale, Security and Reliability Workstream

Last updated: 2026-07-17

## Status

- Phase P0 - Measurement baseline and architecture audit: **Complete**
- Gate P0.1 - Release-blocking security stabilization: **Implementation complete**
- Phase P1.0 - Representative data and query observability: **Complete**
- Phase P1.1 - Measured query and index optimization: Not started
- Phase P2 - Concurrency, idempotency and transaction hardening: Not started
- Phase P3 - Application security: Not started
- Phase P4 - Load and resilience tests: Not started
- Phase P5 - Production operations: Not started

P0 intentionally did not add migrations or perform broad refactors. Gate P0.1 closed the confirmed application-level release blockers without adding a migration or a new commercial module. Production deployment remains blocked until target-environment secret/certificate rotation and trusted reverse-proxy/TLS verification are completed from `docs/operations/security-secret-rotation.md`. P1 must still be measured on representative PostgreSQL data before any scalability claim.

## P1.0 Closure Record - Representative Data and Query Observability

Completed on 2026-07-17 without application query refactors, cache/cursor work, indexes, or migrations.

### Isolated environment and data volume

- Separate Compose project: `noxstock-performance`; database: `NoxStockPerformance`; loopback-only PostgreSQL/Web ports: `5440`/`8081`; dedicated Compose PostgreSQL and data-protection volumes. No development or production connection string is consumed.
- Deterministic profiles are `small`, `medium`, and `target`. The final measured profile is `target`, anchored at `2026-07-01`, with two fixed performance tenants.
- Per tenant: 100,000 products, 300,000 OEM rows, 10,000 partners, 50,000 documents, 200,000 document items, 200,000 stock movements, 100,000 account transactions, and 30 accounts. The two-tenant measured commercial set is 1,920,000 rows plus lookup rows.
- Search data includes exact and substring SKU/barcode/OEM/name patterns, 500 shared `34116775277` alternatives per tenant, zero/critical/normal stock, TRY/USD/EUR prices, due/overdue states, and 730 days of document and ledger history.
- Current database size after repeated seed/baseline runs: 1,247 MB. Final Target seed took 70 seconds; the current generator's second idempotency run took 72 seconds and preserved exact tenant counts. Small seed also passed repeat and clean-to-zero checks.
- Tenant integrity checks for OEM-product, document-partner, document-item/product, stock-movement/product, and account-transaction/account relations all report zero TenantId mismatch.

### Hardware and Compose budget

- Measured host: 12 logical CPUs, 30 GiB RAM, 30 GiB swap, NVMe filesystem with 471 GiB free during capture.
- The P1.0 Compose file intentionally has no container CPU/RAM caps; idle post-capture usage was approximately 320 MiB PostgreSQL and 301 MiB Web. This makes the baseline a local-host diagnostic, not a production-sized hardware guarantee.
- Web maximum pool size defaults to 40; PostgreSQL `max_connections=100`. Capture observed one active and four idle performance-database connections and zero deadlocks.
- Performance-only configurable defaults: slow-query log 250 ms, statement timeout 60 s, lock timeout 5 s, idle-in-transaction timeout 30 s. `pg_stat_statements.track=all` and `track_io_timing=on`. These values were not copied to production.
- Web/DB health returned HTTP 200 and `Healthy`. Operational commands and artifacts are documented in `docs/performance/README.md`.
- Final verification: solution build passed with 0 warnings/0 errors; Domain 2/2, EntityFrameworkCore 36/36, and Web 41/41 tests passed; EF reported no pending model changes; Compose configuration, JSON plan parsing, shell syntax, and `git diff --check` passed. The performance runtime ended with PostgreSQL healthy, Web running, DbMigrator exited 0, `pg_stat_statements` 1.11 visible with 30 captured statements, and 100,000 products in each tenant.

### Required query baseline

| Query | Commands | SQL ms | Root/scan summary | Rows | Shared hit/read | Tenant filter |
|---|---:|---:|---|---:|---:|---|
| Product first page | 52 | 32.107 | Limit; Seq Scan | 50 | 2727/20640 | Yes |
| Product deep page | 52 | 160.006 | Limit; Seq Scan; temp spill | 50 | 2621/20544 | Yes |
| SKU exact | 3 | 19.234 | Gather; Seq Scan | 1 | 2653/20512 | Yes |
| Barcode exact | 3 | 19.239 | Gather; Seq Scan | 1 | 2749/20416 | Yes |
| OEM exact | 52 | 1.710 | Sort; Index Scan | 500 | 2171/0 | Yes |
| Product text search | 52 | 22.217 | Limit; Seq Scan | 50 | 2933/20320 | Yes |
| POS product search | 42-62 | 280.940 | Limit; Seq Scan | 20 | 3334/49397 | Yes |
| Document list | 3 | 28.666 | Limit; Seq Scan | 50 | 5572/0 | Yes |
| Account history | 2 | 3.598 | Bitmap Heap/Index Scan | 100 | 3343/0 | Yes |
| Stock history | 3 | 0.256 | Bitmap Heap/Index Scan | 100 | 203/0 | Yes |
| Dashboard summary | 13 | 40.305 | Result; mixed Bitmap/Seq Scan | 1 | 10661/20192 | Yes |
| Critical stock | 52 | 31.144 | Limit; Seq Scan | 50 | 3157/20096 | Yes |
| Due/overdue documents | 3 | 18.248 | Limit; Index/Seq Scan | 100 | 3706/0 | Yes |
| Sales/purchase report snapshot | 8 + foreign product count | 92.453 | Result; CTE/Index/Seq Scan | 1 | 17978/47174 | Yes |

The entity/DTO envelope is captured in `docs/performance/p1.0-baseline.md`. Product list, OEM lookup, text/critical lists materialize 50 DTOs but issue 52 commands. POS materializes 20 product DTOs plus OEM/rate rows. The report path still loads all 100,000 products, 10,000 partners and 30 accounts before period entities; this is a structural failure even though its direct SQL envelope is under two seconds.

### pg_stat_statements top 20

| Rank | Statement | Total/mean ms | Plan summary |
|---:|---|---:|---|
| 1 | POS product search | 268.336 | Limit over sequential product/OEM search |
| 2 | Deep product page | 159.305 | Limit over sequential scan; temp sort spill |
| 3 | Report snapshot envelope | 92.129 | Result with materialized CTE, index and sequential scans |
| 4 | Dashboard summary | 40.150 | Result with bitmap and sequential aggregate scans |
| 5 | Product first page | 31.996 | Limit over sequential product scan |
| 6 | Critical stock | 31.075 | Limit over sequential product scan |
| 7 | Document list | 28.350 | Limit over sequential document scan |
| 8 | Product text search | 22.195 | Limit over sequential product scan |
| 9 | Barcode exact | 19.215 | Gather over sequential scan |
| 10 | SKU exact | 19.213 | Gather over sequential scan |
| 11 | Due/overdue documents | 18.105 | Limit with index and sequential scans |
| 12 | Account transaction history | 3.545 | Bitmap heap/index scan |
| 13 | OEM exact | 1.684 | Index scan plus sort |
| 14 | Index-usage diagnostic | 1.393 | PostgreSQL catalog observer query |
| 15 | Database-stat diagnostic | 0.692 | PostgreSQL catalog observer query |
| 16 | PostgreSQL-setting diagnostic | 0.562 | PostgreSQL catalog observer query |
| 17 | Stock movement history | 0.226 | Bitmap heap/index scan |
| 18 | Table-I/O diagnostic | 0.214 | PostgreSQL catalog observer query |
| 19 | Connection diagnostic | 0.116 | PostgreSQL catalog observer query |
| 20 | pg_stat_statements reset | 0.102 | Observability control statement |

Raw query IDs, calls, rows, block and temp usage are in `docs/performance/results/top-queries.csv`; all 14 required raw JSON plans are retained beside it.

### SLO and P1.1 decision

- Direct single-user SQL times are only preliminary signals; they are not endpoint p95/p99 measurements. Product first/exact/text, dashboard, and report SQL are below their preliminary latency budgets in this run. POS search is 281 ms, but the SLO is defined for checkout and cannot be declared pass from this query-only result.
- The standard-list `<=3 SQL commands` SLO fails for product/OEM/text/critical paths (52) and POS search (42-62). Dashboard remains 13 commands. Report bounded-materialization and constant-rate-query criteria fail by code-path envelope.
- Tenant seed isolation passes at rest, but authenticated tenant A/B response/cache/export isolation under concurrency is not part of P1.0 and remains for P4.
- P1.1 priorities proven by measurement are: remove product/OEM N+1; batch POS rate/OEM reads; route exact SKU/barcode/OEM before substring search; then test tenant-first exact/list indexes; replace deep offset navigation only after bounded query cleanup; bound report entity loading and push aggregates to SQL; re-measure dashboard/document/history plans before choosing composites.
- No index, cache, cursor contract, broad report refactor, query refactor, or migration was implemented in P1.0.
- Target **data volume** is proven locally. Target **100-300 user throughput, p95/p99 latency, memory behavior, pool saturation, lock behavior, and production hardware capacity are not proven** and must not be claimed before P4 load/resilience testing.

## Target Scale

- 100,000 products per tenant
- Hundreds of thousands of OEM codes, documents, document items, stock movements and account transactions per tenant
- 100-300 concurrent active users
- PostgreSQL with shared database/shared schema multi-tenancy

## Measured Baseline

Measured against the local Docker PostgreSQL and Web runtime on 2026-07-17:

- Database size: 11 MB
- Commercial row counts: 10 products, 5 OEM codes, 6 business partners, 4 documents, 8 document items, 6 stock movements and 4 account transactions
- PostgreSQL `max_connections`: 100; observed connections after startup: 12
- PostgreSQL extensions: only `plpgsql`; `pg_stat_statements` is not enabled
- `log_min_duration_statement = -1`; `statement_timeout = 0`; `lock_timeout = 0`; `idle_in_transaction_session_timeout = 0`
- Web health endpoint: HTTP 200
- Anonymous `/t/asd/api/app/dashboard/summary`: HTTP 200 and returned tenant financial/commercial data
- Anonymous `/t/asd/api/app/product?SkipCount=0&MaxResultCount=1`: HTTP 200 and returned product/pricing/stock data
- Public Swagger UI and schema: HTTP 200 in the current runtime
- Response headers include `X-Content-Type-Options` and `X-Frame-Options`; CSP and `Referrer-Policy` are absent
- Docker Web and PostgreSQL containers have no restart policy or resource limit; Web has no container health check
- Build: succeeded with 0 warnings and 0 errors
- Tests: Domain 2/2, EntityFrameworkCore 36/36 on isolated rerun, Web 16/16. The first full run had one intermittent SQLite command-disposal failure; the same test and complete EF project then passed. Application and TestBase assemblies contain no discovered tests.
- Dependency advisory scan: transitive `MessagePack 3.1.1`, `Microsoft.OpenApi 2.3.0` and `Scriban 7.2.1` have known high-severity advisories; MessagePack also has moderate advisories.
- Audit sample: no sensitive-key text was found in current `AbpAuditLogActions.Parameters`; 4 audit rows contain exception payloads. This is a small sample, not proof of masking safety.

This baseline validates wiring and exposes defects, but it does **not** represent the target data volume or concurrency. NoxStock is not yet demonstrated to be scalable for the stated target.

## Preliminary Performance SLOs

These are P1/P4 acceptance targets to define measurement before optimization. They must be revalidated against documented production-like hardware.

- Product first page and exact barcode/SKU/OEM lookup: p95 <= 250 ms, p99 <= 500 ms at 30 virtual users and 100,000 products in one tenant
- General product text search: p95 <= 500 ms at 30 virtual users
- POS checkout command: p95 <= 750 ms, error rate < 0.5%, no duplicate commercial effects
- Dashboard: p95 <= 750 ms at 30 virtual users
- Interactive report snapshot: p95 <= 2 seconds for a bounded period; larger work must become an asynchronous job
- Standard list endpoint: <= 3 SQL commands and <= 100 rows per response unless an explicitly bounded lookup contract says otherwise
- Tenant isolation: zero cross-tenant rows in responses, logs, caches and generated exports

## Top 20 Performance Risks

| ID | Severity | Finding and affected files | Recommended action | Measurable success criterion |
|---|---|---|---|---|
| P01 | Critical | Report snapshot loads every product, partner and account into memory. `src/NoxStock.Application/Reports/ReportingAppService.cs` | Split reports into bounded DTO projections and database aggregations; paginate detail sections. | At 100k products, no unbounded entity query; report memory delta < 150 MB and p95 meets report SLO. |
| P02 | Critical | Report loads all documents, transactions and movements in the date range, then performs joins/grouping in process and builds a large document-ID `IN` query. `ReportingAppService.cs` | Push grouping, sums and joins into SQL; enforce date/range limits; move large reports to background jobs. | SQL returns aggregate/detail page only; no request materializes more than configured page limit. |
| P03 | Critical | Report calls the price calculator once per product; each foreign-currency calculation performs repository reads. `ReportingAppService.cs`, `Products/ProductPriceCalculator.cs` | Load the two global rates once per request or use a bounded global rate cache. | Exchange-rate SQL commands remain <= 1 per report regardless of product count. |
| P04 | High | Product list performs one OEM query per returned product. `Products/ProductAppService.cs` | Batch-load OEM rows by page IDs or project an aggregate. | Product list uses <= 3 SQL commands for page sizes 10-100. |
| P05 | High | POS product pricing performs repeated rate queries per product; create loads products once to price and again to mutate stock. `Sales/PosSaleAppService.cs`, `ProductPriceCalculator.cs` | Batch product/rate reads and retain tracked product instances for the command. | Search/checkout SQL count grows by O(1), not by cart/product count. |
| P06 | High | Document create/update loops lines and queries each product and rate separately; update also deletes items row by row. `Documents/DocumentAppService.cs` | Batch-load products/rates, validate with dictionaries and use controlled set-based item replacement. | 100-line invoice create stays <= 8 SQL commands before inserts and meets POS/document SLO. |
| P07 | Critical | Number generator loads all matching documents for a type/year and calculates max in memory; concurrent callers can select the same number. `Documents/DocumentNumberGenerator.cs` | Introduce a tenant/type/year sequence row or PostgreSQL sequence strategy with atomic increment. | One indexed/atomic operation; 0 duplicate numbers under 100 concurrent requests. |
| P08 | High | POS checkout options return all active accounts and all customers. `Sales/PosSaleAppService.cs` | Keep small account lookup bounded; replace customer list with paged server search. | Initial checkout payload has <= 100 rows and p95 <= 250 ms at 100k customers. |
| P09 | High | Business-partner payment history is unbounded. `BusinessPartners/BusinessPartnerStatementAppService.cs` | Add cursor/paged input, date filter and hard page cap. | No response exceeds 100 history rows; deep navigation remains stable. |
| P10 | High | Label lookup returns 2,000 products and many Razor modal lookups request 1,000 rows. `PrintLabels/LabelProductLookupAppService.cs`, `src/NoxStock.Web/Pages/**` | Convert to search-as-you-type paged lookups; cap settings/account lists to realistic bounds. | Lookup payload <= 100 rows and <= 250 KB. |
| P11 | High | Due-document operational alerts have no result cap and materialize every overdue/near-due document. `Notifications/OperationalAlertsAppService.cs` | Return counts plus a bounded top list; provide paged detail endpoint. | Summary returns <= 100 alerts and one bounded count/query plan. |
| P12 | Medium | Host tenant page loads every tenant and sorts in memory. `Pages/TenantManagement/Tenants/Index.cshtml.cs` | Add repository-side ordering, filter and pagination. | <= 100 tenants per page and constant response memory. |
| P13 | High | Products, documents, movements, account transactions and audit logs use offset pagination plus total count; deep pages become progressively expensive. Related app services and DTOs | Keep offset for shallow UI pages initially; add keyset/cursor contracts for high-volume history. | Cursor page p95 changes by < 20% between first and 10,000th logical page. |
| P14 | Critical | Name/SKU/barcode/document/cari searches use `Contains` and `ToLower().Contains`; the OEM btree index cannot serve substring search. Product, POS, Document and BusinessPartner app services | Add normalized exact/prefix columns; measure `pg_trgm` GIN/GiST for contains search; route exact barcode/SKU/OEM first. | `EXPLAIN ANALYZE` uses intended index; no sequential scan for exact lookups at target volume. |
| P15 | Critical | Products, partners, accounts, brands and categories lack tenant-first indexes for their main list/search/sort paths. `NoxStockDbContext.cs` | Add only query-backed `(TenantId, ...)` composite indexes after representative plans are captured. | Target list plans avoid full cross-tenant scans; index usage visible in `pg_stat_user_indexes`. |
| P16 | Critical | Documents lack tenant/date/status/partner composites used by dashboard, due alerts, lists and reports. `NoxStockDbContext.cs`, dashboard/report/document services | Design separate composites for recent/type/status/date and partner/date access paths. | Dashboard/document plans use bounded index scans and meet SLO at 500k documents. |
| P17 | High | Stock movements and account transactions only have FK indexes, not tenant/entity/date composites. `NoxStockDbContext.cs`, movement/account services | Add `(TenantId, ProductId, MovementDate, Id)` and `(TenantId, AccountId, TransactionDate, Id)` candidates after plan testing. | History query p95 <= 250 ms at 500k rows and uses index scan. |
| P18 | High | Dashboard executes many independent live aggregates on transactional tables for every view and has no tenant-scoped summary model/cache. `Dashboard/DashboardAppService.cs` | First optimize SQL/indexes; then evaluate short tenant-scoped summary caching or an incremental read model. | Dashboard SQL time and command count meet SLO under 30 VUs; cache/read model never mixes tenants. |
| P19 | Medium | Application query methods do not accept/propagate `CancellationToken`; disconnected or timed-out requests continue consuming DB work. Application services broadly | Add token-aware contracts in high-cost paths and pass tokens to EF/HTTP operations. | Cancelled request stops DB/application work within 1 second in resilience test. |
| P20 | Critical | Pool and timeout budgets are unspecified while PostgreSQL has only 100 connections; slow-query observability and statement/lock timeouts are disabled. Connection strings, Compose, PostgreSQL operations | Define per-instance pool budget, command/statement/lock timeouts and enable `pg_stat_statements` in the performance environment. | At 300 VUs, no pool exhaustion; DB connections stay within budget and top SQL is measurable. |

## Top 20 Security and Operations Risks

| ID | Severity | Finding and affected files | Recommended action | Measurable success criterion |
|---|---|---|---|---|
| S01 | Critical | Anonymous tenant-path requests return dashboard financial data and product pricing/stock data. `Dashboard/DashboardAppService.cs`, conventional API configuration in `NoxStockWebModule.cs` | Require an authenticated tenant user and dashboard permission; audit every conventional service for explicit policy/default authorization. | Anonymous and host-user tenant API tests return 401/403; authenticated tenant A cannot read tenant B. |
| S02 | Critical | No HTTP negative-authorization regression suite caught S01; current Web tests focus on paths/layout. `test/NoxStock.Web.Tests`, application service tests | Add generated-endpoint authorization matrix tests for anonymous, host, tenant A and tenant B. | Every commercial endpoint is covered; CI fails on any unexpected 2xx. |
| S03 | Critical | Database password, connection strings, OpenIddict certificate passphrase and string-encryption passphrase are committed. `docker-compose.yml`, Web and DbMigrator `appsettings.json` | Rotate exposed values; move production secrets to environment/Docker secrets or secret manager; keep only development placeholders. | Secret scan has 0 real credentials; rotated credentials are verified in deployment. |
| S04 | Critical | PII and complete security artifact logging are enabled whenever `App:DisablePII` is absent, including current config. `NoxStockWebModule.cs`, `appsettings.json` | Default to PII disabled; permit explicit local-only opt-in. | Production logs contain no tokens, claims, passwords or complete security artifacts in automated probes. |
| S05 | Critical | Compose runs `Development`, plain HTTP and disabled HTTPS metadata against a public IP. `docker-compose.yml` | Split development/production Compose; production must use Production environment, trusted reverse proxy and HTTPS authority. | Production smoke test rejects HTTP externally and OpenIddict metadata requires HTTPS. |
| S06 | High | No `UseHttpsRedirection`; antiforgery secure policy is `SameAsRequest`, and identity cookie security is not explicitly production-enforced. `NoxStockWebModule.cs` | Enforce HTTPS redirect and `Secure=Always` in production; verify HttpOnly/SameSite for identity and antiforgery cookies. | Cookie/header integration tests pass behind the real proxy; no auth cookie is sent over HTTP. |
| S07 | High | When HTTPS metadata is disabled, forwarded-header configuration clears trusted proxy/network lists and trusts `X-Forwarded-Proto`; production proxy handling is otherwise incomplete. `NoxStockWebModule.cs` | Configure explicit trusted proxy/network values and forward limits from environment. | Spoofed forwarded headers from untrusted clients do not alter scheme/host; proxy integration test passes. |
| S08 | High | CSP, `Referrer-Policy` and `Permissions-Policy` are absent. `NoxStockWebModule.cs`, shared layouts/assets | Add a measured CSP and modern headers; remove inline script/style dependencies or use nonces/hashes. | Header scan passes policy; Playwright has zero CSP violations in core flows. |
| S09 | Critical | No server rate limiting exists for login, public tenant resolution/registration route, API, diagnostics or expensive searches. Web module and endpoints | Add partitioned ASP.NET Core rate limits by IP, user and tenant; preserve lockout behavior. | Brute-force/load tests receive 429 within policy and legitimate tenant traffic stays within SLO. |
| S10 | Medium | Swagger UI/schema and detailed health response are public in all environments. `NoxStockWebModule.cs`, health-check extensions | Disable Swagger in production or authorize it; expose separate minimal liveness/readiness endpoints. | Production anonymous request gets 404/401 for Swagger; public health reveals only status. |
| S11 | High | Dependency scan found high-severity advisories in transitive MessagePack, Microsoft.OpenApi and Scriban. `NoxStock.Web.csproj` dependency graph | Upgrade the owning ABP/packages to patched compatible versions and add advisory scanning to CI. | `dotnet package list --vulnerable --include-transitive` reports no high/critical advisory. |
| S12 | High | Audit logs can retain action parameters and full exception payloads; masking policy is not explicit. ABP auditing configuration and `AuditLogAppService.cs` | Define sensitive field redaction, exception sanitization, access policy and retention/export policy. | Automated password/token probes produce 0 secret matches in audit/log stores. |
| S13 | Medium | Authenticated clients can submit arbitrary URL/message/stack fields to error logs; only client-side rate limiting and length trimming exist. `ClientErrorsController.cs`, browser reporter | Add server-side rate limit, newline/control normalization and PII scrubbing. | Abuse test is throttled; forged multiline fields cannot create misleading structured log entries. |
| S14 | High | PostgreSQL binds host port 5439 on all interfaces with a weak shared password. `docker-compose.yml` | Do not publish DB in production; use an internal network, least-privilege application user and rotated secret. | External port scan cannot reach PostgreSQL; app/migrator use separate least-privilege roles. |
| S15 | High | Web and database containers run without an explicit non-root runtime user. Dockerfiles/Compose | Run application as non-root, use read-only filesystem where practical and restrict capabilities. | Container inspection shows non-zero UID and reduced capabilities; smoke tests still pass. |
| S16 | High | Docker build executes an external NodeSource setup script, installs unpinned ABP CLI/Yarn, and uses non-frozen dependency installation. `src/NoxStock.Web/Dockerfile` | Pin base images by digest/tool versions, use lockfile-enforced installs and produce an SBOM. | Repeated clean builds resolve identical dependency versions and pass image scan. |
| S17 | High | Production OpenIddict requires `openiddict.pfx`, but Compose does not provision it and currently avoids the path by running Development. `NoxStockWebModule.cs`, Dockerfile/Compose | Provision signing/encryption keys from a secret store with rotation and startup validation. | Production container starts with mounted secret, signs tokens, and rotation rehearsal succeeds. |
| S18 | High | No Web health check, restart policy, CPU/RAM/PID limits or DB/client timeout policy exists. `docker-compose.yml`, connection/runtime config | Add production health probes, restart policy, resource budgets and bounded timeouts. | Process/DB restart tests recover automatically without request hang or unbounded resource growth. |
| S19 | Critical | No automated backup, encrypted retention policy or restore rehearsal exists in the repository/operations docs. | Add scheduled PostgreSQL backups, off-host encrypted storage and a real restore runbook/test. | A backup restores to a clean database and passes migration, tenant counts and commercial invariants. |
| S20 | High | No CI workflow, centralized metrics/log shipping, slow-query capture or alert routing exists. Repository root, Serilog/PostgreSQL operations | Add CI gates plus production telemetry and alerts for error rate, latency, DB saturation, disk and backup age. | A synthetic failure opens an alert with trace/tenant-safe context; CI blocks failed build/test/migration/image/security gates. |

## Gate P0.1 Closure Record

Completed on 2026-07-17:

- Commercial conventional APIs now require their NoxStock permission. Dashboard, products/pricing, brands, categories, business partners/statements, accounts/transactions, documents/workflows, POS, reports, notifications, settings, labels and stock movements are protected at class or operation level.
- `ExchangeRate.GetCurrent` is the only intentional anonymous application-service read. It returns global USD/EUR market data and no tenant record. Refresh remains host-only through `ExchangeRates.Refresh`.
- Tenant path activation rejects host-to-tenant and cross-tenant authenticated requests with 403; inactive tenants remain 403. Host administration API/page prefixes reject tenant users.
- API authentication failures are normalized to real 401/403 responses without changing Razor login redirects.
- The Web authorization matrix covers anonymous access to 15 commercial read endpoints plus host, tenant A, tenant B and inactive-tenant boundaries. Web tests pass 41/41 on isolated rerun.
- PII/security-artifact logging is disabled by default. Client diagnostics, exception logs and ABP audit payloads sanitize credentials, tokens, JWTs, connection strings, URL queries, control characters and raw exception details.
- Base configuration contains no operational secret. Development values are explicitly local-only placeholders. Production Compose requires runtime-injected database/admin/encryption/certificate values, an HTTPS public URL and a mounted OpenIddict certificate, and fails closed when they are absent or unsafe.
- Swagger and its transitive OpenAPI chain were removed. ABP Studio runtime and its transitive MessagePack chain were removed. Scriban is pinned to 7.2.5. Web and DbMigrator transitive vulnerability scans report no advisories.
- Closed high advisories: MessagePack `GHSA-hv8m-jj95-wg3x`, `GHSA-382j-8mxh-c7x2`, `GHSA-vh6j-jc39-fggf`; Microsoft.OpenApi `GHSA-v5pm-xwqc-g5wc`; Scriban `GHSA-7jvp-hj45-2f2m`.
- CI now restores, builds, tests and fails on high/critical package advisories.
- Solution build succeeds with 0 errors and 127 pre-existing nullable/Mapperly warnings; Gate P0.1 introduced no remaining new warning. Domain tests pass 2/2, EntityFrameworkCore 36/36 and Web 41/41. EF reports no pending model change.
- Docker development startup succeeds with the single standard command. Migrator exits 0; Web starts on port 8080; runtime probes return health 200, anonymous Dashboard 401, anonymous Product 401, Swagger 404 and public exchange-rate read 200.
- No migration or production database change was made in Gate P0.1.

Risk register disposition after P0.1:

- Closed in code/test: S01, S02, S04, S10, S11 and the payload-sanitization part of S12.
- Repository/config part closed, target-environment operation still required: S03 and S05.
- Partially closed: S06 secure production cookies; real proxy integration remains. S12 retention/export policy remains. S20 has a source/package CI gate but production telemetry remains.
- Deferred by explicit scope to later security/operations phases: S07-S09 and S13-S19.

## Prioritized Implementation Plan

### Gate P0.1 - Release-blocking security stabilization - Complete in code

1. Close S01 with explicit application-service authorization and add S02 endpoint matrix tests.
2. Rotate/remove committed secrets, disable PII logging by default and create a production configuration profile.
3. Resolve high-severity package advisories or document an upstream compatibility block with compensating controls.
4. Rebuild Docker and prove anonymous endpoints return 401/403 before another production deployment.

### Phase P1 - Query and database work

1. Add representative deterministic seed generation: 100k products, 300k OEM codes, 500k documents/items/movements per tenant.
2. Enable `pg_stat_statements`, bounded slow-query logging and capture baseline `EXPLAIN (ANALYZE, BUFFERS)` plans.
3. Remove the report/product/POS/document N+1 and unbounded lookup paths before adding indexes.
4. Add only measured tenant-first indexes, with reversible migrations and production concurrent-index deployment notes.
5. Introduce bounded/cursor contracts and sort whitelists; rerun query-count and latency tests.

### Phase P2 - Concurrency and transaction work

1. Add real PostgreSQL tests; current SQLite test module disables Unit of Work transactions and cannot prove rollback.
2. Protect document/POS approve/cancel/payment/transfer/registration with optimistic concurrency or database locking as appropriate.
3. Extend idempotency to all money/stock commands and translate unique/concurrency failures into stable business outcomes.
4. Inject failures after each ledger mutation and prove complete rollback.

### Phase P3 - Security hardening

1. Complete S03-S17 with production proxy/TLS/cookie/header/rate-limit/secret/audit controls.
2. Run an IDOR matrix for every conventional controller and host/tenant permission boundary.
3. Add dependency, secret and container scans.

### Phase P4 - Load and resilience tests

1. Select k6 or NBomber after the authenticated API harness is stable; record the choice and rationale.
2. Run the ten requested scenarios with predeclared SLOs, tenant A/B traffic and PostgreSQL telemetry.
3. Do not declare target-scale readiness until p50/p95/p99, throughput, error rate, CPU/RAM, connections, locks and top SQL are recorded.

### Phase P5 - Production operations

1. Production Compose/reverse proxy/TLS/secrets/resource/restart configuration.
2. Backup and real restore rehearsal.
3. CI build/test/EF migration/image/smoke/rollback/security gates.
4. Central logs, metrics, dashboards and routed alerts.

## P0 Changed Files and Migrations

- Added this decision/audit record.
- Updated `memory-bank/activeContext.md` and `memory-bank/progress.md`.
- No application code changed in P0.
- No migration was created or modified in P0.

## P0 and P0.1 Exit Decision

P0.1 closes anonymous commercial-data exposure, unsafe default PII/audit behavior, active committed configuration secrets, public Swagger and the identified vulnerable dependency chains. The code-level stabilization gate is complete. The production deployment gate remains **blocked** until real secrets/certificates are rotated and injected, HTTPS reverse-proxy behavior is verified, and the operational checklist is signed off. NoxStock is still not declared ready for 100,000 products or 100-300 concurrent users; P1 query/index work, PostgreSQL transaction tests, backup/restore and later operations controls remain mandatory.
