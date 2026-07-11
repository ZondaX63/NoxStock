# Testing Strategy

Last updated: 2026-07-11

## Current Test Status

Current command:

```bash
dotnet test NexStock.slnx --no-build -m:1
```

Status on 2026-07-11:

- EntityFrameworkCore tests: 12 passed
- Web tests: 1 passed
- Application, Domain, and TestBase assemblies report no available tests

In this environment VSTest requires local socket access, so tests may need approval/escalation.

Frontend interaction verification on 2026-07-11:

- Static button-to-handler audit found no unbound visible command buttons in implemented workflows.
- `node --check` passes for changed Products, Print Labels, Current Account, and Stock Movements scripts.
- `dotnet build NexStock.slnx -m:1 --no-restore` passes (one pre-existing Web test entry-point warning).

Latest focused prototype test:

- `DocumentWorkflowAppServiceTests.Should_Approve_And_Cancel_Sales_Invoice_With_Ledger_Effects`
- Covers partial and closing sale collection, invoice payment history, and cancellation effects on stock, customer balance, account balance, stock movement rows, and account transaction rows.
- `DashboardAppServiceTests.Should_List_Only_Open_Overdue_And_Due_Soon_Invoices`
- Covers the due-date dashboard query and excludes paid invoices from open-item monitoring.
- `DocumentWorkflowAppServiceTests.Should_Approve_And_Cancel_Sales_Invoice_With_Ledger_Effects`
- Also verifies that current-account payment history returns the invoice's collection entries and account name.
- `DocumentWorkflowAppServiceTests.Should_Approve_And_Cancel_Purchase_Invoice_With_Ledger_Effects`
- Covers partial and closing purchase payment plus reversal of all linked payment movements on cancellation.
- `PosSaleAppServiceTests.Should_Create_And_Cancel_Cash_Pos_Sale_With_Stock_And_Account_Effects`
- Covers barcode lookup, POS stock decrease, cash collection, recent-sale visibility, and reversal-based cancellation.
- `PosSaleAppServiceTests.Should_Create_And_Cancel_Customer_Credit_Pos_Sale_With_Current_Account_Effects`
- Covers customer-credit POS sale, current-account balance update, and cancellation reversal.
- `CommercialDocumentWorkflowAppServiceTests.Should_Convert_Offer_To_Sales_Order_And_Then_Invoice_Preserving_Source_And_Totals`
- Covers offer send, offer-to-order conversion, order-to-invoice conversion, source IDs, totals, copied items, and completed source states.
- `CommercialDocumentWorkflowAppServiceTests.Should_Convert_Purchase_Order_To_Purchase_Invoice_Preserving_Source_And_Items`
- Covers purchase order conversion into a linked purchase invoice.
- `CompanySettingsAppServiceTests.Should_Save_Operational_Defaults_And_Expose_Default_Pos_Accounts`
- Covers persisted commercial defaults and POS default cash/bank account exposure.

## Testing Priorities

### Unit/Domain Tests

Add tests for:

- document status transitions
- stock movement sign rules
- account transaction sign rules
- cancellation/reversal invariants
- credit limit checks when implemented

### Application Tests

Add tests for:

- purchase invoice approval
- collection/payment
- partial payment
- POS sale
- POS cancellation
- offer/order conversion

### EF Tests

Add tests for:

- relationships and cascade behavior
- query filters for tenant and soft delete
- document with item persistence
- additional stock movement and account transaction persistence cases

### Web Tests

Keep web tests smoke-level unless a page has critical server-rendered behavior.

Current web test:

- `/` redirects to `/Dashboard`

## Test Exit Criteria Per Phase

Phase work is not complete until:

- relevant build passes
- migration compiles
- new business rules have focused tests
- cancellation/reversal cases are tested for operations that mutate stock or money
