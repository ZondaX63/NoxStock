> Historical record archived 2026-09-10. Do not use as current architecture, configuration, or verification status; consult AGENTS.md and canonical docs.

# Progress

## 2026-08-13 - Weighted-Average Inventory Pricing

- Added separate weighted-average TRY inventory cost and last-purchase snapshots; purchase approval now updates stock, average cost, latest purchase and automatic sale price atomically.
- Purchase lines and cost history preserve source currency/rate, source net unit cost, TRY unit/total cost, quantity and before/after average-cost context.
- Unified automatic/manual behavior in the shared backend calculator and shared Product Card modal pipeline. Manual prices remain fixed; automatic prices follow cost plus target markup.
- Product list/card, purchase preview, reporting and POS/sales snapshots now use the average inventory cost. Below-cost sales require explicit confirmation plus ABP permission.
- Removed active reliance on the legacy live-FX flag and product-level default sales discounts without dropping compatibility columns or altering historical beta documents.
- Normalized touched money, rate and quantity entry/display paths to at most two decimals; foreign card rates omitted by API are resolved instead of silently defaulting to 1 TL.
- Added backward-compatible migration `20260813010000_AddWeightedAverageInventoryPricing`. Reliable existing last-purchase TRY values are backfilled; unknown costs remain unknown.
- Verification: focused ProductPricing/ProductCostProfile/POS integration tests pass 31/31; Application and Web builds complete with zero errors; `git diff --check` passes.

## 2026-08-04 - Stok Sayım Modülü

- Yeni `/InventoryCounts` akışı barkod/SKU araması, aynı barkodu kümulatif artırma, satır içi miktar/not düzenleme, taslak kaydetme, onaya gönderme, düzenlemeye açma, geçmiş ve canlı özetleri içeriyor.
- Sayımda bulunup sistemde olmayan ürünler minimum alanlı uygulama içi modal ile eksik profilli ürün olarak açılabiliyor; Ürünler ekranındaki filtreyle daha sonra tamamlanabiliyor. Stok takibi kapalı hizmet ürünleri istemci ve sunucu tarafında sayıma alınmıyor.
- `.xlsx`/`.csv` sayım aktarımı `SKU`, `Barkod`, `Miktar` kolonlarını destekliyor; tekrarlayan satır miktarları toplanıyor ve aktarım doğrudan stoğu değil yalnızca taslağı değiştiriyor.
- Tamamlama ayrı yetki ve açık onay gerektiriyor. Tek transaction içinde listedeki miktarlar fiziki sayımla değiştiriliyor, listede olmayan stok takipli ürünler sıfırlanıyor ve her gerçek değişiklik için `InventoryCount` stok hareketi yazılıyor. Tamamlanmış komutun tekrarı yeni hareket üretmiyor.
- Migration `20260804150340_Add_Inventory_Count_Workflow` iki tenant-scoped sayım tablosunu, tek aktif sayım invariantını ve `Product.IsIncompleteProfile` alanını ekliyor. Operasyonel veri sıfırlama ve null-tenant audit kapsamı güncellendi.
- Doğrulama: çözüm derlemesi 0 hata; etkilenen EF test grubu 8/8; localization JSON ve `git diff --check` geçerli; EF modelinde bekleyen değişiklik yok.

## 2026-07-31 - Frontend Performance Workstream Completion

- Completed source implementation for phases P0-P5 while keeping the production image-build/deployment phase deferred as requested. The only unchecked P0 items are real Oracle VM cold/warm evidence, because those values cannot be truthfully produced from the development workstation.
- Added dynamic `Server-Timing` and correlation headers, opt-in Web Vitals/resource/long-task capture, a cold/warm authenticated navigation script, and explicit 1 OCPU / 6 GB budgets.
- Pinned and self-hosted the runtime frontend assets, enabled immutable caching only for fingerprinted static files, preserved private caching for authenticated/ABP bootstrap responses, removed broad paint-heavy effects, and retained component-local loading instead of a global skeleton.
- Bounded operational alerts and paged notifications; consolidated/cached Dashboard reads; replaced report over-materialization with database aggregates and independent detail paging. Historical report profit now uses the locked sale-line cost snapshot and KDV-excluded net sale.
- Main DataTables retain session state and debounce search. Product, POS, invoice, account-payment, customer, and label lookups use exact-first/bounded remote paths with stale-response protection where applicable. Etiket Yazdır no longer prices 2,000 products during initial HTML rendering.
- Verification completed: Web build and EF test-project build succeed with zero errors; the complete EntityFrameworkCore integration suite passes 73/73; production npm dependency audit reports zero vulnerabilities; all project JavaScript passes Node 22 syntax validation; `git diff --check` passes. The full suite found and closed two currency-edge regressions: neutral foreign-purchase input no longer replaces the dated rate snapshot, and rate reuse is limited to one Unit of Work so later requests observe changed live rates.

## 2026-07-31 - Exact Product and Payment Account Lookup

- Added an exact-first SKU, barcode, and normalized OEM path to catalogue, invoice, and POS searches. Shared exact OEM codes still return all matching brand alternatives; broad substring search remains the fallback.
- Added reversible non-unique tenant-first SKU/barcode indexes through migration `20260731160000_Optimize_Product_Exact_Lookups`. Existing duplicate data and future uniqueness policy are unaffected.
- Removed the final 1,000-account Razor bootstrap from the invoice payment modal. Active accounts now load in 20-row, document-currency-matched remote results with 250 ms debounce, stale-response sequencing, bounded caching, and validation-state preservation.
- Verification: focused lookup tests pass 5/5, complete ProductPricing/POS classes pass 16/16, and Web build succeeds with 0 errors. Target PostgreSQL plan recapture and authenticated browser smoke remain pending.

