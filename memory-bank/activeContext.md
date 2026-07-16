# Active Context

Last updated: 2026-07-16

## Current Work Focus

The commercial product phases and SaaS multi-tenancy hardening Phases 0-11 are complete, and the current build has been deployed live. The next focus is production hardening and a live regression pass, followed by inventory warehouse/location foundations. AI features remain explicitly out of scope.

## Recent Changes

- Removed the public account-registration UI and its provisioning page model. A minimal route guard redirects legacy `/Account/Register` requests to login before ABP's default register page can render. Tenant/company provisioning remains exclusively in host-only Tenant Management. The host login now identifies platform management; tenant-path login identifies the company workspace. Neither login state contains a registration link or a dead password-reset link, and each directs users to the appropriate administrator for account access.
- Reworked the auth visual composition after viewport review: the login shell now fills the viewport instead of centering inside a narrow max-width frame, the brand panel uses a stable 42/58 split with readable heading width, and the form panel uses a controlled 29rem column with compact inputs, actions, and language control. Mobile collapses into a short brand header plus form area.
- Tenant login now shows the resolved company name in a visible context badge above the form, while host platform login remains unchanged. This makes `/t/{tenant}/Account/Login` unambiguous before credentials are entered.
- Moved the tenant workspace label out of the right-side login form. On tenant login it now appears on the left brand panel directly above `TICARI YONETIM PLATFORMU` as plain icon/text, without a surrounding box; the right form no longer contains the duplicate context chips.
- Replaced the tenant context building icon with the Turkish lira symbol `₺` to better match NexStock's commercial and currency-focused identity.
- Fixed dashboard currency rendering: .NET `ToString("C2")` was producing the generic `¤` symbol under the active culture. Dashboard money and compact trend values now use explicit Turkish lira `₺` output while dynamic USD/EUR client formatters remain currency-aware.
- Fixed ABP language switching: language links now pass both required `culture` and `uiCulture` parameters. Their return URL includes `PathBase`, preserving tenant paths such as `/t/asd/Dashboard` after TR/EN changes instead of falling back to a host route.
- Expanded the localization foundation beyond the language buttons: the shared sidebar, topbar, login brand copy, login placeholder, and tenant dashboard now read from NexStock `tr`/`en` resources. A Web regression test locks the ABP language-switch contract, including the culture cookie and requested return URL. Live Docker verification confirms `/t/asd/Abp/Languages/Switch?culture=en&uiCulture=en` redirects to `/t/asd/Account/Login` and renders English tenant-login content without a 500 response.
- Moved Audit Logs and Users & Roles into the host-only Platform Administration sidebar group alongside Companies. Audit Logs is now a host-side NexStock permission and service operation, seeded to the host `admin` role. Tenant requests to `/t/{tenant}/AuditLogs` and `/t/{tenant}/Identity/Users` are blocked by the platform-administration middleware with HTTP 403; Web tests cover both routes and live Docker verification confirms the guard while host `admin` receives HTTP 200 from `/AuditLogs`.
- Reorganized tenant navigation into visible sections: Workspace, Operations, and Customers & Organization. POS, invoices, offers, and orders are grouped under Commercial; inventory/catalog and finance remain separate groups; reports and notifications are direct operational links. This removes the former ambiguity that placed orders under business partners.
- Corrected the desktop shell density and topbar clipping: desktop root rem sizing is now 90% so the UI has the same comfortable density without browser zoom, while the topbar and sidebar brand header reserve a non-shrinking 64px box with overflow protection. Docker web was rebuilt without cache and the live container was verified to serve the new CSS.
- Added the supplied `favicon_io` brand assets to Web `wwwroot`: ICO and 16/32px PNG favicons, Apple touch icon, Android 192/512px icons, and a NexStock-branded web manifest. Both application and authentication layouts now publish the favicon, touch-icon, manifest, and theme-color metadata. Correct NexStock Docker verification returns HTTP 200 for the favicon and manifest.
- Replaced the old text-only `N` brand blocks with the supplied NexStock logo image in the sidebar brand area, the application topbar, and the authentication layout. The logo remains a stable contained image on a dark brand tile and is live-verified from Docker with HTTP 200.

