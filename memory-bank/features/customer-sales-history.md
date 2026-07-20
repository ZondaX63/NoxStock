# Customer Sales History

Last updated: 2026-07-20

## Purpose

`Customer Sales History` is a tenant-scoped, read-only commercial ledger for reviewing what one customer bought in a selected period. It is not a payment history; collections and payments remain on the existing customer current-account screen.

## Route and Access

- Razor route: `/Customers/{customerId}/SalesHistory`
- Application service: `ICustomerSalesHistoryAppService.GetCustomerSalesHistoryAsync`
- Permission: existing tenant-only `NoxStock.BusinessPartners.Default`
- Host context is rejected by `CurrentTenant.RequireTenantId()`.
- A customer lookup is performed through the tenant-filtered repository. A customer from another tenant, a missing partner, or a supplier-only partner returns `EntityNotFoundException`; the Razor page renders that as 404.
- Customers list rows open the existing customer detail. Both the row action and customer detail provide a Sales History action.

## Included Commercial Data

- Included document types: `SaleInvoice`, `PosSale`.
- Included statuses: `Approved`, `PartiallyPaid`, `Paid`, `Completed`.
- Excluded: drafts, sent/rejected/cancelled documents, purchase invoices, offers, orders, collections and payments.
- Current domain model has no return-document type. `OnlyReturns` and `Return` filters are intentionally empty until a reversal/return document model is introduced; cancelled POS and invoices are excluded so their original positive lines do not distort net sales.

## Summary Rules

- Totals are calculated from `DocumentItem.Total`, never by adding document total and line total together.
- Amounts are grouped by document currency. TRY, USD and EUR are never silently added together.
- The summary includes currency-separated net sales and average document amount, total line quantity, distinct document count and top product by quantity.
- The header uses `BusinessPartner.Balance`, credit limit and available limit. The current model has no separate customer code, so TCKN/VKN is displayed as the available account code when populated.

## Query and Pagination Design

- The service projects `DocumentItem`, `Document`, `Product`, `Brand` and `Category` in one tenant-filtered EF query; it does not materialize all lines.
- Filters: preset/custom date range, type, product/SKU/barcode, document number, brand, category, currency and returns-only.
- Date ranges are bounded to 366 days. Page size is clamped to 1-100, with a 50-row UI page.
- Sort is fixed to newest date then document-item id, avoiding untrusted dynamic SQL sorting.
- Summary and chart aggregations execute as bounded database queries. The UI groups only the returned page. Chart points use daily database aggregates and combine at most 366 aggregate points into day/week/month buckets in memory.
- No migration or index was added: this is a read-only feature and the existing performance/index work remains the source of any measured index decision.

## UI

- Breadcrumb: Customers / customer / Sales History.
- Customer header, balance/credit snapshot, period cards, filters, currency-safe trend bars and dense grouped movement table.
- The table supports mobile horizontal scrolling, dark mode, TR/EN keys and a document detail link.
- The project has no shared chart dependency; the trend uses the established dashboard-style accessible CSS bars rather than adding a frontend package.

## Verification

- `CustomerSalesHistoryAppServiceTests`: approved invoice/POS inclusion, draft and cancellation exclusion, summary consistency, filters and non-overlapping pagination.
- `CustomerSalesHistoryTenantIsolationTests`: tenant A cannot read tenant B customer history; host context is rejected.
- Query-bound test: 110 approved documents, 25-row page, at most 16 SQL commands in the SQLite integration harness.
- Browser smoke scenario extends `private-beta.spec.js`: customer detail to sales history, period selection and invoice detail link.

## Known Limits and Next Step

- Return rows require an explicit return/credit-note document model; current cancellation is ledger reversal plus cancelled source status.
- Product search does not yet include OEM reference codes on this history screen.
- Recommended next increment: supplier-specific purchase history, followed by a paged current-account movement view.