## 2026-07-31 - Frontend Performance Invoice Lookup Batch

- Removed the 1,000-product and 1,000-business-partner bootstrap queries/options from invoice create/edit. New pages now render only placeholders; draft editing and validation failures render only selected values.
- Added a lightweight server-side product lookup projection and bounded Razor Page lookup handlers. Product and partner searches return at most 20 rows to the browser, use a 250 ms debounce, ignore stale responses, and keep a small page-lifetime query cache.
- Preserved exact barcode/SKU/name and tax-number/name Enter selection, backend price quote loading, four purchase/sales discounts, current-rate behavior, selected draft values, and the in-context quick-product creation panel.
- Invoice HTML and new-row template size are now independent of total product and customer counts. No migration or financial calculation rule changed.
- Verification: Web builds with 0 errors using `-m:1`; the focused EF lookup test passes 1/1 with a six-command regression ceiling; `git diff --check` passes. Executable JavaScript syntax validation remains pending because this workstation has no JS runtime.

## 2026-07-31 - Frontend Performance POS Customer Lookup

- Removed the unbounded customer array from POS checkout bootstrap; initial checkout loading now fetches only active account choices and saved defaults.
- Added tenant/license-protected, TRY-only POS customer search by name, tax number, or phone. The client uses 250 ms debounce, stale-response sequencing, 20-row results, a bounded page cache, exact Enter selection, and selected-option preservation.
- Preserved cash/bank walk-in sales and the required selected customer for current-account sales. No schema, price, VAT, discount, stock, or payment rule changed.
- Verification: Web build succeeds with 0 errors; focused POS and host/tenant-boundary tests pass 7/7.

## 2026-07-31 - Frontend Performance Document Save Batching

- Replaced document create/update product lookup per line with one tenant-filtered batch and reused the same server pricing calculation when a product appears on multiple lines.
- Removed the Razor Page loop that fetched full product detail for each zero-price submission. Equivalent manual-sale, dynamic-sale, and purchase fallback pricing now lives in the authoritative document application service.
- Preserved manual override, four-stage discounts, VAT, dynamic rate, cost-history, and approval snapshot semantics; no migration was required.
- Verification: Product pricing and document-integrity regression tests pass 11/11.

## 2026-07-31 - Frontend Performance First Batch

- Added `frontendPerformanceOptimization.md` with browser-first performance rules, measured priorities, phased TODOs, acceptance targets, and an explicit deferral of production image-build work.
- Removed live operational-alert queries from the shared Razor layout. The notification shell now renders immediately, loads its authorized summary during browser idle time, and caches it for 30 seconds by tenant path and user without caching tenant HTML.
- Added a 15-minute browser cache for the global USD/EUR ticker, retained hourly refresh, localized its status messages, and removed the duplicate Outfit Google Fonts import. The remaining external font/icon/Alpine assets are recorded for a later self-hosted pass.
- Replaced Product list row-by-row OEM, cost-history, purchase-document, supplier, and repeated-rate reads with one joined page projection, one OEM batch, and request-local currency reuse. POS lookup now batch-loads cost histories and shares the request-local currency cache.
- Added a 12-row Product list SQL regression test. Focused Product/POS EF tests pass 7/7; the page uses 11 intercepted commands including ABP permission/UOW reads instead of scaling with row count. Web builds successfully with existing warnings. JSON parsing and `git diff --check` pass.
- Web authorization test execution is blocked on this workstation by the missing `Microsoft.AspNetCore.App 10.0.0` runtime. JavaScript syntax execution is also pending because Node.js is not installed locally.

## 2026-07-24 - SaaS Licensing Foundation

- Added host-owned `LicensePlan`, `TenantLicense`, feature, limit, and audit entities with migration `20260724154448_Add_SaaS_Licensing`; PILOT/CUSTOM plans and existing-tenant PILOT trials seed idempotently.
- Added host-only ABP permissions and explicit host-context/route checks. Tenant users cannot call host license services; the host UI supports assignment, plan change, extension, PastDue read-only, suspension, reactivation, and audit history.
- Added backend enforcement at unsafe HTTP and application-service mutation boundaries. POS/dynamic pricing/multi-currency features and the PILOT 10,000-product limit are checked server-side with stable error codes.
- Added UTC lifecycle processing, terminal cancellation rules, audit rows, 14/7/1-day expiry audit notices, tenant-prefix-independent cache invalidation, and immediate fail-closed suspension.
- Added TR/EN license UI resources, dark-mode-compatible host management, and a read-only tenant topbar plan/status indicator.
- Verification complete for the source/runtime gate: solution build succeeds; Domain 8/8, EntityFrameworkCore 58/58, and Web 48/48 pass. EF reports no pending model changes and `git diff --check` is clean.
- Docker DbMigrator applied the licensing migration for host, `asd`, and `asd2`, then exited 0. PostgreSQL contains PILOT/CUSTOM plans and two Trial tenant licenses; Web `/health-status` reports process and database checks Healthy at `http://localhost:8080`.
- Authenticated host/tenant browser verification of the new management UI remains required before production deployment.
- Deliberately pending: `MaxUsers` provisioning enforcement, CUSTOM entitlement editing, tenant-facing expiry notification delivery, product SKU/barcode uniqueness hardening, and the separately staged `Product.Quantity`/WarehouseStock transition.

