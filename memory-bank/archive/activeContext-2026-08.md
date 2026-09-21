> Historical record archived 2026-09-10. Do not use as current architecture, configuration, or verification status; consult AGENTS.md and canonical docs.

# Active Context

Last updated: 2026-08-06

## Current Work Focus

The tenant-scoped **Stock Take / Inventory Audit** workflow is now implemented. It provides draft and approval states, barcode-first cumulative entry, inline quantities, quick incomplete-product creation, Excel/CSV draft import, completion confirmation, transactionally applied stock replacement, zeroing of omitted stock-tracked products, and immutable stock-movement evidence. `Product.Quantity` remains the current stock source of truth; warehouse-ledger expansion is still deferred. The next operational step is applying migration `20260804150340_Add_Inventory_Count_Workflow` through DbMigrator and performing an authenticated browser smoke with assigned `InventoryCounts.Create` / `InventoryCounts.Complete` permissions.

The active target remains **Private Beta Prototype Release**. The frontend-first implementation phases are complete: shared-layout deferral/caching, local pinned assets, Product/POS/document batching, exact and bounded remote lookups, bounded alerts, cached Dashboard aggregates, database-aggregated/paged reports, stable DataTable state, reduced paint work, request/browser timing telemetry, and a repeatable cold/warm navigation script are implemented. The remaining performance release evidence is operational rather than source work: run the prepared cold/warm browser and PostgreSQL capture on the 1 OCPU / 6 GB Oracle VM and compare it with `docs/performance/frontend-budget.md`. Production image build/deployment optimization remains explicitly deferred. ABP Razor Pages, tenant/license/audit enforcement, server-authoritative financial calculations, and component-local loading states remain architectural constraints. The final checklist is in `frontendPerformanceOptimization.md`; Warehouse, reservation, e-Invoice, CRM, advanced accounting, and AI features remain out of scope.

## Recent Changes

- Prepared controlled beta `1.0.0-beta.1`: customer-to-supplier cari virman, auditable partner statement entries, partner modal UX refresh, and customer-facing audit activity summaries are included. This is a limited pilot build, not a general-production scale certification.

- Added the tenant-scoped customer-to-supplier direct-settlement workflow. A customer receivable can now close an equal portion of a same-currency supplier payable without creating a cash/bank transaction. The operation is validated atomically, writes immutable `BusinessPartnerTransfer` evidence with a tenant-unique VIR reference, and is surfaced in both partner statement histories. Migration `20260805211438_Add_Business_Partner_Transfers` is required. Customer and supplier create/edit modals now use the shared sectioned partner-card design. Detailed scope: `features/business-partner-transfers.md`.

- Added the complete Stock Take module under `/InventoryCounts`: focused barcode/search entry, repeated-scan accumulation, inline quantity/note editing, draft persistence, approval/reopen flow, history, summary counts, quick incomplete-product creation, and `.xlsx`/`.csv` import. Stock-untracked service products are rejected both in the browser and application service.
- Completion requires a dedicated permission plus explicit destructive confirmation and runs as one transactional unit of work. Counted products are replaced with physical quantities, omitted tracked products are set to zero, and each actual delta produces an `InventoryCount` stock movement. Completed retries are idempotent.
- Added tenant-filtered inventory-count header/line persistence, a single-active-count database invariant, product incomplete-profile filtering, reset/audit integration, TR/EN localization, migration `20260804150340_Add_Inventory_Count_Workflow`, and focused integration tests. Solution build succeeds with zero errors, the affected EF test group passes 8/8, and EF reports no pending model changes.

- Completed the frontend performance implementation workstream. Dashboard/alerts/reports use bounded database queries and independent paging; Dashboard has a 15-second tenant cache. Authenticated report HTML is not publicly cached. Main DataTables preserve state and debounce search, while high-volume list APIs cap page sizes and deep offsets.
- Self-hosted and pinned Outfit, Material Icons, Alpine.js, JsBarcode, and QRCode; fingerprinted static assets receive immutable cache headers. Broad paint-heavy transitions and blur loading states were reduced, stable table space and reduced-motion behavior were added, and opted-in full-page forms reject duplicate submission without bypassing validation.
- Added `Server-Timing`, correlation headers, opt-in client performance events, private-beta budgets, and `scripts/performance/measure-navigation-http.sh`. Etiket Yazdır now performs a debounced/stale-safe 30-row remote lookup rather than loading/pricing up to 2,000 products. The full EntityFrameworkCore integration suite passes 73/73; Web and EF test builds have zero errors, and all project JavaScript passes Node syntax validation. The full run also closed a foreign-purchase neutral-rate snapshot bug and scoped exchange-rate reuse to the active Unit of Work. Oracle VM cold/warm capture is still required before making production percentile claims.

- Audited the browser/layout/API/query path for frontend responsiveness and connected it to the existing 100,000-product P1.0 evidence. Shared-layout alerts/rates are deferred and briefly cached, duplicate Outfit loading is removed, Product/POS reads are batched, and invoice create/edit no longer embeds 1,000 products plus 1,000 partners. Invoice product/cari selectors, POS customers, and invoice payment accounts now use debounced, stale-response-protected, bounded remote lookup. SKU/barcode/normalized-OEM exact matches run before substring search and have tenant-first indexes. Document create/update loads product references once and reuses per-product pricing calculations instead of fetching details per line. Selected drafts, quick product creation, POS payment semantics, and financial snapshots are preserved. Web build and focused lookup/pricing/tenant tests pass; global skeleton overlays remain prohibited.

- Added the host-owned SaaS licensing model, migration `20260724154448_Add_SaaS_Licensing`, PILOT/CUSTOM plan seed, one-current-license database invariant, feature/limit rows, lifecycle audit, UTC status transitions, and 14/7/1-day expiry audit notices.
- Added host-only license permissions, application-service and unsafe-HTTP write enforcement, product/POS/dynamic-pricing/multi-currency entitlement checks, and stable license error codes. The access cache is tenant-prefix independent; host suspension is fail-closed and the next tenant write check is covered by integration test.
- Added the localized host license management page and tenant topbar status indicator. Existing tenants receive an idempotent PILOT trial; host tenant provisioning assigns the same default trial.
- Remaining license scope is explicit in `features/licensing.md`: `MaxUsers` provisioning enforcement, CUSTOM entitlement editing, and tenant-facing expiry notification delivery are not complete. Docker/PostgreSQL runtime migration and health verification pass; authenticated host/tenant browser verification remains required before deployment.

