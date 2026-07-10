# NexStock Phase Roadmap

Last updated: 2026-07-10

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

Remaining deliverables:

- Account management UI connected to the new account service.
- Sales invoice create/detail/approve/cancel UI connected to workflow service.
- Purchase invoice create/detail/approve/cancel UI connected to workflow service.
- Tests proving purchase invoice approval increases stock and increases supplier payable.
- Business partner transaction/history views.
- Account transaction and stock movement list/detail views.

Exit criteria:

- No static zero dashboard values for implemented KPIs.
- Approval and cancellation are transactional.
- Stock/account/balance changes are auditable.
- A user can complete one sale invoice lifecycle through the web UI.

## Phase 2: Invoice and Current Account Workflow

Goal: make invoices usable for real customer/supplier accounting.

Deliverables:

- Sales invoice create/edit/detail.
- Purchase invoice create/edit/detail.
- Payment and collection modals.
- Partial payment logic.
- Due-date and overdue views.
- Customer/supplier detail with invoices and balance history.

Exit criteria:

- A user can create, approve, partially collect/pay, and cancel invoices without inconsistent balances.

## Phase 3: POS

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

## Phase 4: Offers and Orders

Goal: connect pre-sale/pre-purchase workflows to invoices.

Deliverables:

- Offer create/edit/detail.
- Offer to order/invoice conversion.
- Sales order and purchase order workflows.
- Order to invoice conversion.
- Status transitions.

Exit criteria:

- Converted documents preserve source linkage and totals.

## Phase 5: Reporting

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

## Phase 6: Settings and Printing

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

## Phase 7: Notifications, Audit Views, AI

Goal: add intelligence after data quality exists.

Deliverables:

- Critical stock notifications.
- Due-date notifications.
- Commercial audit views.
- AI query/explanation assistant for reports and product/customer summaries.

Exit criteria:

- AI features cite internal data and never mutate commercial records without explicit user confirmation.