## 2026-07-20 - Customer Sales History

- Added the tenant-scoped `ICustomerSalesHistoryAppService` and `/Customers/{customerId}/SalesHistory` Razor Page. The module joins sales documents, document items, products, brands and categories through projections; no schema or migration change was needed.
- Added period presets/custom bounded date ranges, document/product/SKU/barcode/brand/category/currency filters, deterministic newest-first offset paging, currency-safe summary cards, customer credit snapshot, grouped sales ledger and document print/detail links.
- Added entry points from Customer Detail and Customer list row actions. The page has TR/EN localization keys, responsive horizontal movement-table scrolling and dark-mode styles.
- Added five focused EF integration tests, including tenant A/B and host rejection plus a 110-document query-bound test (25 rows, <=16 SQL commands). The Playwright private-beta suite now covers customer detail to sales history, period change and invoice link discovery.
- Verified: build 0 errors; Domain 2/2, EntityFrameworkCore 41/41 and Web 46/46 tests pass; EF has no pending model changes; localization JSON parses; `git diff --check` is clean. Playwright runtime execution remains pending the isolated Docker test environment and runtime-only E2E credentials.

## 2026-07-18 - Private Beta B3-B5 Review Cleanup

- Reconciled the partially completed private-beta release work from the follow-up agent.
- Confirmed existing evidence for the critical browser smoke run: tenant provisioning, tenant login/logout, product/OEM search, POS sale/cancel, invoice approval, collection/payment, dashboard, TR/EN/theme, tenant A/B isolation and inactive tenant denial all passed.
- Confirmed current local .NET validation: build passes with 0 errors; Domain 2/2, EntityFrameworkCore 36/36 and Web 46/46 pass when VSTest is run outside the sandbox.
- Rewrote `docs/release/PRIVATE_BETA_RELEASE.md` for NoxStock with runtime env vars without values, deployment, health, backup/restore, browser smoke, release gate, onboarding, feedback, rollback and known-limitations sections.
- Added the README private-beta scope and runbook reference.
- Updated `privateBetaRelease.md` so B3, B4 and B5 no longer appear as not started.
- Target-host secret rotation, target backup/restore rehearsal and target Compose restart recovery remain required before opening real pilot users.

## 2026-07-17 - Private Beta B1 Release Configuration Complete

- Added fail-closed Production environment enforcement for Web and DbMigrator, trusted-proxy forwarded-header configuration, HTTPS redirection, and separate live/ready health endpoints.
- Added a production TLS proxy validation overlay and runtime-only secret/certificate workflow. PostgreSQL has no published host port; PostgreSQL, Web, and proxy use `unless-stopped`.
- Runtime verification passed in `noxstock-release-validation`: Production environment, DbMigrator exit 0, Web/database readiness healthy, HTTPS proxy healthy, direct HTTP redirected, Swagger unavailable, and health output status-only.
- Expanded Web release configuration coverage; the complete Web suite passes 46/46.
- No secret value was committed. All credentials and certificates used for validation were generated outside the repository.

## 2026-07-17 - Private Beta B2 Backup/Restore Complete

- Added encrypted, checksummed, retained PostgreSQL backup automation using runtime-only passphrase files.
- Restored the real encrypted dump into a clean isolated PostgreSQL volume, ran current host/tenant migrations, executed commercial/tenant integrity checks, and started the current Web image.
- Restore evidence: 2 tenants, 10 products, 3 invoices, 6 stock movements, 6 business partners, zero tenant-relation mismatches, and healthy readiness/database checks.
- No source database record was modified and no secret value was committed or written to the Memory Bank.

## 2026-07-17 - Scale, Security and Reliability P1.0 Complete

- Added an isolated `noxstock-performance` Docker Compose environment using `NoxStockPerformance`, loopback ports 5440/8081, and separate volumes; development/production data was not used.
- Added deterministic, idempotent, cleanable Small/Medium/Target seed profiles. Target was executed twice with exact final counts for two tenants: 100k products, 300k OEM rows, 10k partners, 50k documents, 200k document items, 200k stock movements, and 100k account transactions per tenant.
- Enabled performance-only `pg_stat_statements`, I/O timing, configurable slow-query logging and timeout/pool budgets. Added connection, lock/deadlock, table/index I/O, top-query, health, row-count, and tenant-integrity capture.
- Captured all 14 required `EXPLAIN (ANALYZE, BUFFERS, SETTINGS)` JSON plans and a top-20 pg_stat_statements snapshot. Every required plan contains TenantId filtering; all parent-child tenant mismatch checks are zero.
- Measured priorities for P1.1: product/OEM N+1, repeated POS rate/OEM reads and expensive substring search, deep offset spill, SKU/barcode sequential scans, report over-materialization, and dashboard/document query shape. No optimization was implemented in P1.0.
- Target data volume is proven on a local 12-CPU/30-GiB machine; target concurrency, endpoint p95/p99, production resource requirements, and 100-300-user readiness remain unproven.
- No application query, index, cache, cursor contract, migration, or existing seed was changed.
- Verification: build 0 warnings/0 errors; Domain 2/2, EntityFrameworkCore 36/36, Web 41/41; no pending EF model changes; PostgreSQL healthy, Web/DB health `Healthy`, pg_stat_statements visible, Compose/shell/JSON/diff validation passed.