- Completed the currency-cost UI integration for Products, invoices, and POS without changing the commercial pricing rules. Product creation/edit now exposes explicit Manual vs Dynamic Currency Markup mode, reference currency, markup, an active last-approved-cost snapshot, current TRY sale quote, and a tenant-scoped cost-history table sourced from immutable `ProductCostHistory` records.
- Invoice create/edit now exposes the document currency, server-rate status, sales-price snapshot guidance, automatic quote details, and a clear manual-price state. Purchase documents show a per-line approval-impact preview and explicitly state that drafts do not update product cost; cost changes remain limited to purchase approval.
- POS product cards now identify dynamic pricing and the original last-purchase currency. Cart lines keep their add-time quote, expose an on-demand price-calculation panel, label manual overrides, and provide a deliberate `Refresh prices` action that only refreshes non-overridden lines from the backend. A missing exchange rate now renders an explicit price-unavailable state instead of a zero price.
- Added `ProductPricingAppService.GetCostHistoryAsync` as a read-only tenant-scoped UI projection; no migration, valuation, authorization rule, price-calculation rule, or commercial workflow changed. POS lookup DTOs now include the existing source-cost and pricing-rate context needed to render the backend quote transparently.
- Added initial TR/EN localization keys for the new pricing UI. Verification: `node --check` passes for Products, Invoices, and POS scripts; both localization JSON files parse; `dotnet build NoxStock.slnx -m:1 --no-restore -v:q` passes with 0 warnings/errors; EF integration tests pass 51/51. Browser smoke is still required on a stable Docker/runtime session before treating these visual flows as release evidence.

- Added the NoxStock last-approved-purchase cost profile and dynamic currency-markup sale pricing foundation. `Product.PurchasePrice` remains the manual product-card fallback; the active cost profile is stored separately and is never inferred from the live exchange rate.
- Added immutable `ProductCostHistory` rows for `PurchaseApproved`, `PurchaseCancelled`, and future `ManualAdjustment` use, plus document-owned TRY/USD/EUR `DocumentExchangeRateSnapshot` rows. A purchase invoice approval calculates net unit cost after discount and before VAT from the document snapshot; draft, rejected, and cancelled documents do not become an active cost source.
- Product pricing now has `PricingCurrencyCode` and `PricingMode`. Dynamic pricing uses `last approved cost in pricing currency * current pricing-currency rate * (1 + markup / 100)`, while price snapshots on invoice/POS lines preserve the chosen price, exchange rate, active cost-history ID, pricing mode, and manual-override flag. Existing created sales documents are not recalculated when rates change.
- Cancelling an approved purchase writes a cancellation history row and restores the previous valid approved purchase profile rather than deleting audit data. The product list now shows last cost, historical pricing-currency exchange rate, cost date, supplier, and purchase document number. A missing document snapshot returns the localized `DocumentExchangeRateSnapshotUnavailable` business error instead of silently using a live rate.
- Added migration `20260720143457_Add_Product_Cost_Profile_And_Document_Rate_Snapshots`. Its filtered document-snapshot unique index permits draft-document snapshot replacement without duplicate-key errors while retaining soft-deleted technical records. Existing approved purchase invoices are deliberately not backfilled; profiles begin with newly approved purchases until an explicit audited backfill phase is designed.
- Verification for the pricing feature: solution build succeeded with 0 errors; EF model reports no pending change; EntityFrameworkCore tests pass 51/51, including TRY/USD/EUR net-cost, draft exclusion, approval idempotency, cancellation restore, live-rate repricing, fixed sale snapshots, and POS manual price override. `git diff --check` passed. Local Docker DbMigrator applied the migration for host and `asd`, exited 0, and Web `/health-status` returned Healthy.
- Playwright private-beta smoke was invoked against local Docker with its isolated demo tenants, but the execution host terminated the run before a completed result was written; its prior JSON output is skipped/list-only and is not accepted as browser verification for this feature. Rerun the browser suite in a stable local terminal before treating the UI flow as release evidence.
- Next cost-accounting phase is intentionally deferred: choose and document FIFO or weighted-average valuation before any stock-ledger or margin reporting consumes `ProductCostHistory` as an accounting cost source.

