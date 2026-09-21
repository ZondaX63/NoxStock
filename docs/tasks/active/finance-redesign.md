# Finance redesign boundary

## Goal

Evolve the operational balance model safely where payment allocation, advances,
returns, FX transfers/revaluation or advanced current-account reporting becomes
a product requirement.

## Current state

NoxStock has transactional materialized balances, account transactions,
openings, reversals and document snapshots. It does not have a complete
open-item/allocation or multi-currency accounting ledger. Detailed discovery is
in `docs/finance-and-current-account-audit.md` and `TODO_FINANCE_REDESIGN.md`.

## Invariants

- Never invent historical allocations or exchange-rate effects in migration.
- Preserve source links, tenant isolation, reversals and audit history.
- Keep document exchange-rate/pricing snapshots immutable.
- Separate cash movement from accrual/revenue reporting.

## Constraints

Decide the accounting policy and user workflow before schema changes. Deliver in
small reversible migrations; do not combine this work with warehouse or broad UI
rewrites.

## Acceptance and validation

- Explicit source-link/status/reversal model.
- PostgreSQL rollback/concurrency tests.
- Migration reconciliation report for legacy/unmatched records.
- Currency-separated reports unless a documented conversion policy is added.
