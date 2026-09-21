# Current State

Last reviewed: 2026-09-10

## Product state

NoxStock is a controlled private-beta layered ABP application. Core inventory,
commercial documents, POS, accounts/current accounts, licensing, tenant routing,
release/restore tooling and performance tooling are implemented. It is not a
claim of unrestricted production-scale readiness.

## Active engineering context

Read the focused task before implementation:

- `docs/tasks/active/document-numbering-concurrency.md`
- `docs/tasks/active/postgresql-commercial-concurrency-tests.md`
- `docs/tasks/active/high-volume-query-fixes.md`
- `docs/tasks/active/finance-redesign.md`

## Current blockers and risks

- PostgreSQL rollback/concurrency behavior is not proven by the SQLite harness.
- Document-number allocation is not safe under concurrent callers.
- Target-volume performance evidence must be recaptured after later query work.
- Finance scope beyond current materialized balances requires an explicit
  source-linked/open-item design.

## Stable constraints

- Operational entities are tenant-scoped; host-owned licensing and exchange
  rates are deliberate exceptions.
- Commercial posting preserves transactional coupled effects and reversal-based
  history.
- Approved document financial/pricing data is historical snapshot data.
- Weighted-average TRY inventory cost is implemented; FIFO is not the current
  policy.
- SQLite integration tests do not prove PostgreSQL rollback or concurrency.

## Read next

Start at root `AGENTS.md`, then use `docs/architecture/README.md`. Historical
implementation narratives and dated verification claims are in `archive/`.