- Added the customer-scoped Sales History module under explicit product scope. `/Customers/{customerId}/SalesHistory` uses a tenant-only `CustomerSalesHistoryAppService` to project approved/partially-paid/paid/completed sale-invoice and POS document lines. It provides period/filter controls, currency-separated summaries, grouped dense movement rows, document links, dashboard-style CSS trend bars, responsive/dark/TR-EN support and customer-detail/list entry points.
- Customer Sales History keeps document-item totals currency-separated, excludes drafts/cancelled/non-sales source documents, hard-caps date ranges to 366 days and page size to 100, and does not introduce a migration. Returns-only currently reports no rows because the commercial model has no return document type; cancellation remains reversal-based and excluded from positive sales history.
- New focused verification passes: five customer-sales-history EF tests cover invoice/POS inclusion, filters, paging, cancellation exclusion, host/tenant isolation and a 110-document bounded-query check (25-row page, <=16 SQL commands). The browser smoke file now covers customer-detail navigation, period filtering and document-detail link discovery; it is syntactically discovered but not live-run without the isolated runtime E2E credentials.
- Private-beta B1 passed in an isolated production-mode TLS/reverse-proxy stack. Production fail-closed validation, trusted forwarded headers, HTTPS redirect, disabled Swagger, non-published PostgreSQL, live/ready health endpoints, and `unless-stopped` restart policies were verified at runtime. Web release-configuration tests pass in the 46-test suite; runtime secret values remain outside Git.
- Private-beta B2 passed with a real encrypted PostgreSQL backup and clean restore rehearsal. The isolated restore ran current migrations for host plus two tenants, verified 2 tenants/10 products/3 invoices/6 stock movements/6 business partners, found zero tenant-relation mismatch, and started Web with `/health/ready` healthy. Evidence is in `docs/release/evidence/restore-rehearsal.txt`; no secret value is recorded.
- Private-beta B3/B4/B5 candidate work is now synchronized. Browser smoke evidence passes for tenant login/logout, product/OEM, POS sale/cancel, invoice approval, collection/payment, dashboard, theme/language switching, tenant A/B isolation and inactive tenant denial. `docs/release/PRIVATE_BETA_RELEASE.md`, README and `privateBetaRelease.md` now document the pilot limits, release gate, env vars without values, backup/restore, smoke command, onboarding, rollback and known limitations.
- Current local gate: build passes with 0 errors; Domain 2/2, EntityFrameworkCore 36/36 and Web 46/46 pass when VSTest is run outside the sandbox. Docker target-environment restart recovery still must be repeated on the real host before opening a pilot.
- Completed P1.0 representative data and query observability without application query changes, indexes, migrations, caches, or cursor contracts. Added the isolated `noxstock-performance` Compose environment, deterministic Small/Medium/Target seed generator, cleanup/idempotency controls, pg_stat_statements/slow-query configuration, health/tenant-integrity capture, and 14 raw EXPLAIN ANALYZE BUFFERS plans.
- Final Target data has two tenants with 100,000 products, 300,000 OEM rows, 50,000 documents, 200,000 document items, 200,000 stock movements, and 100,000 account transactions per tenant. Final idempotency rerun preserved exact counts; all five tenant-parent integrity checks report zero mismatch.
- Baseline evidence prioritizes product/OEM N+1 (52 commands), POS search/rate reads (42-62 commands, 281 ms), deep offset pagination (160 ms with temp spill), exact SKU/barcode sequential scans, unbounded report materialization, and dashboard/document plan cleanup. Detailed plans, top 20 statements, SLO interpretation, resources, and P1.1 order are in `scaleSecurityReliability.md` and `docs/performance`.
- Target data volume is proven on the local 12-CPU/30-GiB host. Target concurrency, p95/p99, pool/lock saturation, and production hardware readiness remain explicitly unproven until P4.
- Completed Scale-Security-Reliability Gate P0.1 without migrations or new commercial modules. Explicit permissions now protect all commercial application services; API failures return 401/403 and tenant path middleware blocks host-to-tenant, cross-tenant, tenant-to-host-admin and inactive-tenant access.
- Kept only `ExchangeRate.GetCurrent` intentionally anonymous because it returns global USD/EUR market data without tenant data. Rate refresh remains host-only.
- Expanded the Web security suite to cover anonymous access to 15 commercial read endpoints plus host/tenant A/tenant B/inactive boundaries and sensitive audit/log sanitization.
- Final verification passes Domain 2/2, EntityFrameworkCore 36/36 and Web 41/41 on isolated rerun; EF reports no pending model change. Solution build has 0 errors and 127 existing nullable/Mapperly warnings, which remain a separate technical-debt stream.
- Disabled PII and complete security-artifact logging by default; sanitized client errors, server exception logs and ABP audit parameters/URLs/exceptions. Sensitive login, registration and tenant-provisioning inputs are excluded from auditing.
- Added fail-closed Production configuration and Compose overrides requiring HTTPS, injected runtime secrets and a mounted OpenIddict certificate. Production health is status-only and Swagger is removed. The separate secret-rotation/rollback runbook is `docs/operations/security-secret-rotation.md`.
- Removed the ABP Studio/MessagePack and Swagger/Microsoft.OpenApi runtime chains and pinned Scriban 7.2.5. Web and DbMigrator vulnerability scans are clean; CI now fails on high/critical advisories.
- Rebuilt local Docker with the standard command after aligning Generic Host environment detection. Existing data was retained; migrator exited 0, Web started at `http://localhost:8080`, health returned 200, anonymous Dashboard/Product returned 401, Swagger returned 404 and public exchange-rate read returned 200.
- Production trusted proxy/TLS behavior is locally verified. Target deployment still requires fresh credential/certificate rotation and target-network verification. P1.1 measured query/index work is paused for the private beta release.

- Completed Scale-Security-Reliability Phase P0. The audit defines target-scale SLOs, 20 performance risks, 20 security/operations risks and the P0.1/P1-P5 implementation order in `scaleSecurityReliability.md`.
- Live P0 verification proved that anonymous requests to tenant-path Dashboard and Product conventional APIs return HTTP 200 with commercial data. This is a production blocker and the first stabilization gate.
- Recorded the current non-scale baseline: 11 MB database, 10 products, 4 documents, PostgreSQL `max_connections=100`, no `pg_stat_statements`/slow-query logging/timeouts, public Swagger, and Docker services without restart/resource limits.
- Dependency advisory scan found high-severity transitive advisories in MessagePack 3.1.1, Microsoft.OpenApi 2.3.0 and Scriban 7.2.1. Compose/appsettings also contain committed development secrets and the current runtime enables complete PII/security-artifact logging by default.
- P0 verification passed build with 0 warnings/errors; Domain 2/2, EntityFrameworkCore 36/36 on rerun and Web 16/16 tests pass. The first full run exposed one intermittent SQLite command-disposal failure; the real PostgreSQL transaction/rollback harness remains mandatory because EF tests disable Unit of Work transactions.
- P0 changed no application code and created no migration.

