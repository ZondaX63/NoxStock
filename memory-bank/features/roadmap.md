# NoxStock Phase Roadmap

Last updated: 2026-07-11

## Phase 1: Commercial Foundation

Goal: establish reliable stock, account, current-account, and document state mechanics.

Completed backend deliverables:

- `Account` aggregate for cash, bank, credit card, personnel, and adjustment accounts.
- `AccountTransaction` entity for opening, income/expense, transfer, collection, payment, and reversal movement types.
- `StockMovement` entity for manual, adjustment, invoice, POS, return, and reversal movement types.
- Document approval and cancellation application service.
- Dashboard application service with real KPIs.
- Tests proving sale invoice approval decreases stock and increases customer receivable.
- Tests proving cancellation reverses stock and financial effects.

Completed UI/reporting additions:

- Dashboard KPIs backed by the dashboard application service.
- Six-month sales trend, critical-stock list, top-selling products, and recent document views.

Exit criteria:

- No static zero dashboard values for implemented KPIs.
- Approval and cancellation are transactional.
- Stock/account/balance changes are auditable.
- A user can complete one sale invoice lifecycle through the web UI.

## Phase 2: Invoice and Current Account Workflow

Status: Completed on 2026-07-11.

Goal: make invoices usable for real customer/supplier accounting.

Deliverables:

- Sales invoice create/edit/detail.
- Purchase invoice create/edit/detail.
- Invoice-level payment and collection modal.
- Partial payment logic and reversal coverage.
- Dashboard due-date and overdue view.
- Customer/supplier document view with due date and outstanding balance.
- Customer/supplier invoice-linked payment history with cash/bank and reversal visibility.

Exit criteria:

- A user can create, approve, partially collect/pay, and cancel invoices without inconsistent balances.

Verification completed:

- Sales and purchase invoices support partial and closing payments.
- Cancellation creates reversal records for every linked stock and account movement.
- Due-date monitoring and current-account payment history are available in the web UI.

## Phase 3: POS

Status: Completed on 2026-07-11.

Goal: support fast retail sales.

Deliverables:

- Barcode lookup.
- Product search.
- Cart operations.
- Cash/bank/customer credit payment handling.
- POS sale detail and cancellation.
- Recent sales list.

Exit criteria:

- POS sale affects stock and account/current-account balances correctly.
- Cancellation fully reverses effects.

Verification completed:

- Cash POS sale changes stock and the selected account; cancellation reverses both movements.
- Customer-credit POS sale changes stock and the customer balance; cancellation restores both.
- POS sales are shown in recent-sales UI and contribute to dashboard sales figures.

## Phase 4: Offers and Orders

Status: Completed on 2026-07-11.

Goal: connect pre-sale/pre-purchase workflows to invoices.

Deliverables:

- Offer create/edit/detail.
- Offer to order/invoice conversion.
- Sales order and purchase order workflows.
- Order to invoice conversion.
- Status transitions.

Exit criteria:

- Converted documents preserve source linkage and totals.

Verification completed:

- Source document ID, items, currency, totals, and due date are copied into each converted target document.
- Source offer/order becomes `Completed`; target document starts in `Draft`, so financial and stock effects only occur on normal invoice approval.
- Sales and purchase conversion paths are covered by focused tests.

## Phase 5: Reporting

Status: Completed on 2026-07-11.

Goal: give owners operational visibility.

Deliverables:

- Stock report.
- Critical stock report.
- Stock movement report.
- Debt/receivable report.
- Cash/bank report.
- Income/expense report.
- VAT report.
- Aging report.
- Product profitability report.

Exit criteria:

- Reports use application/query services and match source transactions.

Verification completed:

- The reports page filters a single operational snapshot by date range and reads through `ReportingAppService` rather than static page values.
- Sales/purchase document values use the exchange rate saved on the document when a non-TRY currency was selected.
- Focused EF coverage verifies sales/purchase totals, VAT, account cash flow, current-account amounts, aging, critical stock, stock movement, and estimated product profitability.

## Phase 6: Settings and Printing

Status: Completed on 2026-07-11.

Goal: make the product configurable.

Deliverables:

- Company profile settings.
- Default currency.
- Units.
- VAT rates.
- Numbering templates.
- Default accounts.
- Barcode/QR label printing.

Exit criteria:

- New documents and labels use configured defaults.

Verification completed:

- Company profile, defaults, numbering, and default accounts are tenant-aware ABP settings with server-side validation.
- New product cards inherit the configured unit/currency; document creation inherits currency, VAT rate, and type-specific prefixes.
- POS receipt prefix and compatible default cash/bank accounts are applied in checkout.
- Label printing supports product search, quantities, Code 128 barcode, QR, and print media styling.
- Light/dark preference persists using browser storage.

## Phase 7: Notifications and Audit Views

Status: Completed on 2026-07-11 (AI excluded).

Goal: add intelligence after data quality exists.

Deliverables:

- Critical stock notifications.
- Due-date notifications.
- Commercial audit views.
- AI query/explanation assistant is excluded from the current scope.

Exit criteria:

- Notifications and audit views can be traced back to immutable commercial source records.

Verification completed:

- Topbar notification count and preview are generated from current critical stock and open invoice data.
- Notifications page lists critical stock, out-of-stock, overdue, and upcoming due-date alerts.
- Audit-log page filters ABP audit records by date range, user, HTTP method, and exception state.
- Focused EF test verifies critical stock alert generation.

## Enterprise Roadmap After Phase 7

The remaining product work is tracked in [enterprise-roadmap.md](enterprise-roadmap.md). Its priority order is:

1. Product hardening and live browser verification.
2. Professional inventory core: warehouses, returns, reservations, batches, serials, and costing.
3. Complete accounting and Turkish tax/e-document workflows.
4. Advanced purchasing, sales, deliveries, returns, pricing, and approvals.
5. CRM/customer success and supplier performance.
6. Multi-branch, multi-currency, reporting intelligence, UX, integrations, security, and reliability.
7. AI remains deferred until the accounting and inventory ledgers are mature.
