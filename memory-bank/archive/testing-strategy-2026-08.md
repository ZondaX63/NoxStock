> Historical record archived 2026-09-10. Do not use as current architecture, configuration, or verification status; consult AGENTS.md and canonical docs.

# Testing Strategy

Last updated: 2026-07-24

## Current Test Status

Current command:

```bash
dotnet test NoxStock.slnx -m:1
```

Licensing coverage added on 2026-07-24:

- Domain status/period invariants, suspension reason, reactivation, and terminal cancellation.
- Host-only service boundary, one-current-license invariant, entitlements, and audit history.
- Immediate write denial after host suspension using the real tenant-independent distributed-cache key.
- Host-only license permission and route tenancy sides.

Final licensing run:

- Domain tests: 8 passed
- EntityFrameworkCore tests: 58 passed
- Web tests: 48 passed
- Docker DbMigrator: exit code 0
- Web process/database health: Healthy
- Authenticated browser pass: pending

Status on 2026-07-13:

- EntityFrameworkCore tests: 31 passed
- Web tests: 12 passed
- Application and Domain assemblies report no available tests

In this environment VSTest requires local socket access, so tests may need approval/escalation.

Frontend interaction verification on 2026-07-11:

- Static button-to-handler audit found no unbound visible command buttons in implemented workflows.
- `node --check` passes for changed Products, Print Labels, Current Account, and Stock Movements scripts.
- `dotnet build NoxStock.slnx -m:1 --no-restore -v:minimal` passes with 0 warnings and 0 errors.
- A forced `-t:Rebuild` passes but reports 153 pre-existing nullable and Mapperly diagnostics.
- `dotnet test test/NoxStock.Web.Tests/NoxStock.Web.Tests.csproj -m:1 --no-build --no-restore -v:minimal` passed 12/12 after the host sidebar and ABP page-script layout changes.

Latest multi-tenant isolation verification:

- `TenantIsolationTests`: 14 tests covering Tenant A/Tenant B product/document/report/settings isolation, server-owned TenantId, host commercial-operation rejection, relation IDs, POS payment accounts, conversion sources, registration closure, global exchange rates, and isolated idempotent tenant seed.
- `PermissionSideTests`: commercial permissions are tenant-only and global exchange refresh is host-only.
- `NullTenantDataAuditTests`: null ownership is reported without mutating TenantId.
- Web tests cover trusted `/t/{tenantName}` parsing, resolver order, path rewriting, active login, inactive-tenant blocking, and closed public registration.

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
- `ReportingAppServiceTests.Should_Build_Operational_Snapshot_From_Commercial_Ledger`
- Covers date-filtered sales/purchase totals, VAT, account income/expense and cash flow, receivable/payable amounts, aging, critical stock, stock movement, and estimated product profitability.
- `OperationalAlertsAppServiceTests.Should_List_Critical_Stock_As_Operational_Alert`
- Covers live critical/out-of-stock alert generation from the product stock state.

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