- Renamed the product to NoxStock across the complete active solution surface: .NET namespaces, assemblies, projects, solution/ABP files, Razor UI, localization resource, permissions, settings, JavaScript proxies, diagnostics keys, Docker images/containers/volumes, database name, documentation, and tests. Historical migration IDs were preserved while their active C# namespaces and context references moved to `NoxStock`.
- Added migrations `20260716204050_Rename_Product_To_NoxStock` and `20260716205742_Complete_NoxStock_Persisted_Rename` to preserve production data during the rename. They migrate persisted ABP settings, permission grants and definitions, feature names, OpenIddict clients/scopes, audit entity and service identifiers to the NoxStock keys and are reversible.
- Migrated the local Docker database and runtime without losing data: the database is `NoxStock`; active containers are `noxstock-postgres`, `noxstock-dbmigrator`, and `noxstock-web`; active persistent volumes are `noxstock-postgres-data` and `noxstock-data-protection`. Previous volumes remain offline as rollback copies.
- Verified the renamed runtime at `http://localhost:8080`: DbMigrator completed host, `asd`, and `asd2`; the login page renders NoxStock; TCMB refresh and health checks succeed. A scan of every PostgreSQL text column found no persisted legacy product-name value; 6 settings, 205 permission grants, and the OpenIddict scope are present under NoxStock.
- Final rename verification passes: build 0 errors; Domain 2/2, EntityFrameworkCore 35/35, and Web 16/16 tests pass using the NoxStock assemblies.
- Completed the commercial stabilization pass before warehouse development. Invoice approval/payment/cancellation, POS sale/cancellation, and offer/order conversion commands now declare transactional units of work. Core document workflows use domain-owned status transition rules.
- Added retry safety for POS creation and invoice payment through operation keys. Tenant-scoped filtered unique indexes prevent duplicate POS documents and duplicate document payment transactions; sequential retries return the existing logical result instead of applying stock, current-account, or account effects twice.
- Added migration `20260716155704_Add_Commercial_Operation_Idempotency`. It adds nullable operation-key columns and filtered unique indexes only; no existing commercial data is rewritten.
- Expanded stabilization tests: tenant A cannot pay tenant B's invoice; repeated invoice approval/cancellation, repeated payment, and repeated POS creation cannot duplicate commercial effects; domain tests cover valid and invalid document transitions. EntityFrameworkCore tests pass 35/35, Domain tests 2/2, and Web tests 16/16.
- Recorded the future Branch/Warehouse/WarehouseStock/StockTransfer/StockReservation design and the staged `Product.Quantity` backfill plan in `inventoryArchitecture.md`. No warehouse models or migrations were implemented in this pass.
- Local Docker validation on `http://localhost:8080` completed: PostgreSQL is healthy, host plus `asd` and `asd2` tenant migrations complete successfully, and host/tenant login endpoints return HTTP 200.

- Removed the public account-registration UI and its provisioning page model. A minimal route guard redirects legacy `/Account/Register` requests to login before ABP's default register page can render. Tenant/company provisioning remains exclusively in host-only Tenant Management. The host login now identifies platform management; tenant-path login identifies the company workspace. Neither login state contains a registration link or a dead password-reset link, and each directs users to the appropriate administrator for account access.
- Reworked the auth visual composition after viewport review: the login shell now fills the viewport instead of centering inside a narrow max-width frame, the brand panel uses a stable 42/58 split with readable heading width, and the form panel uses a controlled 29rem column with compact inputs, actions, and language control. Mobile collapses into a short brand header plus form area.
- Tenant login now shows the resolved company name in a visible context badge above the form, while host platform login remains unchanged. This makes `/t/{tenant}/Account/Login` unambiguous before credentials are entered.
- Moved the tenant workspace label out of the right-side login form. On tenant login it now appears on the left brand panel directly above `TICARI YONETIM PLATFORMU` as plain icon/text, without a surrounding box; the right form no longer contains the duplicate context chips.
- Replaced the tenant context building icon with the Turkish lira symbol `₺` to better match NoxStock's commercial and currency-focused identity.
- Fixed dashboard currency rendering: .NET `ToString("C2")` was producing the generic `¤` symbol under the active culture. Dashboard money and compact trend values now use explicit Turkish lira `₺` output while dynamic USD/EUR client formatters remain currency-aware.
- Fixed ABP language switching: language links now pass both required `culture` and `uiCulture` parameters. Their return URL includes `PathBase`, preserving tenant paths such as `/t/asd/Dashboard` after TR/EN changes instead of falling back to a host route.
- Expanded the localization foundation beyond the language buttons: the shared sidebar, topbar, login brand copy, login placeholder, and tenant dashboard now read from NoxStock `tr`/`en` resources. A Web regression test locks the ABP language-switch contract, including the culture cookie and requested return URL. Live Docker verification confirms `/t/asd/Abp/Languages/Switch?culture=en&uiCulture=en` redirects to `/t/asd/Account/Login` and renders English tenant-login content without a 500 response.
- Moved Audit Logs and Users & Roles into the host-only Platform Administration sidebar group alongside Companies. Audit Logs is now a host-side NoxStock permission and service operation, seeded to the host `admin` role. Tenant requests to `/t/{tenant}/AuditLogs` and `/t/{tenant}/Identity/Users` are blocked by the platform-administration middleware with HTTP 403; Web tests cover both routes and live Docker verification confirms the guard while host `admin` receives HTTP 200 from `/AuditLogs`.
- Reorganized tenant navigation into visible sections: Workspace, Operations, and Customers & Organization. POS, invoices, offers, and orders are grouped under Commercial; inventory/catalog and finance remain separate groups; reports and notifications are direct operational links. This removes the former ambiguity that placed orders under business partners.
- Corrected the desktop shell density and topbar clipping: desktop root rem sizing is now 90% so the UI has the same comfortable density without browser zoom, while the topbar and sidebar brand header reserve a non-shrinking 64px box with overflow protection. Docker web was rebuilt without cache and the live container was verified to serve the new CSS.
- Added the supplied `favicon_io` brand assets to Web `wwwroot`: ICO and 16/32px PNG favicons, Apple touch icon, Android 192/512px icons, and a NoxStock-branded web manifest. Both application and authentication layouts now publish the favicon, touch-icon, manifest, and theme-color metadata. Correct NoxStock Docker verification returns HTTP 200 for the favicon and manifest.
- Replaced the old text-only `N` brand blocks with the supplied NoxStock logo image in the sidebar brand area, the application topbar, and the authentication layout. The logo remains a stable contained image on a dark brand tile and is live-verified from Docker with HTTP 200.

