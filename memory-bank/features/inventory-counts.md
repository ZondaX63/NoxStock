# Stock Take / Inventory Audit

Last updated: 2026-08-04

## Scope

Tenant-scoped physical stock counting is implemented at `/InventoryCounts`. The current inventory authority remains `Product.Quantity`; this feature does not introduce Warehouse/WarehouseStock or a valuation ledger.

## Workflow

- One active Draft or AwaitingApproval count is allowed per tenant.
- Barcode/SKU search is exact-first through the shared normalized product lookup. Repeated scans increase the existing line instead of creating duplicates.
- Draft lines snapshot product name, SKU, barcode, unit and system quantity; counted quantity and note remain editable while Draft.
- Quick-created products are stock-tracked, start at zero, use a generated SKU, and are marked `IsIncompleteProfile` for later completion on Products.
- Excel/CSV import accepts `SKU`, `Barkod`, and `Miktar`; duplicate source rows are accumulated. Import only changes the browser draft and never stock directly.
- **Yedeği indir** exports the last server-saved count state as a versioned JSON file. It is read-only: it neither saves browser edits nor changes stock/count records. The UI explicitly warns when unsaved browser changes are excluded.
- **Yedekten yükle** accepts only this versioned JSON backup and is available only while the count is an editable draft. It requires explicit confirmation, replaces all draft lines atomically and does not change product stock until completion. It resolves products by current tenant ID first, then SKU, then barcode; a product catalogue Excel export can therefore be imported before restoration when a database was recreated.
- Submit changes Draft to AwaitingApproval. A Create-authorized user can reopen it; only `InventoryCounts.Complete` can apply the result.

## Completion Invariants

- Explicit `ConfirmReset=true` is mandatory.
- Completion is a transactional application-service command.
- Every tracked product in the count is set to its physical counted quantity.
- Every tracked product omitted from the count is set to zero.
- Each changed product receives a `StockMovementType.InventoryCount` delta and post-movement balance.
- The delta is calculated from the product quantity at completion time, not only the earlier draft snapshot.
- Completed retries return the completed record without duplicating movements.
- Products with `TrackStock=false` are excluded and rejected.

## Persistence and Operations

- Tables: `AppInventoryCounts`, `AppInventoryCountItems`.
- Migration: `20260804150340_Add_Inventory_Count_Workflow`.
- Tenant operational reset deletes count lines before count headers and products.
- Permissions: `InventoryCounts`, `InventoryCounts.Create`, `InventoryCounts.Complete`.
- Apply the migration with DbMigrator before enabling the menu in production.
