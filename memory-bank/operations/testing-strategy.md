# Testing Strategy

Last updated: 2026-07-10

## Current Test Status

Current command:

```bash
dotnet test NexStock.slnx --no-build -m:1
```

Status on 2026-07-10:

- EntityFrameworkCore tests: 4 passed
- Web tests: 1 passed
- Application, Domain, and TestBase assemblies report no available tests

In this environment VSTest requires local socket access, so tests may need approval/escalation.

Latest focused prototype test:

- `DocumentWorkflowAppServiceTests.Should_Approve_And_Cancel_Sales_Invoice_With_Ledger_Effects`
- Covers sale invoice approval and cancellation effects on stock, customer balance, account balance, stock movement rows, and account transaction rows.

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
