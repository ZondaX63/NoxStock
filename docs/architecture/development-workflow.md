# Development workflow and context strategy

## Context loading

Read root `AGENTS.md`, the nearest path-specific guide, one relevant active
task, and only the architecture/ADR documents needed for the change. Then read
the implementation and tests. The Memory Bank is an indexed supplement; archive
files are historical evidence only.

## Risk levels

| Level | Typical work | Required approach |
|---|---|---|
| Low | Copy, isolated presentation, local refactor | Read local guidance; build/test affected area. |
| Medium | CRUD, DTO, ordinary application/database work | Inspect contract, authorization, tests and migration impact. |
| High | Tenant/auth/license, money/stock, concurrency, transaction, migration, production infrastructure | Read relevant architecture/ADR/task, trace side effects, and validate the listed invariants. |

## High-risk checklist

- Tenant/host boundaries and authorization outcomes.
- License write/feature/limit enforcement.
- Historical pricing/currency semantics and stock/money coupled effects.
- Retry, idempotency, transaction and PostgreSQL concurrency behavior.
- Migration reversibility, data preservation and query/index plan impact.

## Validation map

Use [testing and validation](../operations/testing-and-validation.md) for test
commands and limitations, [migration safety](../operations/migration-safety.md)
for schema work, [performance](../performance/README.md) for PostgreSQL plan
work, and the release runbook for deployment changes.