- Completed the first shared frontend quality pass. `global-styles.css` now owns stable light/dark design tokens, compact commercial cards, readable forms, tables, DataTables pagination, modals, report surfaces, and browser error notices. It replaces the previous glass-card treatment, row-spaced tables, and light-only modal/input rules that caused visual drift.
- Reworked the shared application shell: responsive content gutters and a 1600px operational frame replace the undefined `max-w-8xl` wrapper and duplicated padding; theme preference is applied before paint to prevent a light-theme flash. The sidebar and topbar are denser and retain mobile behavior.
- Reworked the NoxStock auth layout into a restrained, responsive commercial sign-in surface with a solid brand panel, accessible language switcher, and dark-mode-compatible inputs. Decorative animated gradients, blurred shapes, remote avatar images, and oversized glass-card composition were removed.
- Standardized responsive action headers on invoice, offer, order, customer, supplier, account, brand, category, personnel, product-movement, and retail-sales pages. Multi-action controls stack on narrow screens. Invoice line-grid presentation now inherits the shared dark-aware border/color system; report/page-specific duplicated form styles were removed.
- Local production-container verification after the frontend pass: `dotnet build NoxStock.slnx -m:1 --no-restore -v:minimal` passed with 0 warnings and 0 errors. `docker compose build --quiet web` succeeded; normal `docker compose up -d` brought PostgreSQL healthy, DbMigrator to successful completion, and Web back to `http://localhost:8080`. `/health-status`, `/Account/Login`, and the new auth-layout assets returned HTTP 200.

- Replaced email tenant discovery with trusted `/t/{tenantName}` resolution and explicit resolver order.
- Added fail-closed tenant guards across all commercial application-service surfaces.
- Split commercial tenant-only permissions from host-only global exchange refresh.
- Added cross-tenant Brand/Category/BusinessPartner/Product relation validation.
- Added ABP Tenant `IsActive` lifecycle enforcement and migration `20260713123858_Add_Tenant_Activation`.
- Kept exchange rates global while proving company settings remain tenant-scoped.
- Closed public tenant registration by default until abuse protections are implemented.
- Added non-mutating null-TenantId DbMigrator audit output.
- Verified host-first/tenant-second migration flow and idempotent tenant defaults.
- Completed the final tenant hardening verification with 31 EntityFrameworkCore and 13 Web tests.
- Applied all nine migrations through tenant activation to the local Docker PostgreSQL database and verified an idempotent DbMigrator rerun.
- Added explicit cross-tenant POS payment-account and document-conversion regression tests.
- Rebuilt and started the Docker stack; PostgreSQL is healthy, DbMigrator exits successfully, Web is available on port 5002, and `/health-status` returns HTTP 200. Host ports 5000, 5001, 5435 and 5438 were occupied locally, so Compose maps Web to 5002 and PostgreSQL to 5439.
- Added the Compose-only `App__HealthUiCheckUrl` override so HealthChecks UI calls `localhost:80` inside the Web container instead of an unspecified bind address.
- Host admin sidebar now shows only the tenant-management entry and logout; tenant-owned commercial navigation remains unavailable until a tenant context is selected.
- The custom application layout renders the shared Razor `scripts` section exactly once; Razor matches existing `Scripts` declarations case-insensitively, restoring the Tenant Management create-modal JavaScript bundle without a duplicate-section exception.
- The Tenant Management create button requires the ABP-standard `#AbpContentToolbar` parent selector. The custom layout now supplies that wrapper; the rendered host-admin HTML and authorized `TenantManagement/Tenants/CreateModal` endpoint were verified after a Docker rebuild.
- The physical NoxStock login page now owns password sign-in and renders model validation errors directly. Tenant login requires `/t/{tenant-name}/Account/Login`; the existing `asd` tenant has `admin` / `asd@asd.asd` as its provisioned administrator identity and retains the password supplied during provisioning. Logout now posts to a physical page and redirects to the matching login route.
- The physical login page now resolves either a username or an e-mail address before calling the user overload of `PasswordSignInAsync`. The old string overload only searched usernames, so valid tenant administrator e-mail logins were incorrectly rejected. Failed lookup/sign-in outcomes are logged with tenant-safe diagnostic fields. Live verification of the `asd` administrator now redirects to `/t/asd/Dashboard` after a successful sign-in.
- Tenant sign-in now issues the authenticated user's ABP `tenantid` claim explicitly. Previously a valid `/t/{tenant}/Account/Login` sign-in could redirect to the tenant dashboard, but the dashboard saw a host user and redirected to tenant administration. Live verification confirms `asd` now receives `302 /t/asd/Dashboard`, then `200` with the NoxStock dashboard.
- Docker Compose persists ASP.NET Core Data Protection keys in the `noxstock-data-protection` volume at `/root/.aspnet/DataProtection-Keys`. This fixes login-form antiforgery HTTP 400 failures after a Web container recreate. A token issued before a forced recreate was verified to sign in successfully afterwards.
- Fixed host/tenant identity-cookie collision: ASP.NET had emitted the host cookie at `/` and the tenant cookie at `/t/{tenantName}`, causing both cookies to be sent and the host `admin` identity to win. The application identity cookie is now always rooted at `/`; login entry clears authenticated and legacy path-scoped cookies through a redirect before rendering a fresh antiforgery form.
- Tenant topbar identity now shows the tenant administrator e-mail and tenant name, while host sessions show the host username and localized platform-management context. This removes the ambiguity caused by both host and old tenant seed users having the username `admin`.
- Tenant Management now uses physical NoxStock pages rather than the ABP tenant-list API, which returned an empty list despite persisted tenant rows. The host page reads `ITenantRepository` directly and refreshes after a successful modal result.
- The NoxStock onboarding modal collects a company login code, administrator username, email and password. It provisions through `ITenantManager` + `ITenantRepository` + `IDataSeeder`, explicitly sets the required `IsActive` extension value before insert, and then renames the seeded `admin` account. This fixes the former silent `The IsActive field is required` failure.
- Unexpected onboarding exceptions are logged and are returned into the modal validation summary; duplicate company codes return a user-facing error. Live Docker verification posted a company, received HTTP 204, and confirmed that it appeared in the company table. The isolated verification tenant was removed afterwards; the existing `asd` tenant was restored from its pre-existing soft-deleted state and is visible again.
- Tenant authentication and host/tenant administration separation are accepted as working by the product owner, and the current NoxStock build has been deployed to a live environment. Treat multi-tenancy as a stabilized foundation; future changes must preserve host-only company management and tenant-only commercial workflows.
- Added the production error-observability baseline: centralized NoxStock exception middleware logs unhandled server errors with trace ID, tenant, user, method, and path; browser-facing failures receive a branded error page or a JSON problem response without technical detail leakage.
- Added authenticated client-side error reporting for browser, promise, and jQuery AJAX failures. Reports are rate-limited per page, written to Serilog with tenant/user context, and paired with an accessible on-screen error notice. Live tenant smoke test returned HTTP 202 and produced a structured log entry.
- Serilog file output now rolls daily, retains 30 files, and caps each file at 10 MB. The current local Docker Web mapping is `http://localhost:8080`.
- Started the invoice-excellence phase with server-authoritative document creation: new documents are always Draft with zero paid balance, line totals/tax/discount totals are recalculated and rounded on the server, dates and partner type are validated, and only Draft documents can be deleted.
- Added sequential type-specific document numbers (`PREFIX-YYYY-000001`), duplicate-number checks, and migration `20260715174844_Add_Document_Number_Uniqueness` for a tenant/type/number unique index.
- Added payment-account/document-currency validation and a printable invoice page available from the invoice list. The new migration was applied idempotently through Docker DbMigrator; focused document workflow/integrity tests currently pass 10/10.
- Replaced the third-party exchange-rate V4 call with TCMB's official `kurlar/today.xml` source. The real refresh failure was an ABP DbContext lifecycle error after Unit of Work completion, not TCMB availability; the refresh now writes within one Unit of Work and returns the validated TCMB values without querying a disposed context. Live Docker verification logged USD/TRY `47.0098` and EUR/TRY `53.5755` from the 2026-07-14 TCMB bulletin.
- Invoice line pricing now displays product-card purchase and sale amounts with their original currencies; POS product cards and cart lines show the same purchase, original sale, and effective sale-price context.
- Invoice creation crash `0HNN2JM3FHH6C:000000B8` was traced to culture-sensitive decimal range metadata during Razor model validation (`0.0001` could not be parsed under Turkish culture). Item-level numeric checks remain server-authoritative in `DocumentAppService`; the fragile DTO range metadata is absent. The invoice create page now uses a responsive, fixed-width line grid: quantity, unit price, discount, VAT, and line total each retain readable space, with controlled horizontal scrolling on narrow screens.
- Follow-up invoice form hardening: `Invoice.Date` and `Invoice.DueDate` now explicitly render as `yyyy-MM-dd` for HTML date controls, preventing `dd.MM.yyyy` model-binding failures under the Turkish UI culture. The invoice item table no longer inherits the generic spaced table treatment; it has a dedicated percentage-based column model, matching header/cell padding, and top-aligned line totals/actions so product price guidance cannot visually shift VAT or discount inputs.
- Date-post fallback: the invoice create handler also normalizes raw form values before validation and accepts `yyyy-MM-dd`, `dd.MM.yyyy`, and `d.M.yyyy`, so localized browser date submissions cannot block document creation. Docker Desktop is the canonical local daemon/context (`desktop-linux`), with NoxStock Web at `http://localhost:8080` and PostgreSQL exposed at `5439`. Do not use `docker --context default` for this project: it is a separate daemon and creates a duplicate stack with port conflicts.
- Client runtime cleanup: ABP v10 needs `Abp/ApplicationLocalizationScript?cultureName={current culture}` in addition to the application configuration script; without it `abp.localization.getResource("NoxStock")` warns because the resource map is empty. The app layout now loads this script before page scripts. Invoice, offer, and order DataTable row-action visibility callbacks accept both ABP callback shapes (`data.record` and the row directly), preventing the `data.record is undefined` crash.

