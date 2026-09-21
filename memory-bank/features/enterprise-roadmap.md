# NoxStock Enterprise Product Roadmap

Last updated: 2026-07-15

This roadmap defines the work remaining after the initial commercial prototype phases. The goal is to make NoxStock a serious Turkish-first inventory, accounting, customer management, purchasing, sales, and retail platform rather than a collection of CRUD pages.

## Product Quality Bar

Every module must meet these rules before it is considered complete:

- Important operations are transactional and reversible.
- Stock, current-account, cash/bank, and accounting effects are traceable to a source document.
- Currency, tax, rounding, date, tenant, branch, and warehouse context are explicit.
- Permissions are enforced on the server and reflected in the UI.
- List pages support search, filters, sorting, pagination, empty/loading/error states, and useful bulk actions.
- High-risk business rules have focused tests and at least one end-to-end browser flow.
- Reports reconcile to source ledgers and expose the assumptions behind estimates.
- No page presents placeholder values as if they were real business data.

## Priority 0: Product Hardening

Status: Active release gate

- Run a complete browser walkthrough with PostgreSQL and Docker.
- Audit every page for unhandled buttons, broken modals, missing loading states, duplicate submissions, and server-side validation gaps.
- Add a global error boundary and consistent Turkish error/validation messages.
- Add pagination and export for reports, audit logs, products, documents, current accounts, and movements.
- Add request correlation IDs, structured logs, health checks, and actionable startup diagnostics.
- Clean remaining nullable and Mapperly warnings where they can hide real defects.
- Add CI build, test, migration validation, and container smoke-test workflows.
- Add backup/restore documentation and a safe development seed dataset.
- Verify the live deployment with tenant login, authorization boundaries, currency refresh, critical commercial workflows, and rollback readiness.
- Centralized server and browser error capture is implemented; the remaining work is external alert routing, dashboarding, and production log retention/aggregation.
- Invoice integrity baseline is implemented: sequential tenant/type numbers, duplicate protection, server-side line/tax totals, lifecycle tamper prevention, payment-currency validation, and browser printing. Remaining invoice scope is reconciliation, tax variants, PDF/email/archive delivery, and end-to-end browser coverage.
- Product price transparency is implemented for invoice entry and POS: original purchase/sale currency values remain visible beside the effective sale price calculated with the current TCMB rate and product margin.

## Phase 8: Professional Inventory Core

Status: Planned

- Warehouse and multi-location model.
- Branch/store model with branch-level permissions.
- Inter-warehouse transfer workflow with dispatch, receipt, and cancellation states.
- Stock count and inventory adjustment workflow requiring reason and approval.
- Purchase return and sales return documents with full ledger reversal.
- Stock reservation for orders, POS carts, and pending shipments.
- Negative-stock policy configurable per tenant, warehouse, and role.
- Batch/lot tracking, expiry dates, FEFO support, and recall history.
- Serial number tracking and warranty history.
- Product variants, attributes, kits/bundles, and composite products.
- Unit conversion: piece, box, pallet, kilogram, liter, and custom units.
- Product images, documents, technical specifications, and multiple barcodes.
- FIFO inventory costing and warehouse valuation. Weighted-average TRY inventory
  cost and historical cost snapshots are implemented for current pricing; do
  not treat that as a complete accounting valuation/reporting model.
- Stock valuation by warehouse, currency, product, category, and valuation method.
- Reorder point, safety stock, lead time, supplier preference, and purchase suggestion engine.

## Phase 9: Complete Accounting and Tax

Status: Planned

- Chart of accounts and account groups.
- Double-entry journal engine linked to every commercial action.
- Accounting periods, period lock, closing, reopening, and year-end carry-forward.
- Opening balances with controlled import and approval.
- General ledger, journal entries, trial balance, balance sheet, and income statement.
- Cash-flow statement based on classified account movements.
- Customer/supplier sub-ledger reconciliation against the general ledger.
- Tax configuration by rate, exemption, withholding, and reverse charge/tevkifat.
- Turkish e-Invoice, e-Archive, e-Dispatch/e-İrsaliye provider abstraction.
- Tax reports and export formats required by the target accountant workflow.
- Bank statement import and reconciliation.
- Installments, payment plans, late fees, and collection reminders.
- Multi-currency realized/unrealized exchange difference postings.

## Phase 10: Advanced Purchasing and Sales

Status: Planned

