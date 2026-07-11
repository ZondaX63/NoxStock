# Progress

Last updated: 2026-07-11

## What Works

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
- **Phase 6: Settings and Printing Completed**:
  - Tenant-aware company profile and commercial defaults saved through ABP Setting Management
  - Default currency, unit, VAT rate, cash/bank accounts, document prefixes, POS prefix, and label format
  - New product and document defaults wired to saved settings
  - POS checkout uses compatible default cash/bank accounts
  - Barcode/QR label selection, preview, and printing screen
  - Persistent light/dark preference

## What's Left To Build

### Phase 5: Reporting

- Debt/receivable report
- Income/expense report
- Cash flow
- Cash/bank report
- Stock and critical stock reports
- Stock movement report
- Customer/supplier sales/purchase reports
- VAT report
- Aging report
- Product profitability report

### Phase 7: Audit, Notifications, and AI

- Business notifications
- Audit log views for commercial actions
- AI assistant features only after reliable product/account/document data exists

## Known Issues

- Several DTO/page model properties currently produce nullable warnings in some builds.
- Some Razor Pages are UI skeletons and not connected to full business rules.
- Dashboard is service-backed, but charts are still placeholder-level and need real time-series/report data.
- Dashboard's current trend is turnover-only; richer reporting still belongs to Phase 5.
- Application/Domain test projects currently contain no meaningful tests.
- Historical migration designer files still contain Book/Author snapshots, which is expected for migration history.
- Existing Mapperly warnings for audit/base properties are noisy but not blocking.

## Decision History

- 2026-07-10: ASP.NET Core + Razor Pages + ABP confirmed as final product direction.
- 2026-07-10: Legacy React/Node implementation confirmed as reference only.
- 2026-07-10: BookStore tutorial source artifacts removed.
- 2026-07-10: NexStock module permission system introduced.
- 2026-07-10: Next phases fixed around commercial workflow correctness before AI or polish.
- 2026-07-10: Phase 1 backend prototype slice added for accounts, stock movements, dashboard summary, and document approval/cancellation.
- 2026-07-10: Cancellation policy confirmed as reversal-based commercial history preservation.
- 2026-07-10: Phase 1 completed successfully with UI wiring and Purchase Invoice tests. Focus shifted to Phase 2.