- Removed Book/Author source modules:
  - Domain entities and shared constants/enums
  - Application services and cache items
  - Application.Contracts DTOs/interfaces
  - Web Razor Pages and scripts
  - Book application tests
- Removed `BookStoreDataSeederContributor`.
- Removed ABP getting-started home page and unused getting-started image assets.
- Root `/` now redirects to `/Dashboard`.
- Added real NoxStock permission constants and definitions.
- Connected Products, Brands, Categories, BusinessPartners, and Documents app services to ABP policy names.
- Updated ABP menu contributor to use NoxStock commercial modules.
- Updated localization keys for NoxStock modules and permissions.
- Added migration `20260710113202_Add_CommercialCore_Remove_BookStore` to drop `AppBooks` and `AppAuthors`.
- Restored migration `20260623195606_Add_BusinessPartner_Document` after EF tooling removed it during migration cleanup.
- Updated Web test for root-to-dashboard redirect.
- Added `Account`, `AccountTransaction`, and `StockMovement` domain models.
- Added account, account-transaction, stock-movement, document-workflow, and dashboard DTO/contracts.
- Added `AccountAppService` with explicit create/update handling so tenant/default values are controlled by the service.
- Added `DocumentWorkflowAppService` for sale/purchase invoice approval and cancellation.
- Document approval now:
  - validates status/type/items/payment amount
  - updates stock for stock-tracked products
  - updates customer/supplier balance
  - records cash/bank account movement when a payment account is supplied
  - sets status to `Approved` or `Paid`