- Completed the first shared frontend quality pass. `global-styles.css` now owns stable light/dark design tokens, compact commercial cards, readable forms, tables, DataTables pagination, modals, report surfaces, and browser error notices. It replaces the previous glass-card treatment, row-spaced tables, and light-only modal/input rules that caused visual drift.
- Reworked the shared application shell: responsive content gutters and a 1600px operational frame replace the undefined `max-w-8xl` wrapper and duplicated padding; theme preference is applied before paint to prevent a light-theme flash. The sidebar and topbar are denser and retain mobile behavior.
- Reworked the NexStock auth layout into a restrained, responsive commercial sign-in surface with a solid brand panel, accessible language switcher, and dark-mode-compatible inputs. Decorative animated gradients, blurred shapes, remote avatar images, and oversized glass-card composition were removed.
- Standardized responsive action headers on invoice, offer, order, customer, supplier, account, brand, category, personnel, product-movement, and retail-sales pages. Multi-action controls stack on narrow screens. Invoice line-grid presentation now inherits the shared dark-aware border/color system; report/page-specific duplicated form styles were removed.
- Local production-container verification after the frontend pass: `dotnet build NexStock.slnx -m:1 --no-restore -v:minimal` passed with 0 warnings and 0 errors. `docker compose build --quiet web` succeeded; normal `docker compose up -d` brought PostgreSQL healthy, DbMigrator to successful completion, and Web back to `http://localhost:8080`. `/health-status`, `/Account/Login`, and the new auth-layout assets returned HTTP 200.

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
- The physical NexStock login page now owns password sign-in and renders model validation errors directly. Tenant login requires `/t/{tenant-name}/Account/Login`; the existing `asd` tenant has `admin` / `asd@asd.asd` as its provisioned administrator identity and retains the password supplied during provisioning. Logout now posts to a physical page and redirects to the matching login route.
- The physical login page now resolves either a username or an e-mail address before calling the user overload of `PasswordSignInAsync`. The old string overload only searched usernames, so valid tenant administrator e-mail logins were incorrectly rejected. Failed lookup/sign-in outcomes are logged with tenant-safe diagnostic fields. Live verification of the `asd` administrator now redirects to `/t/asd/Dashboard` after a successful sign-in.
- Tenant sign-in now issues the authenticated user's ABP `tenantid` claim explicitly. Previously a valid `/t/{tenant}/Account/Login` sign-in could redirect to the tenant dashboard, but the dashboard saw a host user and redirected to tenant administration. Live verification confirms `asd` now receives `302 /t/asd/Dashboard`, then `200` with the NexStock dashboard.
- Docker Compose persists ASP.NET Core Data Protection keys in the `nexstock-data-protection` volume at `/root/.aspnet/DataProtection-Keys`. This fixes login-form antiforgery HTTP 400 failures after a Web container recreate. A token issued before a forced recreate was verified to sign in successfully afterwards.
- Fixed host/tenant identity-cookie collision: ASP.NET had emitted the host cookie at `/` and the tenant cookie at `/t/{tenantName}`, causing both cookies to be sent and the host `admin` identity to win. The application identity cookie is now always rooted at `/`; login entry clears authenticated and legacy path-scoped cookies through a redirect before rendering a fresh antiforgery form.
- Tenant topbar identity now shows the tenant administrator e-mail and tenant name, while host sessions show the host username and localized platform-management context. This removes the ambiguity caused by both host and old tenant seed users having the username `admin`.
- Tenant Management now uses physical NexStock pages rather than the ABP tenant-list API, which returned an empty list despite persisted tenant rows. The host page reads `ITenantRepository` directly and refreshes after a successful modal result.
- The NexStock onboarding modal collects a company login code, administrator username, email and password. It provisions through `ITenantManager` + `ITenantRepository` + `IDataSeeder`, explicitly sets the required `IsActive` extension value before insert, and then renames the seeded `admin` account. This fixes the former silent `The IsActive field is required` failure.
- Unexpected onboarding exceptions are logged and are returned into the modal validation summary; duplicate company codes return a user-facing error. Live Docker verification posted a company, received HTTP 204, and confirmed that it appeared in the company table. The isolated verification tenant was removed afterwards; the existing `asd` tenant was restored from its pre-existing soft-deleted state and is visible again.
- Tenant authentication and host/tenant administration separation are accepted as working by the product owner, and the current NexStock build has been deployed to a live environment. Treat multi-tenancy as a stabilized foundation; future changes must preserve host-only company management and tenant-only commercial workflows.
- Added the production error-observability baseline: centralized NexStock exception middleware logs unhandled server errors with trace ID, tenant, user, method, and path; browser-facing failures receive a branded error page or a JSON problem response without technical detail leakage.
- Added authenticated client-side error reporting for browser, promise, and jQuery AJAX failures. Reports are rate-limited per page, written to Serilog with tenant/user context, and paired with an accessible on-screen error notice. Live tenant smoke test returned HTTP 202 and produced a structured log entry.
- Serilog file output now rolls daily, retains 30 files, and caps each file at 10 MB. The current local Docker Web mapping is `http://localhost:8080`.
- Started the invoice-excellence phase with server-authoritative document creation: new documents are always Draft with zero paid balance, line totals/tax/discount totals are recalculated and rounded on the server, dates and partner type are validated, and only Draft documents can be deleted.
- Added sequential type-specific document numbers (`PREFIX-YYYY-000001`), duplicate-number checks, and migration `20260715174844_Add_Document_Number_Uniqueness` for a tenant/type/number unique index.
- Added payment-account/document-currency validation and a printable invoice page available from the invoice list. The new migration was applied idempotently through Docker DbMigrator; focused document workflow/integrity tests currently pass 10/10.
- Replaced the third-party exchange-rate V4 call with TCMB's official `kurlar/today.xml` source. The real refresh failure was an ABP DbContext lifecycle error after Unit of Work completion, not TCMB availability; the refresh now writes within one Unit of Work and returns the validated TCMB values without querying a disposed context. Live Docker verification logged USD/TRY `47.0098` and EUR/TRY `53.5755` from the 2026-07-14 TCMB bulletin.
- Invoice line pricing now displays product-card purchase and sale amounts with their original currencies; POS product cards and cart lines show the same purchase, original sale, and effective sale-price context.
- Invoice creation crash `0HNN2JM3FHH6C:000000B8` was traced to culture-sensitive decimal range metadata during Razor model validation (`0.0001` could not be parsed under Turkish culture). Item-level numeric checks remain server-authoritative in `DocumentAppService`; the fragile DTO range metadata is absent. The invoice create page now uses a responsive, fixed-width line grid: quantity, unit price, discount, VAT, and line total each retain readable space, with controlled horizontal scrolling on narrow screens.
- Follow-up invoice form hardening: `Invoice.Date` and `Invoice.DueDate` now explicitly render as `yyyy-MM-dd` for HTML date controls, preventing `dd.MM.yyyy` model-binding failures under the Turkish UI culture. The invoice item table no longer inherits the generic spaced table treatment; it has a dedicated percentage-based column model, matching header/cell padding, and top-aligned line totals/actions so product price guidance cannot visually shift VAT or discount inputs.
- Date-post fallback: the invoice create handler also normalizes raw form values before validation and accepts `yyyy-MM-dd`, `dd.MM.yyyy`, and `d.M.yyyy`, so localized browser date submissions cannot block document creation. Docker Desktop is the canonical local daemon/context (`desktop-linux`), with NexStock Web at `http://localhost:8080` and PostgreSQL exposed at `5439`. Do not use `docker --context default` for this project: it is a separate daemon and creates a duplicate stack with port conflicts.
- Client runtime cleanup: ABP v10 needs `Abp/ApplicationLocalizationScript?cultureName={current culture}` in addition to the application configuration script; without it `abp.localization.getResource("NexStock")` warns because the resource map is empty. The app layout now loads this script before page scripts. Invoice, offer, and order DataTable row-action visibility callbacks accept both ABP callback shapes (`data.record` and the row directly), preventing the `data.record is undefined` crash.

