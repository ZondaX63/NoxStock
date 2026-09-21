# Persistence and migrations

NoxStock uses one ABP EF Core DbContext with PostgreSQL in production. EF
mappings define decimal precision, tenant-aware indexes, filtered uniqueness and
relationships for custom and ABP-module tables.

## Migration contract

1. Change the model/configuration deliberately.
2. Generate a new migration using the EntityFrameworkCore project and DbMigrator
   startup project.
3. Review `Up`, `Down`, designer and model snapshot; preserve applied history.
4. Build and run focused tests; use DbMigrator for an actual database update.

Use the serial commands in root `AGENTS.md`. Operational migration guidance is
in `docs/operations/migration-safety.md`; this document is the design contract.

## Query and concurrency contract

Use tenant-scoped, bounded projections; avoid unbounded histories, loading whole
tables and per-row database calls. Capture PostgreSQL plans in the isolated
performance environment before creating indexes. SQLite integration tests do
not prove PostgreSQL transactions, filtered-index semantics, locking or plan
quality.
