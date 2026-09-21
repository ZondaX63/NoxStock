# EF Core and PostgreSQL guidance

`NoxStockDbContext` is the production persistence contract. Keep mappings,
migrations and the model snapshot aligned.

- Use EF migrations for schema evolution. Never modify an applied migration;
  create a new reversible migration and review generated `Up`, `Down`, designer
  and snapshot changes.
- Tenant-owned tables must preserve `IMultiTenant`, `TenantId` propagation and
  tenant-aware foreign-key/query assumptions. Do not disable ABP filters in
  ordinary product code.
- Encode cross-request correctness with database constraints when possible:
  scoped unique indexes, filtered indexes and appropriate foreign-key deletion
  behavior. Application pre-checks alone do not make concurrent writes safe.
- Design indexes from current PostgreSQL query plans and workload predicates,
  usually beginning with `TenantId` for operational tables. Avoid speculative
  indexes and client-side filtering.
- Money/rate/quantity precision is deliberately configured; preserve decimal
  column types and rounding semantics.
- SQLite workflow tests do not validate PostgreSQL partial-index, transaction,
  locking or query-plan behavior. Add a PostgreSQL validation path for those
  concerns.

See `docs/architecture/persistence-and-migrations.md` before persistence work.