- Document cancellation now preserves original records and creates reversal stock/account movements.
- `DocumentAppService` now creates/updates document items explicitly and recalculates totals from lines.
- Dashboard page now reads `IDashboardAppService` instead of static zero values.
- Dashboard query now exposes six-month approved-sale trend data, critical-stock product details, and top-selling products.
- Dashboard only counts approved, partially paid, paid, or completed sales invoices as revenue; draft and sent invoices do not inflate turnover.
- Dashboard Razor UI now renders the sales trend, critical stock list, and top-product list without a CDN chart dependency.
- Added `DocumentWorkflowAppService.RecordPaymentAsync` for invoice-level collections/payments after invoice approval.
- Invoice payment updates `PaidAmount`, invoice status, business partner balance, account balance, and account transaction history in one application-service workflow.
- Invoice list now exposes outstanding amount and a modal action for sales collections and purchase payments.
- Focused workflow test now verifies partial sale collection, closing collection, and reversal of all linked payments on cancellation.
- Dashboard now lists open invoices that are overdue or due within seven days.
- Current-account statement now shows each document's due date and outstanding amount, with overdue open invoices highlighted.
- Added dashboard query coverage proving that paid invoices are excluded from the due-date list.
- Added `BusinessPartnerStatementAppService` to provide a current-account's invoice-linked collection/payment history with document and account names.
- Current-account detail now renders collection, payment, and reversal history under the invoice list.
- Focused workflow test verifies that an invoice's partial and closing collection entries are present in its current-account history.
- Invoice list now supports backend-driven filters for overdue and next-seven-day due invoices.
- Purchase invoice workflow test now covers partial payment, closing payment, and cancellation reversal of both payment movements.
- Full solution verification passes: 7 Entity Framework tests and 1 Web test.
- Added `DocumentType.PosSale` and `PosSaleAppService`, reusing the document, stock movement, and account transaction ledger for POS auditability.
- POS screen now supports barcode/product search, a single-currency cart, cash/bank/customer-credit checkout, recent sales, and cancellation.
- POS server rules derive price from product data, reject insufficient stock, enforce account/customer currency rules, and create a walk-in customer only for cash/bank retail sales without a selected customer.
- POS sales now contribute to dashboard turnover and top-selling-product queries while remaining separate from the invoice list.
- Full solution verification now passes: 9 Entity Framework tests and 1 Web test.
- Added `SourceDocumentId` to commercial documents and migration `20260711090224_Add_Document_Source_Link` to preserve offer/order conversion lineage.
- Added offer and order send/convert workflows: offer to sales order or invoice, sales order to sales invoice, and purchase order to purchase invoice.
- Offer and order Razor pages now support create, send, convert, and draft deletion workflows.
- Full solution verification now passes: 11 Entity Framework tests and 1 Web test.
- Phase 1 UI integrations completed: Account UI, Purchase/Sale invoice UI, and historical movement views (Customer Detail, Product Movements, Account Transactions).
- EF workflow tests prove sale and purchase invoice approval/cancellation update stock, customer balance, account balance, and ledger rows correctly.
- Added tenant-aware company settings through ABP Setting Management for company profile, default currency/unit/VAT, default cash and bank accounts, document/POS prefixes, and label format.
- Replaced the static settings page with a validated save form and active account selectors.
- New product cards and documents inherit configured defaults; conversion and POS flows use configured number prefixes.
- Replaced the static label page with product selection, quantities, Code 128 barcode/QR preview, and print media styling.
- Theme persistence now uses the `noxstock.theme` preference while retaining compatibility with `darkMode`.
- Completed a frontend interaction audit: CRUD, invoice, offer/order, POS, label, and current-account command buttons have matching handlers or real links. Removed misleading stock-entry/exit and personnel placeholder commands, wired current-account printing and notification acknowledgement, and made report cards navigate to their existing operational views.
- Product-list currency selection now converts displayed purchase/sale amounts after exchange rates load; stock-movement listing now uses its explicit table selector and date sort.
- Added a read-only `ReportingAppService` and reporting DTOs; it derives operational report data directly from documents, document lines, stock movements, accounts, account transactions, products, and current accounts without adding a reporting table.
- Replaced the static reports page with a date-filtered operational report covering sales/purchases, collections/payments, cash flow, receivables/payables, stock value/critical stock, stock movements, customer/supplier turnover, VAT, aging, and estimated product profitability.
- Document-based report figures normalize non-TRY documents using their stored document exchange rate. Product profitability states that its cost is an estimate based on the current product-card purchase price.
- Added focused reporting coverage for document totals, VAT, cash flow, current-account balances, aging, critical stock, movement history, and profitability.
- Added live operational alerts for critical/out-of-stock products and overdue or soon-due sale/purchase invoices.
- Replaced the static topbar notification dropdown with live alert counts and links to the notification page.
- Added a notification screen and a filtered audit-log screen backed by ABP's existing `IAuditLogRepository` and audit tables; no new notification or audit schema was introduced.
- Added notification permission, audit-log permission, navigation entries, and a focused critical-stock alert test.

## Verification Status

- `dotnet build NoxStock.slnx -m:1 --no-restore -v:minimal` passes with 0 warnings and 0 errors in the final normal build.
- A forced clean rebuild passes with 153 pre-existing nullable and Mapperly diagnostics; this debt is recorded and has not been hidden with warning suppression.
- EntityFrameworkCore integration tests pass 31/31; Web tests pass 13/13.
- Application and Domain test projects currently contain no discoverable tests.
- EF reports no pending model changes.
- Local Docker PostgreSQL contains all nine migrations and DbMigrator completes idempotently.
- `CompanySettingsAppServiceTests.Should_Save_Operational_Defaults_And_Expose_Default_Pos_Accounts` passes.
- Live tenant onboarding check: a host-admin POST to `/TenantManagement/Tenants/CreateModal` returns HTTP 204 and the newly created tenant is rendered by `/TenantManagement/Tenants` immediately after refresh.
- Final live Docker authentication matrix verified on port `5002`: host `admin` login redirects to `/TenantManagement/Tenants`; the same browser session can then enter tenant `asd` as `asd@asd.asd` and reaches `/t/asd/Dashboard`; the identity jar contains one root-scoped cookie; Dashboard returns HTTP 200; tenant access to host tenant management is denied.
- Tenant sidebar links now use Razor application-relative URLs so the `/t/{tenantName}` path base is preserved for the logo, Dashboard, Products, Accounts, Reports, and Settings. The host sidebar renders only the Companies entry.
- Test output notes that Application, Domain, and TestBase assemblies contain no tests. This is currently informational.
- Local PostgreSQL availability is still required for a live browser walkthrough; automated build and integration tests pass.

## Active Decisions

