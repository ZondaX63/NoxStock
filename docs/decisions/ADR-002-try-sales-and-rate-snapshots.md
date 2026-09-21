# ADR-002: TRY-only approved sales and document rate snapshots

## Status

Accepted.

## Context

Current market rates must influence new dynamic quotes without changing prior
commercial outcomes. The workflow rejects non-TRY sales at approval and
persists document/line snapshots.

## Decision

Approve sales documents in TRY only. Capture document exchange-rate snapshots
for purchase/cost processing and line-level price/cost/markup/rate snapshots
for sales.

## Alternatives

- Store only a current exchange rate: rejected because historical totals and
  margins would change.
- Permit foreign-currency sales without an expanded accounting model: rejected.

## Consequences

New sales-currency support requires an explicit design for documents, accounts,
payments, reporting and historical FX treatment; it is not a UI-only change.