## 2026-07-17 - Scale, Security and Reliability Gate P0.1 Complete

- Closed anonymous access to commercial conventional APIs with explicit NoxStock permissions and real API 401/403 responses.
- Added host/tenant/inactive-tenant boundary enforcement and anonymous HTTP coverage for 15 commercial read endpoints; Web tests pass 41/41 on isolated rerun.
- Kept global read-only USD/EUR rates public by design; refresh remains host-only.
- Disabled PII/security-artifact logging by default and added bounded redaction for logs, diagnostics and ABP audit payloads.
- Removed operational secrets from base configuration and legacy documentation/scripts. Added fail-closed Production configuration, required secret/certificate injection and a secret rotation/rollback runbook.
- Removed Swagger/ABP Studio runtime chains, pinned Scriban 7.2.5 and added a CI high/critical advisory gate. Web and DbMigrator package scans report no vulnerabilities.
- Verification: build succeeds with 0 errors and 127 existing nullable/Mapperly warnings; Domain 2/2, EntityFrameworkCore 36/36 and Web 41/41 pass. EF reports no pending model change. Initial broad EF and expanded Web runs each encountered the known intermittent SQLite disposal race during host seeding; both isolated reruns passed completely.
- Docker verification with the normal one-command development flow succeeds without deleting data: PostgreSQL healthy, DbMigrator exit 0, Web on port 8080, health 200, anonymous Dashboard/Product 401, Swagger 404, exchange-rate read 200.
- No migration or production database change was made. Production deployment remains blocked until real secrets/certificates are rotated and trusted HTTPS proxy behavior is verified.

## 2026-07-17 - Scale, Security and Reliability Phase P0 Complete

- Audited product/OEM, stock, document/item, current-account, account-transaction, dashboard, report, tenant and audit paths for pagination, unbounded loads, N+1 behavior, projection, search and indexing.
- Audited authentication/authorization, tenant routing, cookies, proxy/TLS headers, rate limiting, secrets, PII/audit logging, dependency advisories, health/Swagger exposure, Docker hardening and production operations.
- Confirmed anonymous HTTP 200 access to tenant Dashboard and Product data. This is the immediate release-blocking defect.
- Captured a small functional baseline only; no target-scale load result exists, so NoxStock is not yet declared scalable for 100k products or 100-300 concurrent users.
- Added the detailed 20 performance + 20 security/operations register, measurable success criteria and P0.1/P1-P5 plan in `scaleSecurityReliability.md`.
- Build passed with 0 warnings/errors. Domain 2/2, EntityFrameworkCore 36/36 on rerun and Web 16/16 tests pass; one initial SQLite initialization failure was intermittent. Dependency scanning found high-severity transitive advisories.
- No application code or migration changed during P0.

## 2026-07-16 NoxStock Product Rename

- Renamed all active backend, frontend, ABP, test, solution, project, localization, permission, setting, proxy, and Docker identifiers to NoxStock.
- Renamed the operational PostgreSQL database to `NoxStock` and moved active persistence to `noxstock-postgres-data` and `noxstock-data-protection` without deleting rollback volumes.
- Added reversible persisted-identifier migrations `20260716204050_Rename_Product_To_NoxStock` and `20260716205742_Complete_NoxStock_Persisted_Rename`.
- Confirmed persisted rename results: 6 settings, 205 permission grants, and the OpenIddict scope use NoxStock; a scan of every PostgreSQL text column found no old product-name value.
- Confirmed runtime names: `noxstock-postgres`, `noxstock-dbmigrator`, `noxstock-web`, `noxstock-dbmigrator:latest`, and `noxstock-web:latest`.
- Verification: solution build succeeds; Domain 2/2, EntityFrameworkCore 35/35, and Web 16/16 tests pass; Docker health/login smoke checks return HTTP 200.

## 2026-07-16 Commercial Stabilization Gate

- Added explicit transactional units of work to invoice approval/payment/cancellation, POS creation/cancellation, and offer/order conversion workflows.
- Added domain-owned document status transitions and rejected invalid backward or repeated terminal transitions.
- Added tenant-scoped operation-key idempotency for invoice payments and POS sales, backed by filtered unique indexes in migration `20260716155704_Add_Commercial_Operation_Idempotency`.
- Verified that duplicate approval, payment, cancellation, and POS retries do not create a second stock movement, account transaction, current-account balance effect, or commercial document.
- Extended tenant A/B integration coverage to invoice payments while retaining product, document, POS, stock, relation, and report isolation coverage.
- Stabilized the SQLite integration runner by serializing test collections and keeping its shared in-memory connection alive for the test host lifetime.
- Verification result: solution build 0 warnings/0 errors; EntityFrameworkCore 35/35; Domain 2/2; Web 16/16; EF reports no pending model changes; `git diff --check` passes.
- Docker result: PostgreSQL healthy; host, `asd`, and `asd2` migrations successful; Web running at `http://localhost:8080`; host and tenant login smoke checks return HTTP 200.
- Designed but did not implement Branch, Warehouse, WarehouseStock, StockTransfer, and StockReservation. The approved invariants and staged Product.Quantity migration are in `inventoryArchitecture.md`.
- Remaining reliability work: production-PostgreSQL rollback fault tests, authenticated Playwright A/B scenarios, graceful handling of concurrent unique-key races, and reconciliation tooling before warehouse backfill.

