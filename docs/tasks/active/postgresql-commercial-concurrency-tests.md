# PostgreSQL commercial concurrency tests

## Goal

Prove rollback and concurrency behavior for stock, account and partner postings
against the production database provider.

## Current state

EF integration tests use in-memory SQLite and disable UoW transactions. They
exercise workflow logic but cannot prove PostgreSQL transaction, filtered-index
or concurrent update behavior.

## Affected domains

Documents, POS, accounts, partners, stock movements, licensing and EF Core.

## Requirements

- Use an isolated PostgreSQL database/Compose profile.
- Test approval/cancellation/POS/payment/transfer retries and concurrent calls.
- Inject failures between coupled effects and assert full rollback.
- Assert tenant isolation and stable handling of unique/concurrency conflicts.

## Constraints

No production database, shared developer database or live credentials. Keep the
fixture deterministic and cleanup scoped to its own database/container.

## Acceptance and validation

- Tests run reproducibly in CI or an explicitly documented optional profile.
- Failed commercial operations leave no partial stock/money/partner effects.
- Concurrent operations do not silently lose balance/quantity updates.
