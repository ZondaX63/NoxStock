> Historical record archived 2026-09-10. Do not use as current architecture,
> requirements, or verification status; use `docs/performance/` and source.

# Frontend Performance and Responsive UX Workstream

Last updated: 2026-07-31

## Objective

Make NoxStock pages render and respond faster without replacing the ABP layered monolith,
Razor Pages, application-service contracts, tenant isolation, financial server authority, or
commercial audit rules. The first priority is real frontend load time and interaction latency;
loading visuals may communicate bounded asynchronous work but must not hide a slow page.

Production image build/deployment optimization is explicitly deferred. The current workstream
targets browser, shared layout, page bootstrap, API payload, and read-query behavior.

## Baseline Evidence

The existing target-data baseline under `docs/performance/p1.0-baseline.md` remains the source
of truth for query priorities:

- Product page: 52 SQL commands for 50 rows before the later pricing enrichments.
- POS product search: 42-62 SQL commands for 20 products and approximately 281 ms direct SQL.
- Dashboard: 13 SQL commands.
- Deep product offset page: approximately 160 ms direct SQL with temporary spill.
- Report snapshot: structurally unbounded product/partner/account materialization.

These are single-user SQL measurements, not browser p95/p99 or production-capacity proof.

## UX and Architecture Rules

- Do not reintroduce a global full-page skeleton or artificial wait.
- Keep sidebar, topbar, page header, and layout dimensions stable during asynchronous loads.
- Use local loading states only for the table, card, dropdown, or command that is waiting.
- Do not cache authenticated tenant HTML publicly.
- Cache keys for tenant data must include tenant/path context and, where relevant, user/permission context.
- Client calculations and caches are previews only; POS/invoice prices and snapshots remain server-authoritative.
- Do not parallelize EF queries on the same DbContext. Remove N+1 behavior through batching/projection.
- Preserve ABP authorization, license enforcement, auditing, antiforgery, localization, and modal conventions.
- Measure before and after each batch; do not claim a speedup from animation alone.

## TODO

### P0 - Browser and Endpoint Measurement

- [ ] Capture cold and warm navigation for Dashboard, Products, POS, Invoices, and Reports on the Oracle VM (deployment-run evidence; collection tooling is ready).
- [ ] Record target-VM TTFB, FCP, LCP, CLS, transferred bytes, request count, and long tasks (opt-in collection endpoint and instructions are ready).
- [x] Add safe server timing/trace correlation for page render and high-cost application endpoints.
- [x] Capture endpoint SQL command counts beside browser timings through bounded Product/POS/Dashboard/Alerts/Reports regression tests; target PostgreSQL recapture remains a deployment run.
- [x] Define an initial private-beta performance budget for the 1 OCPU / 6 GB production host.

### P1 - Shared Layout Critical Path

- [x] Response compression enabled with Brotli/Gzip Fastest.
- [x] Remove synchronous operational-alert data loading from the topbar render path.
- [x] Load the topbar alert summary after first paint and cache it briefly per tenant path and user.
- [x] Reserve notification badge/dropdown dimensions to avoid layout shift.
- [x] Cache the global USD/EUR ticker in the browser and fetch only when stale.
- [x] Remove duplicate Outfit stylesheet requests.
- [x] Self-host and pin Outfit, Material Icons, Alpine.js, barcode, and QR runtimes.
- [x] Inspect ABP configuration/localization/service-proxy response handling; authenticated bootstrap responses remain private and their transfer sizes are captured by the opt-in metrics collector.
- [x] Add one-year immutable app-layer caching only for successful fingerprinted static assets; the external reverse proxy must preserve this header during deployment.
- [x] Keep global page overlays disabled; retain only component-level loading states.

### P2 - Product Catalogue and POS Hot Paths