Last updated: 2026-07-16

## What Works

- OEM/reference-code product lookup is hardened for spare-parts inventory. Multiple product cards and brands can share one OEM without a uniqueness restriction; product and POS searches return every matching alternative. Search ignores case, spaces, and punctuation (`34116775277`, `34 116 775 277`, and `34-116-775-277` match alike), existing codes are backfilled by migration `20260716152420_Add_Product_Oem_Search_Code`, and the UI displays OEM values in product and POS results. A focused integration test verifies three alternatives for BMW OEM `34116775277`.
- Public registration UI is removed. New companies and their administrator accounts are provisioned only from host Tenant Management; login copy now clearly distinguishes platform administrators from tenant/company users.
- Login visual pass completed: full-width desktop composition, readable brand copy without clipping, balanced form column, compact controls, and responsive mobile collapse. Docker health verification passed after rebuild.
- Tenant login context is explicit: `/t/{tenant}/Account/Login` renders the resolved company name above the form, while host `/Account/Login` remains platform-only. Verified live with `/t/asd/Account/Login` showing `asd` and health returning HTTP 200.
- Tenant context presentation was refined: `Company: asd` now sits as a plain line on the left brand panel above the commercial-platform label, with no box around either text and no duplicate tenant chips in the form.
- Dashboard TL display is corrected from generic `¤` to explicit `₺`; the fix is centralized in the dashboard formatter and does not alter USD/EUR product or POS pricing.
- Language switching now follows the ABP controller contract (`culture` plus `uiCulture`) and preserves the tenant path on redirects. This resolves the former invalid-uiCulture trace error from topbar and login language buttons.
- The shared UI localization baseline is now active: TR/EN resources drive the sidebar, topbar, login-brand content, login placeholder, and dashboard. The Web suite includes the ABP language-switch redirect/cookie regression test, and live Docker verification confirms tenant English login content at `/t/asd/Account/Login` after switching languages.
- Platform administration is now cleanly separated from tenant operations. Host sidebar: Platform Administration -> Companies, Users & Roles, Audit Logs. Tenant sidebars no longer expose user/role or audit links, and direct tenant-path requests to those host routes return HTTP 403. The tenant sidebar now uses Workspace, Operations, and Customers & Organization headings, with orders under Commercial rather than business partners. Web tests pass 16/16 and Docker host-admin verification returns HTTP 200 for Audit Logs.
- Desktop visual density and shell clipping pass completed: desktop UI now targets 90% rem density at normal browser zoom, and topbar/sidebar headers use fixed non-shrinking 64px dimensions. The web image was rebuilt without cache; the running container serves the new CSS and login responds with HTTP 200.
- NoxStock branding assets are now integrated across auth and application layouts. The supplied favicon set is published from `wwwroot`, the manifest is named NoxStock with branded theme colors, and live Docker checks return HTTP 200 for `favicon.ico` and `site.webmanifest`.
- The actual application logo now replaces the placeholder `N` in the sidebar, topbar, and login brand header. All three surfaces use the same supplied Android logo asset, which is present and served successfully by the running Docker web container.

- **Frontend foundation pass completed**:
  - Shared light/dark color tokens now cover cards, forms, tables, DataTables, modals, reports, and error notices.
  - The main layout uses responsive gutters and a bounded operational content frame; theme selection is applied before initial paint.
  - Sidebar/topbar density and page action headers are responsive for tablet/mobile widths.
  - The login/register shell is now NoxStock-branded and dark-mode compatible without decorative gradients, glass effects, or remote avatar dependencies.
  - Invoice, report, product and settings pages now use shared form/table rules instead of conflicting inline style blocks.
  - Docker validation after the pass succeeded using the standard `docker compose up -d` stack at `http://localhost:8080`.

- ABP layered solution is present.
- Core product/catalog entities exist:
  - Product
  - ProductOEMCode
  - Brand
  - Category
- Commercial base entities exist:
  - BusinessPartner
  - Document
  - DocumentItem
  - DocumentType
  - DocumentStatus
- Prototype ledger entities exist:
  - Account
  - AccountTransaction
  - StockMovement
- Razor Pages exist for many target modules.
- Products, brands, categories, business partners, and documents have application services.
- Accounts have an application service.
- Documents have create/update line handling with recalculated totals.
- Sale and purchase invoices can be approved/cancelled through `DocumentWorkflowAppService`.
- Approval currently mutates stock, customer/supplier balance, account balance, and creates ledger rows.
- Cancellation currently creates reversal stock/account rows and restores the source balances.
- Dashboard has a real query service and no longer depends on static zero values for implemented KPIs.
- Dashboard shows a six-month sales trend, five critical-stock products, and five top-selling products from live commercial data.
- Approved and partially paid sales/purchase invoices can receive additional invoice-level collection/payment entries from the invoice list.
- Invoice payment completion updates document status to `Paid`; cancellation reverses all linked account movements while retaining audit history.
- Dashboard lists overdue and next-seven-day open invoices; paid and cancelled documents are excluded.
- Current account document list exposes due date and outstanding balance, highlighting overdue open items.
- Current-account detail includes a dedicated invoice-linked payment history with the account used, transaction direction, amount, date, description, and reversal records.
- Real NoxStock permission constants and definitions exist.
- BookStore tutorial source modules were removed.
- Root page redirects to Dashboard.
- Web build passes.
- DbMigrator build passes.
- Test suite passes when VSTest can open its local socket.
- **Phase 1: Commercial Foundation Completed**:
  - Account UI wired to account service
  - Sale invoice UI wired to create/approve/cancel workflow
  - Purchase invoice UI wired to create/approve/cancel workflow
  - Business partner transaction history
  - Account transaction history
  - Stock movement history
  - Purchase invoice approval/cancellation test
