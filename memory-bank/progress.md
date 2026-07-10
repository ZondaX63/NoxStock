# Progress

Last updated: 2026-07-10

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

## What's Left To Build

### Phase 2: Invoice, Payment, and Current Account Workflows

- Sales invoice creation and approval
- Purchase invoice creation and approval
- Collection and payment flows
- Partial payment support
- Due-date tracking
- Customer/supplier detail history

### Phase 3: POS

- Barcode/search product lookup
- Cart and payment flow
- Cash/bank/customer-credit sale handling
- POS sale cancellation
- POS reports integration

### Phase 4: Offer and Order Workflows

- Offer creation
- Offer to order/invoice conversion
- Sales and purchase orders
- Order to invoice conversion
- Order status tracking
- Later: partial delivery and stock reservation

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

### Phase 6: Settings, Labels, and Operational Polish

- Company settings
- Default currency and units
- VAT rates
- Document numbering templates
- Default accounts
- Barcode/QR label templates and printing
- Light/dark theme persistence

### Phase 7: Audit, Notifications, and AI

- Business notifications
- Audit log views for commercial actions
- AI assistant features only after reliable product/account/document data exists

## Known Issues

- Several DTO/page model properties currently produce nullable warnings in some builds.
- Some Razor Pages are UI skeletons and not connected to full business rules.
- Dashboard is service-backed, but charts are still placeholder-level and need real time-series/report data.
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