- [x] Batch OEM, last-cost, document, supplier, and exchange-rate reads for product list pages.
- [x] Use one bounded joined page projection for Product, brand/category and last-purchase context instead of row-by-row detail helpers; OEM and rate enrichments are batched/request-cached.
- [x] Keep product list enrichment SQL command count constant as page size grows; the focused test bounds all intercepted application/ABP reads to 12 for a 12-row page, pending target PostgreSQL recapture.
- [x] Batch POS OEM, cost-history, and rate reads; target PostgreSQL command-count recapture remains pending.
- [x] Route exact barcode, SKU, and normalized OEM lookup before general substring search.
- [x] Add the baseline-proven tenant-first exact SKU/barcode indexes; prefix/trigram indexes remain measurement-gated.
- [x] Sequence-protect remote invoice, POS, payment, and label searches and standardize them at 250-300 ms debounce.
- [x] Replace the unbounded POS customer bootstrap list with bounded remote search.

### P3 - Invoice and Lookup Bootstrap

- [x] Replace 1,000-product and 1,000-partner Razor option lists with bounded remote lookups (20 results per request; server cap 30 from the invoice endpoint).
- [x] Preserve quick in-context product creation and append the created item to the active lookup.
- [x] Load the selected value directly when editing instead of repopulating the complete catalogue.
- [x] Batch product validation during document create/update, reuse per-product calculations, and remove the Web-layer detail fetch per zero-price line.
- [x] Keep invoice HTML and DOM size independent of total product/customer counts.
- [x] Replace the invoice payment modal's 1,000-account Razor option list with a currency-matched bounded remote lookup.

### P4 - Lists, Dashboard, Alerts, and Reports

- [x] Bound operational alerts to aggregate counts plus four preview rows; paginate the notification page at 20 rows.
- [x] Reduce Dashboard reads to bounded aggregates and add a 15-second tenant-scoped cache.
- [x] Push report aggregation into the database and paginate every detail section independently.
- [x] Audit export paths: there is no large synchronous report export endpoint today. Any future export must use a background job rather than reintroduce request-time materialization.
- [x] Bound offset paging to 10,000 rows. Cursor navigation was not forced into current DataTables page-number UX; it remains a future history-specific contract when target PostgreSQL evidence justifies it.
- [x] Preserve DataTables filters, visible columns, ordering and paging in session storage and debounce built-in searches.

### P5 - Rendering and Interaction Polish

- [x] Audit `transition-all`, blur, large shadow, and unnecessary paint-heavy effects; remove broad transitions from page controls/charts and neutralize sidebar paint-heavy transitions.
- [x] Add `prefers-reduced-motion` behavior and keep active animations to transform/opacity or targeted color transitions.
- [x] Reserve table/card/loading dimensions to prevent CLS without a global skeleton.
- [x] Add command-local pending state and duplicate-submit protection for full-page forms, while preserving client validation.
- [x] Evaluate hover/idle prefetch and reject it for the single-CPU/private-beta deployment because speculative authenticated requests compete with real navigation.
- [x] Evaluate partial-main navigation and retain normal Razor navigation: ABP modal lifecycle, DataTables state, antiforgery and tenant PathBase make a custom PJAX layer higher-risk than the measured benefit.

### Deferred - Build and Deployment Pipeline

- [ ] Build immutable Web/DbMigrator images in CI and pull them on the production VM.
- [ ] Remove duplicate Docker build/publish work and enforce frozen frontend lockfiles.
- [ ] Avoid source builds competing with production traffic on the single-vCPU host.

## Acceptance Targets for the First Implementation Batch

- Shared page HTML no longer waits for operational-alert database queries.
- Repeated page navigation within the ticker freshness window produces no additional rate request.
- Only one Outfit stylesheet definition remains.
- Topbar has stable dimensions before alert/rate data arrives.
- Existing authorization and tenant PathBase behavior remains intact.
- Changed JavaScript passes syntax checks and the solution builds without new errors.

## 2026-07-31 First Batch Result