- **Phase 2: Invoice and Current Account Workflow Completed**:
  - Invoice-level partial and closing collection/payment
  - Reversal of every payment movement on cancellation
  - Due-date monitoring on dashboard and invoice list
  - Current-account statement with payment history
- **Phase 3: POS Completed**:
  - Barcode, product name, and SKU lookup
  - Fast single-currency cart with server-side stock and price validation
  - Cash, bank/card, and customer-credit checkout
  - Recent POS sales and reversal-based cancellation
  - POS turnover included in dashboard revenue and product rankings
- **Phase 4: Offers and Orders Completed**:
  - Offer and sales/purchase order creation using the existing commercial document editor
  - Draft-to-sent transition before conversion
  - Offer to sales order or invoice conversion
  - Sales/purchase order to matching invoice conversion
  - Source-document link, totals, and item preservation through conversion
- **Phase 5: Reporting Completed**:
  - Date-filtered operational reporting built from commercial source transactions
  - Sales, purchases, income/expense, collections/payments, net cash flow, receivables, and payables
  - Cash/bank account summary, stock/critical-stock and movement lists, customer/supplier turnover, VAT, aging, and estimated product profitability
  - Document currency totals converted through the exchange rate stored on each document
  - Focused report-query coverage for ledger, VAT, aging, stock, and profitability calculations
- **Phase 6: Settings and Printing Completed**:
  - Tenant-aware company profile and commercial defaults saved through ABP Setting Management
  - Default currency, unit, VAT rate, cash/bank accounts, document prefixes, POS prefix, and label format
  - New product and document defaults wired to saved settings
  - POS checkout uses compatible default cash/bank accounts
  - Barcode/QR label selection, preview, and printing screen
  - Persistent light/dark preference
- **Phase 7: Notifications and Audit Views Completed (AI excluded)**:
  - Live critical/out-of-stock product alerts
  - Overdue and next-seven-day sale/purchase invoice alerts
  - Topbar alert count and preview linked to the notification page
  - Filterable ABP audit-log screen with date, user, HTTP method, and exception filters
  - Dedicated notification and audit-log permissions and navigation

## What's Left To Build

### Live Deployment Baseline

- The stabilized host/tenant login split and tenant commercial experience have been deployed live.
- Host administrators remain limited to tenant/company provisioning; tenant users enter their own `/t/{tenantName}` commercial workspace.
- The immediate release gate is now operational reliability: production regression checks, backups, monitoring, error reporting, TLS/secrets review, and repeatable deployment/rollback procedures.
- Centralized error observability is now implemented: unhandled server errors are logged with trace ID, tenant/user context, request method, and path; programmatic requests receive JSON problem responses and page requests receive a NoxStock error page with a support tracking code.
- Browser and jQuery AJAX failures are surfaced to users as non-blocking error notices and reported through an authenticated diagnostics endpoint. The endpoint was live-tested as tenant `asd`, returned HTTP 202, and wrote the tenant/user-scoped event to Docker Serilog output.
- Serilog file retention is bounded by daily rolling files, a 30-file retention limit, and a 10 MB per-file cap.

### SaaS Multi-Tenancy Hardening

