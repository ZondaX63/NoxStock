# Commercial posting

## Posting model

Documents are the commercial source. Approval can update document state,
product quantity, stock movements, partner balance, account balance and account
transactions. POS follows the same document/stock/account pattern. Cancellations
preserve sources and apply inverse stock/financial effects rather than deleting
history. Inventory count completion adjusts physical stock through movements.

The application layer coordinates these cross-aggregate effects in transactional
units of work. `Document.TransitionTo` defines allowed document transitions.
Operation keys provide idempotency for selected retryable commercial commands;
reuse that convention when adding similar commands.

## Invariants

- A tenant context and authorization are required.
- Referenced products, partners and accounts are tenant-filtered.
- A single posting either commits its coupled effects or rolls back.
- Historical commercial records are corrected by cancellation/reversal, not
  destructive mutation.
- Credit, payment and account-currency validations run before posting.

## Known boundary

Product, account and partner balances are materialized values. Current SQLite
tests verify workflow behavior but not PostgreSQL concurrency/rollback. Treat
new concurrent posting changes as high-risk; see `docs/tasks/active/`.