- Topbar authorization is still server-rendered, but operational alert data no longer blocks the shared layout.
- Alert data is fetched during browser idle time and kept for 30 seconds in session storage with tenant-path and user context in the key.
- Currency data renders from a 15-minute local cache; page changes inside the freshness window do not start another rate request.
- The duplicate Google Outfit import was removed; self-hosting the remaining external assets is still pending.
- Product list purchase context is joined in the page query, OEM codes are loaded once per page, and the transient price calculator reuses each currency read for the request.
- POS lookup loads all referenced cost histories in one query and shares the request-local rate cache.
- Focused Product/POS EF tests pass 7/7. The new 12-row query-bound test observes 11 commands including ABP permission/UOW infrastructure, below its regression ceiling of 12.
- Web authorization tests could not execute on this workstation because the `Microsoft.AspNetCore.App 10.0.0` runtime is absent; the Web project itself builds successfully.
- A JavaScript runtime is not installed on this workstation, so `node --check` remains pending; JSON parsing and `git diff --check` pass.

## 2026-07-31 Invoice Lookup Batch Result

- Invoice create/edit no longer reads or renders 1,000 products and 1,000 business partners at page bootstrap.
- A dedicated lightweight product lookup projection returns only ID, name, SKU, barcode, unit, and quantity; it does not calculate pricing or hydrate product detail context.
- Product and business-partner searches are debounced by 250 ms, capped at 20 client results, sequence-protected against stale responses, and cached for the current page session with a 50-query bound.
- Exact barcode, SKU, product name, tax number, or partner name can be selected with Enter; zero product matches preserve the existing in-context quick-product creation path.
- Draft editing and validation failures render only currently selected products/partner. The new-row template contains one placeholder option, so total catalogue size no longer multiplies invoice HTML or DOM size.
- Web build passes with 0 errors using single-worker MSBuild (`-m:1`). The focused lightweight lookup EF test passes and keeps its intercepted reads under the six-command regression ceiling.
- JavaScript executable syntax validation remains pending because no Node/Bun/Deno/QuickJS runtime is installed locally; `git diff --check` passes.

## 2026-07-31 POS Customer Lookup Result

- POS checkout options now load only the bounded account/default-setting data required at startup; the complete customer catalogue is no longer included in that response.
- TRY customers are searched by name, tax number, or phone through a tenant/license-protected endpoint with 20 browser results and a 30-row server cap.
- The customer picker uses 250 ms debounce, stale-response sequencing, a 30-query page-lifetime cache, exact Enter selection, and selected-option preservation while cart/payment renders continue.
- Cash, bank/card, walk-in customer, and required customer-credit semantics are unchanged.
- Web build passes with 0 errors. Focused POS plus host/tenant-boundary tests pass 7/7.

## 2026-07-31 Document Save Batch Result

- Document create/update now loads every referenced product in one tenant-filtered repository query and rejects missing IDs before line processing.
- Repeated lines for the same product reuse one server pricing calculation; exchange-rate reads remain request-cached by currency.
- The Razor Page no longer performs a full `ProductAppService.GetAsync` call for every submitted zero-price line. Existing fallback behavior moved to the authoritative document service: manual sale uses the product card sale price, purchase uses list/fallback purchase price, and dynamic sale still uses the current server quote.
- Financial snapshot, four-discount, VAT, manual-override, TRY-sales, and historical purchase-rate rules are unchanged.
- Product pricing plus document-integrity tests pass 11/11.

## 2026-07-31 Combined Lookup Verification

- The focused invoice-product and POS-customer remote-lookup regression tests pass 2/2 after the final case-insensitive search adjustment.
- A static scan confirms that the former 1,000-row invoice product/partner bootstrap and POS customer bootstrap references are gone.
- One separate 1,000-row lookup remains in the invoice payment modal account selector (`RecordPaymentModal`); it is tracked for a later bounded-lookup batch because it is outside the create/edit invoice bootstrap path changed here.
- `git diff --check` passes. Browser JavaScript execution and target-VM cold/warm timing remain the release evidence still required for this workstream.