- Completed two-tenant isolation test infrastructure.
- Completed trusted development route resolution; production subdomain resolution remains deployment work.
- Completed tenant-required commercial service guards.
- Completed host/tenant permission separation.
- Completed application-layer cross-tenant foreign-key validation.
- Completed tenant activation/deactivation enforcement.
- Completed host-only global exchange-rate refresh and worker separation.
- Closed public registration by default; abuse-resistant public provisioning remains deferred until explicitly enabled.
- Completed null-tenant dry-run reporting; manual ownership mapping remains an operational decision.
- Completed DbMigrator order review and tenant-seed idempotency verification.
- Completed final verification: 31 EntityFrameworkCore tests, 13 Web tests, no pending EF model changes, and all nine migrations applied in local Docker PostgreSQL.
- Docker stack is running: `noxstock-postgres` healthy on host port `5439`, `noxstock-dbmigrator` completed successfully, and `noxstock-web` is available at `http://localhost:5002`.
- Host admin UX now restricts the sidebar to tenant management. The ABP Tenant Management new-tenant modal script bundle and its required `#AbpContentToolbar` selector are rendered by the custom application layout; the authorized modal endpoint returns HTTP 200.
- Login and logout hardening completed: failed sign-ins show a form-level error, tenant sign-ins remain under `/t/{tenant-name}`, and logout clears the identity cookie before returning to login. Verified against the live Docker stack.
- Tenant login now supports the documented username-or-email field correctly: it resolves e-mail addresses through `UserManager.FindByEmailAsync` before password sign-in. This fixes valid provisioned administrator e-mail credentials being rejected by the username-only sign-in overload; failure outcomes are logged for diagnosis.
- Tenant login now writes the authenticated user's ABP tenant claim to the identity ticket. This prevents tenant dashboard requests from being treated as host sessions and redirecting to `/TenantManagement/Tenants`; live Docker verification renders `/t/asd/Dashboard` successfully.
- Docker now retains ASP.NET Data Protection keys in the `noxstock-data-protection` volume, preventing stale login-form antiforgery tokens from returning HTTP 400 after a Web container recreate. This was validated with a token captured before and posted after a forced recreate.
- Finalized tenant onboarding UI: host administrators now create a company together with a chosen administrator username, email and password instead of relying on the hidden ABP default `admin` username. The form shows the tenant login route convention before creation.
- Tenant list reliability fix: the host company page no longer relies on the ABP list endpoint that returned an empty payload for persisted rows. It reads `ITenantRepository` directly, so successful provisioning reloads into a visible row.
- Tenant provisioning reliability fix: the onboarding page sets the required tenant `IsActive` extension property before persistence, seeds the tenant administrator, handles duplicate codes, logs unexpected exceptions, and shows validation errors in the modal. A live Docker POST returned HTTP 204 and was visible in the table immediately.
- Final host/tenant login split is live-verified: `/Account/Login` with the host admin opens only company management, while `/t/asd/Account/Login` with the provisioned tenant administrator opens `/t/asd/Dashboard` and permits tenant commercial pages. Tenant users are redirected away from `/TenantManagement/Tenants`.
- Tenant navigation is path-base aware: sidebar and logo links resolve under `/t/{tenantName}` instead of dropping the tenant context. The rebuilt Docker UI was verified to emit `/t/asd/Dashboard`, `/t/asd/Products`, `/t/asd/Accounts`, `/t/asd/Reports`, and `/t/asd/Settings` links.
- Host-to-tenant session switching is fixed. Identity cookies are always written at `/`, old `/t/{tenantName}` cookies are removed on login entry, authenticated login entry performs a clean redirect before antiforgery rendering, and a regression test locks the root-cookie contract. A live same-cookie-jar host→`asd` flow reaches the tenant dashboard with exactly one identity cookie.
- The topbar distinguishes identity context: tenant sessions display administrator e-mail plus tenant name (`asd@asd.asd` / `asd` in the live check); host sessions display the host username plus the localized platform-management label.

### Product Hardening

- Invoice excellence phase is active. Completed the first integrity slice: server-authoritative Draft creation, payment/status tamper prevention, partner/date/line validation, deterministic four-decimal totals, unique tenant/type document numbers, compatible payment-account currencies, and a browser-printable invoice page.
- Added and applied migration `20260715174844_Add_Document_Number_Uniqueness` through the current Docker DbMigrator.
- Remaining invoice work: detail/edit view, payment allocation/reconciliation history, tax-inclusive and withholding rules, PDF/email/archive delivery, and an end-to-end tenant browser flow.
- Product pricing transparency: invoice item lookup and POS now expose the product-card purchase price, original sale price, their currencies, and the current effective sale price together. This preserves foreign-currency product-card values while making the actual sale calculation visible.
- Exchange-rate reliability: the hourly hosted refresh uses TCMB official XML and logs successful source/date/rate values. A Docker startup refresh completed successfully after the DbContext/Unit of Work lifecycle fix.
- Fixed the Turkish-culture invoice submit failure reported with trace `0HNN2JM3FHH6C:000000B8`. Decimal range metadata that parsed the dot-decimal lower bound during Razor validation has been removed in favor of the existing application-service validation. The invoice entry grid was also redesigned with stable column widths and mobile-safe scrolling so VAT and discount fields remain usable.
- Corrected the next Turkish-culture invoice binding issue: date inputs now render ISO values for the browser (`yyyy-MM-dd`) rather than posting localized `dd.MM.yyyy` values. The dedicated invoice table layout replaces the generic table spacing that caused header and input columns to appear offset.
- Added a server-side date-post fallback on invoice creation. Both localized Turkish dates and ISO dates are parsed before model-state validation, which closes the remaining browser-culture dependency. Docker Desktop's `desktop-linux` context is the canonical local environment: the application is `http://localhost:8080` and PostgreSQL external access is `localhost:5439`.
- Cleared the ABP client warning caused by an unloaded NoxStock localization resource by adding the culture-aware application-localization script to the shared layout. Hardened DataTable row-action visibility callbacks in invoices, offers, and orders so the ABP callback can pass either its wrapper or a raw record without a JavaScript exception.
- Production regression walkthrough across tenant onboarding, authentication, products, invoices, POS, offers/orders, reports, notifications, and audit logs
- Pagination and export polish for high-volume reports and audit logs
- Frontend/runtime regression pass across all implemented workflows
- CI build/test/migration/container smoke checks and deployment rollback verification
- Global error handling, health checks, structured logging, alerting, backup and restore runbook
- Phase 8 inventory core: warehouse/location, transfers, returns, reservations, batch/serial tracking, and historical costing
- Phase 9 accounting and Turkish tax/e-document workflows
- Phase 10 advanced purchasing, sales, delivery, pricing, and approval workflows
- Phase 11 CRM and customer/supplier success workflows
- Phase 12 multi-branch, multi-currency, and tenant maturity
- Phase 13 management intelligence and reconciled reporting
- Phase 14 UX, accessibility, imports, exports, and mobile/tablet operations
- Phase 15 integrations and platform APIs
- Phase 16 security, KVKK, reliability, and disaster recovery
- AI assistant features are explicitly excluded from the current scope

## Known Issues

