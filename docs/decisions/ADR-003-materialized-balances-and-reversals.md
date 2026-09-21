# ADR-003: Materialized balances with reversal corrections

## Status

Accepted for the current operational model.

## Context

Products, accounts and business partners carry operational balance fields while
documents, stock movements and account transactions record source activity.

## Decision

Maintain balances transactionally in commercial workflows. Correct approved
operations with cancellation/reversal records rather than deleting source
history. Restrict cross-currency account transfers until their own model exists.

## Alternatives

- Immediate full double-entry/open-item ledger rewrite: deferred; it would
  expand current operational risk substantially.
- Mutable in-place correction with no reversal evidence: rejected.

## Consequences

Concurrency and rollback behavior need PostgreSQL proof. Payment allocation,
FX revaluation and advanced accounting require a separately designed model.