- The C# ABP project is the final product direction.
- The old React/Node project is reference material only.
- BookStore tutorial source code should not return.
- Existing historical migration designer files may still mention Book/Author because they represent old migration snapshots.
- New schema changes must be done through EF Core migrations.
- Build commands should use `-m:1` in this environment to avoid silent MSBuild project-reference failures.
- Running `dotnet test` may require escalation/outside-sandbox execution because VSTest opens a local socket.
- Commercial history must be reversed, not deleted: cancellations create reversal stock/account rows and mark the source document cancelled.
- Account opening balance is captured on account creation. Later balance edits should be implemented as explicit ledger operations, not silent CRUD changes.
- `DocumentType.SaleInvoice` is the canonical enum name; stock movement uses `StockMovementType.SalesInvoice`.
- Shared database/shared schema is the initial SaaS model; hybrid tenant databases remain a future option.
- Development tenant resolution will use `/t/{tenantName}` and production will use `{tenant}.noxstock.com`.
- Commercial permissions and services are tenant-only; platform and tenant administration are host-only.
- `TenantId`, `BranchId`, and `WarehouseId` represent different ownership scopes and must not be reused for one another.

## Immediate Next Steps

0. Apply the license migration through Docker DbMigrator, verify host/tenant seed counts, and browser-check assign/suspend/read-only/reactivate plus TR/EN/dark mode.
1. Connect `MaxUsers` to host user provisioning and add a small CUSTOM feature/limit editor; route expiry audit notices to tenant notifications.
2. Add tenant-scoped backend/database uniqueness for SKU and non-empty barcode, define the intended OEM uniqueness boundary, and handle concurrent conflicts with stable business errors.
3. Remove direct operational quantity editing from the product form; keep `Product.Quantity` as the documented compatibility balance until the separately approved WarehouseStock backfill phase.
4. Continue invoice/browser regression and private-beta production safeguards without opening broad Warehouse/CRM/e-Invoice scope.

## Pricing Architecture Completed — 2026-08-13

- The pricing authority is now the product's weighted-average TRY inventory cost. `LastPurchaseCost*` remains a separate latest-purchase snapshot and is not reused as the average-cost field.
- Purchase approval records source currency/rate, net TRY unit cost and TRY total cost on document lines and cost history, then updates stock average, last purchase and automatic sale price inside the existing unit of work.
- Automatic mode uses `AverageInventoryCostTry × (1 + ProfitMarkupPercent / 100)`; manual mode preserves the entered TRY sale price and reports actual markup/below-target state.
- Product-card cost entry is a manual fallback/reference only. Editing it never rewrites approved historical inventory cost. Existing stock continues to show its real weighted-average cost separately.
- The former `UseCurrentExchangeRateForSale` flag and product-level sales discounts are compatibility data only and no longer drive the active price calculator. Customer defaults/document-line discounts remain the sales-discount authority.
- POS and sales-invoice approval compare the net KDV-exclusive selling price with the inventory cost snapshot. Continuing below cost requires the corresponding ABP permission and explicit confirmation.
- Product, invoice, POS, import and inventory-count numeric paths normalize user-entered money/quantity/rate values to two decimals. Active Razor/JS displays no longer emit padded four-decimal zero values.
- Added migration `20260813010000_AddWeightedAverageInventoryPricing`; it preserves historical data, backfills averages only from reliable last-purchase TRY values, and does not guess missing costs.
- Focused integration verification passes 31/31 for product pricing, cost history and POS flows. Application and Web builds pass with zero errors; existing nullable/Mapperly warnings remain unrelated technical debt.

## Pricing Deployment Step

- Run the production DbMigrator before the new Web image. After migration, assign `Documents.SellBelowCost` / `Sales.SellBelowCost` only to roles allowed to override the warning, then smoke-test one purchase approval and one manual/automatic sale.

## Pricing UI Production Pass — 2026-08-13

- Product create/edit now presents user-facing “Maliyet ve fiyatlandırma” flows. Edit shows stock, current cost, active sale, unit profit, actual profit rate and the latest purchase calculation before the tabs.
- Automatic mode keeps the sale amount calculated from current cost and target profit rate; manual mode unlocks the sale amount and shows a live below-target warning without blocking the save.
- The product list is reduced to product, stock, sale, cost, profit and status. Latest purchase source details remain available as a cost-cell tooltip instead of occupying permanent columns.
- Purchase invoice rows show document-currency net unit price, rate, TRY unit cost and TRY total; a collapsed “Fiyat etkisini göster” panel previews the post-approval inventory cost and automatic sale impact.
- Sales invoice and POS profitability details use the existing product-edit permission. Cost/profit details remain hidden without permission, while below-cost risk is still communicated in user language.
- Product cost history is exposed as “Fiyat geçmişi”; stock movements enrich purchase-origin rows with source currency, rate, TRY unit cost and TRY total.
- Active pricing inputs and displays use the two-decimal standard; metric cards collapse to two columns on tablet and one column on narrow phones.
- Focused pricing/cost/POS integration tests pass 31/31 and the Web project compiles with zero errors using a clean temporary output directory.
- POS now includes a dedicated quick-customer modal with only name required; a successful creation refreshes customer search and selects the new customer for the current sale.
- POS product cards now show the shelf/location value when present, keeping the warehouse lookup visible without adding a separate product-detail step.
- Inventory summary now uses the same effective cost as product pricing: approved inventory cost first, otherwise the product-card reference cost. The UI explicitly labels the aggregate as an estimated stock value and marks non-approved values as “Referans maliyet”; it no longer counts these products as costless while using them to calculate profit.
- Product Excel export/import now covers reference purchase cost, currency/rate, four purchase discounts, pricing mode, manual sale price, target markup, KDV, sales-discount compatibility fields and stock tracking. Approved/last-purchase costs are exported as read-only information and cannot be overwritten by Excel; a reference cost only seeds an empty opening inventory cost during the first stock migration.
- POS and sales invoices no longer block a stocked product solely because automatic price/cost data is missing. Cashiers may add it and enter a positive manual TRY unit price; stock availability, credit-limit and below-cost controls remain in force whenever cost is known.
