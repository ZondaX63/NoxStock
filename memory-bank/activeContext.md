# Active Context

Last updated: 2026-07-11

## Current Work Focus

Phase 1 (Commercial Foundation), Phase 2 (Invoice and Current Account Workflow), Phase 3 (POS), Phase 4 (Offers and Orders), and Phase 6 (Settings and Printing) are complete. Phase 5 is intentionally deferred and is the next focus: operational reporting built from the now-reliable commercial ledger.

## Recent Changes

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

## Verification Status

- `dotnet build NexStock.slnx -m:1 --no-restore` passes (one pre-existing Web test entry-point warning).
- `dotnet test NexStock.slnx --no-build -m:1` passes when run outside the sandbox because VSTest needs local socket access.
- `CompanySettingsAppServiceTests.Should_Save_Operational_Defaults_And_Expose_Default_Pos_Accounts` passes.
- Test output notes that Application, Domain, and TestBase assemblies contain no tests. This is currently informational.
- Local PostgreSQL at `localhost:5433` currently returns `no response`, so the web app was not started in this pass.

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

## Immediate Next Steps

1. Build report query services for stock, cash/bank, receivable/payable, and income/expense visibility.
2. Implement critical stock, stock movement, debtor/creditor, and cash/bank report screens from application queries.
3. Add aging and product profitability reports after currency and cost assumptions are explicitly defined.
4. Add focused report-query tests against ledger and document data.
