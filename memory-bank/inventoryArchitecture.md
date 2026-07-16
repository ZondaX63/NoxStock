# Inventory Architecture Decision

Last updated: 2026-07-16
Status: Design approved for staged implementation; warehouse entities are not implemented yet.

## Why This Change Is Needed

`Product.Quantity` is currently the operational stock source. This is sufficient for a single implicit location, but it cannot safely represent multiple branches, warehouses, reservations, transfers, or warehouse-specific availability. It must remain in place during migration so existing screens, reports, and migrations keep working, but it must not remain the long-term source of truth.

The target stock source is the combination of an append-only `StockMovement` ledger and the current per-warehouse `WarehouseStock` balance. `Product.Quantity` will temporarily act as a compatibility cache and will be removed only after backfill and reconciliation prove that warehouse totals are correct.

## Target Aggregate Model

### Branch

- Tenant-owned organizational boundary.
- Required fields: `TenantId`, `Code`, `Name`, `IsActive`.
- May contain address and commercial identity fields later.
- Unique business key: `(TenantId, Code)`.
- A branch groups warehouses but does not itself own a stock balance.

### Warehouse

- Tenant-owned physical or logical stock location.
- Required fields: `TenantId`, `BranchId`, `Code`, `Name`, `IsActive`.
- Unique business key: `(TenantId, Code)`.
- Every operational warehouse belongs to one branch. A tenant bootstrap process will create a default branch and warehouse before warehouse stock becomes authoritative.

### WarehouseStock

- Current balance projection for a product in one warehouse.
- Required identity: `TenantId`, `WarehouseId`, and `ProductId` together.
- Unique key: `(TenantId, WarehouseId, ProductId)`.
- Quantities: `OnHandQuantity`, `ReservedQuantity`; available stock is derived as `OnHandQuantity - ReservedQuantity`.
- Uses an optimistic concurrency token so simultaneous sale, transfer, and reservation operations cannot silently overwrite each other.
- Every referenced product and warehouse must belong to the same tenant. A matching GUID is never sufficient without tenant validation.

### StockMovement

- Remains append-only commercial history. Existing movement rows are never updated or deleted to change stock.
- Each warehouse ledger entry carries `TenantId`, `WarehouseId`, `ProductId`, signed quantity, operation date, movement type, source document/operation link, and actor/audit data.
- Corrections and cancellations create a new inverse movement linked by `ReversalOfMovementId` or an equivalent immutable correlation reference.
- A transfer creates two linked ledger entries in one transaction: negative at the source warehouse and positive at the destination warehouse.
- Reversal is permitted once per source movement; the original and reversing entries remain queryable.

### StockTransfer

- Tenant-owned aggregate with source warehouse, destination warehouse, lines, operation key, and status.
- Source and destination must differ and must belong to the same tenant.
- Proposed status path: `Draft -> Approved -> InTransit -> Completed`, with cancellation allowed only from explicitly defined states.
- Completion writes paired stock movements and updates both `WarehouseStock` rows in one transaction.
- Replaying the same operation key returns the original result and cannot apply stock twice.

### StockReservation

- Tenant-owned reservation tied to `WarehouseId`, `ProductId`, quantity, source document/line, operation key, and lifecycle status.
- Proposed statuses: `Active`, `Consumed`, `Released`, `Expired`.
- Active reservation increases `ReservedQuantity`; consumption decreases both reservation and available on-hand through the related commercial operation; release/expiry removes only the reservation.
- A source document line cannot create multiple active reservations for the same logical operation.

## Required Invariants

- `TenantId`, `WarehouseId`, and `ProductId` are evaluated together on every stock read and write.
- Cross-tenant branch, warehouse, product, transfer, or reservation references fail closed.
- High-risk commands run in explicit transactional units of work.
- Approval, payment, POS, transfer, reservation, and cancellation commands carry idempotency keys.
- Document status changes use domain transition rules, not arbitrary application-layer assignment.
- Cancellation creates inverse ledger entries; it does not erase history.
- Negative stock policy must be an explicit tenant setting. Until then, available stock cannot fall below zero.
- Reservation and transfer updates use optimistic concurrency and retry/conflict handling, never last-write-wins behavior.

## Migration Strategy

1. Introduce Branch, Warehouse, WarehouseStock, transfer, and reservation schema in a dedicated future phase without changing existing reads.
2. Seed one default branch and one default warehouse per tenant idempotently.
3. Backfill one `WarehouseStock` row per existing tenant/product using `Product.Quantity` in the default warehouse.
4. Reconcile `sum(WarehouseStock.OnHandQuantity)` against `Product.Quantity` and produce a tenant-scoped discrepancy report before enabling writes.
5. Temporarily dual-write `Product.Quantity`, `WarehouseStock`, and append-only movement rows in the same transaction, with consistency monitoring.
6. Move stock reads and reports to WarehouseStock totals, then stop treating `Product.Quantity` as authoritative.
7. Stop writing `Product.Quantity` only after production reconciliation remains clean. Remove the column in a later, separately reversible release.

No warehouse entity or migration was added during the 2026-07-16 stabilization pass.

## Stabilization Baseline

- Invoice approval, payment, cancellation, POS sale/cancellation, and commercial document conversion now declare transactional units of work.
- Document status changes in core workflows pass through domain transition rules.
- POS creation and invoice payment accept operation keys; unique tenant-scoped database indexes prevent concurrent duplicate persistence.
- Replayed operation keys return the previously completed logical result in sequential retry scenarios.
- Existing stock and account cancellation behavior remains reversal-based and append-only.
- Tenant isolation coverage includes products, invoices, POS, payments, stock effects, relation IDs, and reporting queries.

## Remaining Verification Risk

- The SQLite integration harness intentionally disables real database transactions, so it proves workflow effects, tenant isolation, idempotent replay, and status rules but not provider-level rollback after an injected mid-operation failure.
- A future PostgreSQL integration fixture should inject failures between ledger writes and aggregate updates to prove rollback behavior against the production provider.
- A concurrent duplicate request may currently receive a unique-constraint error even though duplicate effects are rolled back. A later API refinement should translate that race into the already-created operation result.
- Authenticated browser automation is not installed yet. Current tenant-path HTTP smoke checks and Web/integration tests should be expanded into Playwright end-to-end workflows.
