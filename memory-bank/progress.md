# Progress

Last updated: 2026-07-16

## What Works

- Public registration UI is removed. New companies and their administrator accounts are provisioned only from host Tenant Management; login copy now clearly distinguishes platform administrators from tenant/company users.
- Login visual pass completed: full-width desktop composition, readable brand copy without clipping, balanced form column, compact controls, and responsive mobile collapse. Docker health verification passed after rebuild.
- Tenant login context is explicit: `/t/{tenant}/Account/Login` renders the resolved company name above the form, while host `/Account/Login` remains platform-only. Verified live with `/t/asd/Account/Login` showing `asd` and health returning HTTP 200.
- Tenant context presentation was refined: `Company: asd` now sits as a plain line on the left brand panel above the commercial-platform label, with no box around either text and no duplicate tenant chips in the form.
- Dashboard TL display is corrected from generic `¤` to explicit `₺`; the fix is centralized in the dashboard formatter and does not alter USD/EUR product or POS pricing.
- Language switching now follows the ABP controller contract (`culture` plus `uiCulture`) and preserves the tenant path on redirects. This resolves the former invalid-uiCulture trace error from topbar and login language buttons.
- The shared UI localization baseline is now active: TR/EN resources drive the sidebar, topbar, login-brand content, login placeholder, and dashboard. The Web suite includes the ABP language-switch redirect/cookie regression test, and live Docker verification confirms tenant English login content at `/t/asd/Account/Login` after switching languages.

- **Frontend foundation pass completed**:
  - Shared light/dark color tokens now cover cards, forms, tables, DataTables, modals, reports, and error notices.
  - The main layout uses responsive gutters and a bounded operational content frame; theme selection is applied before initial paint.
  - Sidebar/topbar density and page action headers are responsive for tablet/mobile widths.
  - The login/register shell is now NexStock-branded and dark-mode compatible without decorative gradients, glass effects, or remote avatar dependencies.
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
- Real NexStock permission constants and definitions exist.
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
- Centralized error observability is now implemented: unhandled server errors are logged with trace ID, tenant/user context, request method, and path; programmatic requests receive JSON problem responses and page requests receive a NexStock error page with a support tracking code.
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
- Docker stack is running: `nexstock-postgres` healthy on host port `5439`, `nexstock-dbmigrator` completed successfully, and `nexstock-web` is available at `http://localhost:5002`.
- Host admin UX now restricts the sidebar to tenant management. The ABP Tenant Management new-tenant modal script bundle and its required `#AbpContentToolbar` selector are rendered by the custom application layout; the authorized modal endpoint returns HTTP 200.
- Login and logout hardening completed: failed sign-ins show a form-level error, tenant sign-ins remain under `/t/{tenant-name}`, and logout clears the identity cookie before returning to login. Verified against the live Docker stack.
- Tenant login now supports the documented username-or-email field correctly: it resolves e-mail addresses through `UserManager.FindByEmailAsync` before password sign-in. This fixes valid provisioned administrator e-mail credentials being rejected by the username-only sign-in overload; failure outcomes are logged for diagnosis.
- Tenant login now writes the authenticated user's ABP tenant claim to the identity ticket. This prevents tenant dashboard requests from being treated as host sessions and redirecting to `/TenantManagement/Tenants`; live Docker verification renders `/t/asd/Dashboard` successfully.
- Docker now retains ASP.NET Data Protection keys in the `nexstock-data-protection` volume, preventing stale login-form antiforgery tokens from returning HTTP 400 after a Web container recreate. This was validated with a token captured before and posted after a forced recreate.
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
- Cleared the ABP client warning caused by an unloaded NexStock localization resource by adding the culture-aware application-localization script to the shared layout. Hardened DataTable row-action visibility callbacks in invoices, offers, and orders so the ABP callback can pass either its wrapper or a raw record without a JavaScript exception.
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
- A fresh Docker restore reports known dependency advisories for transitive `MessagePack`, `Microsoft.OpenApi`, and `Scriban` packages. Dependency update and compatibility verification is a production-priority maintenance task before expanding the live invoice surface.

## Decision History

- 2026-07-10: ASP.NET Core + Razor Pages + ABP confirmed as final product direction.
- 2026-07-10: Legacy React/Node implementation confirmed as reference only.
- 2026-07-10: BookStore tutorial source artifacts removed.
- 2026-07-10: NexStock module permission system introduced.
- 2026-07-10: Next phases fixed around commercial workflow correctness before AI or polish.
- 2026-07-10: Phase 1 backend prototype slice added for accounts, stock movements, dashboard summary, and document approval/cancellation.
- 2026-07-10: Cancellation policy confirmed as reversal-based commercial history preservation.
- 2026-07-10: Phase 1 completed successfully with UI wiring and Purchase Invoice tests. Focus shifted to Phase 2.
- 2026-07-13: Shared database/shared schema confirmed as the initial SaaS model; multi-tenancy security hardening started before Branch/Warehouse expansion.
- 2026-07-13: `/t/{tenantName}` selected for development resolution and `{tenant}.nexstock.com` selected for production.
