# ADR-001: Weighted-average TRY inventory cost

## Status

Accepted.

## Context

Product pricing needs a stable cost basis after purchases in TRY, USD and EUR.
The application now persists approved purchase cost history and the product
holds `AverageInventoryCostTry`.

## Decision

Use moving weighted-average TRY unit cost for inventory cost updates from
approved purchase documents. Persist source/rate/cost history and use reversal
history when an approved purchase is cancelled.

## Alternatives

- FIFO: not implemented and would require lot consumption/valuation rules.
- Last purchase cost: retained as profile context, not the inventory valuation
  authority.

## Consequences

Future pricing/margin work must use this established cost policy or introduce a
separate, explicit accounting-policy migration. Historical purchase evidence is
not recomputed from live rates.