## 2026-07-31 Exact Product and Payment Account Lookup Result

- Product catalogue, invoice product lookup, and POS now probe exact SKU, barcode, or normalized OEM before executing the broader substring search. Exact shared OEM values deliberately return every brand alternative.
- Added non-unique `(TenantId, Sku)` and `(TenantId, Barcode)` indexes in migration `20260731160000_Optimize_Product_Exact_Lookups`; existing duplicate data and uniqueness policy are unchanged. The existing `(TenantId, SearchCode)` OEM index remains authoritative for normalized exact OEM lookup.
- The invoice payment modal no longer embeds up to 1,000 accounts. It loads at most 20 active accounts matching the document currency, with 250 ms debounce, stale-response protection, a bounded page cache, and selected-value preservation on validation failure.
- Focused exact/broad/OEM/account lookup tests pass 5/5; complete ProductPricing and POS service regression classes pass 16/16. Web build succeeds with 0 errors and the repository still requires target-PostgreSQL plan/timing recapture after migration deployment.

## 2026-07-31 Dashboard, Alerts, and Reports Result

- Operational-alert summary counts are database aggregates and the topbar receives at most four preview rows. The notification page uses independent 20-row paging instead of materializing every current alert.
- Dashboard document and partner metrics were consolidated into grouped database reads; the Razor Page keeps a 15-second cache keyed by tenant. Its regression test bounds the complete intercepted request to 14 SQL commands including ABP infrastructure.
- Reports no longer load complete account, partner, product, document-item, or stock-movement tables into memory. Summary values are database aggregates and account, partner, profitability, stock, and movement details have independent bounded pages.
- Historical profitability now uses the sale-line `CostTryAmount` snapshot and KDV-excluded discounted sales, so a later product-cost change cannot rewrite historical gross profit.
- Focused Dashboard/Alerts tests pass 3/3 and Reporting tests pass 2/2.

## 2026-07-31 Critical Path, Rendering, and Measurement Result

- Outfit, Material Icons, Alpine.js, JsBarcode, and QRCode are pinned and served locally. Fingerprinted successful static resources receive immutable one-year browser caching; authenticated HTML and ABP bootstrap responses remain private.
- Table filters/order/paging/column state is retained in session storage. Main DataTables searches use a 300 ms delay and high-volume list services cap page size at 100 and offset at 10,000.
- Broad control/chart animations and blur-heavy loading effects were removed or narrowed. Reduced-motion behavior, stable table space, and opt-in duplicate-submit locking were added without reintroducing the rejected global skeleton.
- Etiket Yazdır no longer embeds and prices up to 2,000 products during page render. It performs stale-response-protected, 300 ms debounced server searches capped at 30 items; its focused bound test passes.
- Dynamic responses expose `Server-Timing` and `X-Correlation-Id`. Opt-in browser metrics record TTFB/FCP/LCP/CLS, requests, bytes, long tasks and load time. `scripts/performance/measure-navigation-http.sh` records cold/warm HTTP evidence for the five release pages.
- The initial 1 OCPU / 6 GB budgets and exact collection procedure are recorded in `docs/performance/frontend-budget.md`. Actual Oracle VM cold/warm measurements remain an external release-evidence step and are not represented as completed local measurements.
- All project JavaScript passes `node --check` in a read-only Node 22 container. The Web project and EF test project build successfully with zero errors, and the full EntityFrameworkCore integration suite passes 73/73. Existing nullable/Mapperly warnings remain pre-existing debt.
- The full suite exposed and closed two adjacent currency regressions: a foreign purchase DTO's neutral default rate no longer overrides its dated snapshot, and the price-calculator rate cache is now scoped to the active Unit of Work so a later request observes a changed live rate.