- Several DTO/page model properties currently produce nullable warnings in some builds.
- Some Razor Pages are UI skeletons and not connected to full business rules.
- Dashboard is service-backed, but charts are still placeholder-level and need real time-series/report data.
- Product profitability is an operational estimate because it uses the current product-card purchase price, not a historical inventory-costing ledger.
- Notification read state is currently live/derived from source data; persistent per-user dismissal is a later product-hardening item.
- Application/Domain test projects currently contain no meaningful tests.
- Historical migration designer files still contain Book/Author snapshots, which is expected for migration history.
- Existing Mapperly warnings for audit/base properties are noisy but not blocking.
- A forced clean rebuild currently reports 153 nullable/Mapperly diagnostics even though the normal incremental build is clean; warning reduction remains explicit technical debt.
- Gate P0.1 removed the MessagePack and Microsoft.OpenApi runtime chains, pinned patched Scriban 7.2.5, and added CI scanning. Current Web and DbMigrator transitive scans report no advisories.

## Decision History

- 2026-08-13: Production pricing UI pass completed across Products, invoice create/edit, POS, price history and purchase stock movements. Technical property/model names were replaced with current cost, latest purchase, active sale and profit language; purchase cost impact and live below-cost feedback are now visible at the point of work. Focused integration tests pass 31/31.
- 2026-08-13: POS quick-customer creation was added as a compact modal, and POS product cards now display shelf/location information when available.
- 2026-08-13: Fixed the inventory-summary cost-source mismatch: reference costs were used for product profit but excluded from total stock value and missing-cost counts. The summary now uses the same effective cost path and labels reference-based values honestly.

- 2026-08-03: Invoice quick-product creation now uses the ABP/jQuery promise `always` lifecycle to restore the save button after either outcome; this fixes subsequent in-invoice product creation being stuck at “Kaydediliyor…”.
- 2026-08-03: POS kartları ürün sayısı arttığında kaydırma alanının önceki konumunda kalmayacak şekilde sıfırlanıyor; sepet içindeki fiyat/KDV kontrolleri dar panelde dengeli oranlarla hizalanıyor.
- 2026-08-03: POS ürün kataloğu, ürün sayısı arttığında sayfa yüksekliğini büyütmemesi için sınırlı yükseklikli iç kaydırma alanına alındı; geniş ekranlarda dört kolonlu yoğun görünüm kullanılıyor.
- 2026-08-03: POS sepetinde KDV oranı satır bazında görünür ve düzenlenebilir hale getirildi; fiyat input'u artık her tuşta DOM'dan yeniden oluşturulmadığı için odak korunuyor ve fiyat oklarının adımı 1 olarak sabitlendi.
- 2026-08-03: Product catalogue now defaults to a compact daily-work column set and separates TRY-only from TRY-plus-reference-currency presentation. Reference amount, invoice rate and reference sale columns move together; user column widths/visibility persist, and non-stock-tracked items no longer appear as zero/critical stock.
- 2026-08-03: POS cart lines now allow an explicit legal KDV selection; the selected rate is recalculated live, validated server-side, and stored on the commercial document item snapshot.
- 2026-08-03: The invoice A4 print view was generalized for offers, sales orders and purchase orders, and print actions were added to both commercial lists.
- 2026-08-03: Sidebar navigation groups were changed from collapsible menus to permanently visible static groups so page destinations no longer move or disappear.
- 2026-08-03: Added a tenant-scoped operational data reset from Settings. It clears commercial records (products, lookups, documents, stock/account history, partners and accounts) while retaining tenant identity, users, license, company profile and global exchange rates. Reset requires settings-edit authority plus typed `SIFIRLA` confirmation and acknowledgement.
- 2026-08-03: Product creation was reduced to a single required business field (`Name`); blank SKU values are generated server-side and all optional text fields receive safe defaults.
- 2026-08-03: Added tenant- and license-aware `.xlsx` stock-count import to the Products page. Import upserts by SKU, uses the first duplicate row, creates missing brand/category lookups, maps tags to OEM references, and ignores spreadsheet-corrupted scientific-notation barcodes.
- 2026-08-03: `SON STOK SAYIM.xlsx` was verified against a temporary integration database: 806 source rows resolve to 470 unique products and 336 duplicate rows, without touching production data.
- 2026-08-03: Corrected the stock-import decimal parser for exported values such as `1.0`; those values are now interpreted as one unit rather than ten under the Turkish server culture. The correction also supports Turkish comma decimals and both common grouped-number formats, and was verified against `SON STOK SAYIMI TÜMÜ.xlsx` in an isolated integration test.

- 2026-07-10: ASP.NET Core + Razor Pages + ABP confirmed as final product direction.
- 2026-07-10: Legacy React/Node implementation confirmed as reference only.
- 2026-07-10: BookStore tutorial source artifacts removed.
- 2026-07-10: NoxStock module permission system introduced.
- 2026-07-10: Next phases fixed around commercial workflow correctness before AI or polish.
- 2026-07-10: Phase 1 backend prototype slice added for accounts, stock movements, dashboard summary, and document approval/cancellation.
- 2026-07-10: Cancellation policy confirmed as reversal-based commercial history preservation.
- 2026-07-10: Phase 1 completed successfully with UI wiring and Purchase Invoice tests. Focus shifted to Phase 2.
- 2026-07-13: Shared database/shared schema confirmed as the initial SaaS model; multi-tenancy security hardening started before Branch/Warehouse expansion.
- 2026-07-13: `/t/{tenantName}` selected for development resolution and `{tenant}.noxstock.com` selected for production.