- Purchase request, approval, RFQ, supplier comparison, and purchase order workflow.
- Supplier price lists, lead times, minimum order quantities, and preferred suppliers.
- Partial delivery/receipt and backorder handling.
- Sales quotation revision history, approval, expiry, and version comparison.
- Sales order reservation, delivery/shipment, partial delivery, and backorder handling.
- Sales and purchase return workflows with reason codes.
- Price lists by customer group, currency, branch, quantity tier, and date range.
- Campaigns, discounts, promotions, and margin floor controls.
- Customer credit limit, risk status, overdue blocking, and approval override.
- Payment terms, installment plans, and collection schedules.
- Delivery addresses, shipping methods, cargo integration abstraction, and delivery tracking.
- Document templates, print layouts, PDF generation, email sending, and signed document archive.

## Phase 11: CRM and Customer Success

Status: Planned

- Multiple contacts and contact roles per customer/supplier.
- Customer segments, tags, custom fields, and account owner.
- Activity timeline: calls, meetings, notes, tasks, reminders, and attachments.
- Sales opportunities, pipeline stages, probability, expected value, and win/loss reasons.
- Customer visit and follow-up planning.
- Customer-specific products, last purchase, buying frequency, and profitability.
- Supplier performance scorecards: price, lead time, fill rate, and return rate.
- Customer portal for quotes, orders, invoices, statements, and payment status.
- Email/SMS/WhatsApp notification provider abstraction with consent tracking.

## Phase 12: Multi-Tenant, Multi-Branch, and Currency Maturity

Status: Planned

- Explicit tenant, branch, warehouse, and user scope on all commercial queries.
- Branch-level numbering, price lists, cash accounts, and document templates.
- Historical exchange-rate snapshots per transaction and daily rate history.
- Currency-specific account balances and consolidated reporting.
- Configurable rounding, decimal precision, tax-inclusive/exclusive pricing, and locale rules.
- Tenant onboarding, subscription limits, feature flags, and usage metrics.
- Tenant data export, retention, archival, and deletion workflows.

## Phase 13: Reporting and Management Intelligence

Status: Planned

- Reconciled stock ledger and inventory valuation reports.
- Gross margin by document, product, customer, salesperson, branch, and period.
- Purchase price variance and supplier performance reports.
- Sales funnel, conversion, retention, and cohort reports.
- Cash forecast based on due invoices, payment plans, and recurring expenses.
- Budget vs actual income/expense reporting.
- ABC/XYZ inventory analysis and dead-stock reports.
- Reorder and demand trend analysis using explainable rules.
- Scheduled reports delivered by email.
- Saved report views, dashboard widgets, exports, and accountant-friendly formats.

## Phase 14: Workflow, UX, and Accessibility

Status: Planned

- Global search across products, documents, partners, accounts, and audit records.
- Saved filters, column preferences, density settings, and personal dashboards.
- Keyboard-first POS and document entry workflows.
- Bulk edit, bulk price update, bulk status actions, and safe bulk import.
- Excel/CSV import with preview, validation report, duplicate handling, and rollback.
- Responsive tablet/mobile workflows for stock counts, barcode scanning, and approvals.
- Accessibility review: keyboard navigation, focus states, contrast, labels, and screen-reader semantics.
- Turkish-first localization completed across system, validation, permissions, dates, money, and tax terminology.
- Contextual help and non-intrusive onboarding for complex workflows.

## Phase 15: Integrations and Platform

Status: Planned

- Versioned REST API and webhook subscriptions.
- API keys, OAuth clients, scopes, rate limiting, and integration audit logs.
- Payment gateway and virtual POS abstraction.
- Bank, e-Invoice, cargo, marketplace, accounting-office, and identity integrations.
- Email, SMS, WhatsApp, and push notification providers.
- Import/export connectors for common legacy stock and accounting software.
- Background job dashboard, retries, dead-letter handling, and idempotency keys.
- Docker production profile, reverse proxy, TLS, secrets management, and observability.

## Phase 16: Security, Compliance, and Reliability

Status: Planned

- Two-factor authentication and session/device management.
- Fine-grained approval permissions for discounts, cancellations, stock corrections, and payments.
- Separation of duties for sales, warehouse, finance, and admin roles.
- Immutable commercial audit history with before/after values and reason codes.
- KVKK-oriented consent, access, export, retention, and deletion processes.
- Encryption and secret management review.
- Automated backups, restore drills, migration rollback strategy, and disaster recovery runbook.
- Load testing for POS, product search, dashboard, reports, and audit queries.
- Security dependency scanning and vulnerability response process.

## Phase 17: AI (Deferred)

Status: Deferred by product decision

- AI may be reconsidered only after accounting, stock costing, permissions, audit, and reporting data are reliable.
- Any future assistant must be read-only by default, cite internal records, expose uncertainty, and require explicit confirmation before mutation.