- Removed Book/Author source modules:
  - Domain entities and shared constants/enums
  - Application services and cache items
  - Application.Contracts DTOs/interfaces
  - Web Razor Pages and scripts
  - Book application tests
- Removed `BookStoreDataSeederContributor`.
- Removed ABP getting-started home page and unused getting-started image assets.
- Root `/` now redirects to `/Dashboard`.
- Added real NexStock permission constants and definitions.
- Connected Products, Brands, Categories, BusinessPartners, and Documents app services to ABP policy names.
- Updated ABP menu contributor to use NexStock commercial modules.
- Updated localization keys for NexStock modules and permissions.
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
- Theme persistence now uses the `nexstock.theme` preference while retaining compatibility with `darkMode`.
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

- `dotnet build NexStock.slnx -m:1 --no-restore -v:minimal` passes with 0 warnings and 0 errors in the final normal build.
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
- Development tenant resolution will use `/t/{tenantName}` and production will use `{tenant}.nexstock.com`.
- Commercial permissions and services are tenant-only; platform and tenant administration are host-only.
- `TenantId`, `BranchId`, and `WarehouseId` represent different ownership scopes and must not be reused for one another.

## Immediate Next Steps

0. Validate spare-parts OEM search in a tenant browser walkthrough with multiple brands sharing one OEM code; expand product interchange/reference-code capabilities if manufacturer catalog imports are added.
1. Perform a browser/device regression pass across tenant onboarding, login/logout, products, POS, invoices, offers/orders, reports, notifications, audit logs, modals, and both color themes; fix any page-specific overflow found in real viewport screenshots.
2. Continue invoice excellence: detail/edit view, payment reconciliation and allocation history, tax-inclusive/withholding scenarios, and true PDF/email/archive output.
3. Add remaining production safeguards: backups/restore drill, health alerts, secrets/TLS/reverse-proxy review, CI smoke checks, and alert routing.
4. Start Phase 8 with warehouses, locations, transfers, stock counts, returns, and historical inventory costing after invoice work is stable.
