# High-volume query fixes

## Goal

Remove measured and source-observed unbounded/N+1 paths, then choose PostgreSQL
indexes from fresh plans.

## Current state

The isolated performance environment and P1.0 baseline exist, but the baseline
predates later application changes. Source still contains all-product inventory
summary work, unbounded partner statements and line-oriented repository calls.

## Affected domains

Products/OEM search, POS, document workflow, partner statements, reporting,
dashboard, EF mappings and performance Compose/scripts.

## Invariants

- Tenant filters always remain server-side.
- Do not alter pricing, stock, financial totals or sort semantics accidentally.
- Bound API payloads and preserve deterministic paging/filter contracts.
- Add indexes only after current PostgreSQL plan evidence.

## Acceptance and validation

- Refresh target-volume baseline before/after each coherent improvement.
- Add query-count and result-bound tests where practical.
- Capture `EXPLAIN (ANALYZE, BUFFERS)` for changed query shapes.
