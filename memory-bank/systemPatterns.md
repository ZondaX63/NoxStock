# System Patterns

Last updated: 2026-07-20

## Architecture

NoxStock uses the ABP layered monolith structure:

- `NoxStock.Domain`: entities, domain services, domain constants, settings, data seed/migration services
- `NoxStock.Domain.Shared`: shared enums, localization resource, multi-tenancy constants
- `NoxStock.Application.Contracts`: DTOs, app service interfaces, permission constants and definitions
- `NoxStock.Application`: application services and object mapping
- `NoxStock.EntityFrameworkCore`: DbContext, EF model configuration, migrations
- `NoxStock.HttpApi`: HTTP API module
- `NoxStock.Web`: Razor Pages UI, menu contributors, custom layout/sidebar/topbar
- `NoxStock.DbMigrator`: migration runner and seed host

## Key Patterns

### ABP CRUD Services

Started modules use `CrudAppService<TEntity, TDto, Guid, TInput, TCreateUpdateDto>`. CRUD services should set:

- `GetPolicyName`
- `GetListPolicyName`
- `CreatePolicyName`
- `UpdatePolicyName`
- `DeletePolicyName`

This keeps API permissions aligned with role management and menus.

### Business Operation Services

Commercial behavior should move beyond basic CRUD. High-risk workflows should use explicit application/domain services:

- stock movement service
- account transaction service
- document workflow service
- POS sale service
- reversal/cancellation service

These services should coordinate repositories inside one unit of work.

High-risk command methods must declare a transactional unit of work explicitly. Client retries must carry an operation key, and a tenant-scoped database uniqueness rule must guard concurrent duplicate requests. Query-before-write checks improve normal retry behavior but do not replace the database constraint.

Document status changes in commercial workflows must use the `Document.TransitionTo` domain rule. Application services must not invent status paths or directly move a document backward.

### Entity Configuration

EF configuration lives in `NoxStockDbContext.OnModelCreating`.

Current commercial tables include:

- `AppAccounts`
- `AppAccountTransactions`
- `AppBrands`
- `AppCategories`
- `AppProducts`
- `AppProductOEMCodes`
- `AppBusinessPartners`
- `AppDocuments`
- `AppDocumentItems`
- `AppStockMovements`

### Permissions

Permissions are centralized in:

- `src/NoxStock.Application.Contracts/Permissions/NoxStockPermissions.cs`
- `src/NoxStock.Application.Contracts/Permissions/NoxStockPermissionDefinitionProvider.cs`

Current permission groups:

- Dashboard
- Products
- Brands
- Categories
- StockMovements
- BusinessPartners
- Accounts
- Documents
- Offers
- Orders
- Sales
- Reports
- Settings
- Personnel
- PrintLabels

### UI Navigation

Two navigation surfaces exist:

- ABP menu contributor: `src/NoxStock.Web/Menus/NoxStockMenuContributor.cs`
- Custom sidebar: `src/NoxStock.Web/Components/AppSidebar/Default.cshtml`

Keep both aligned when adding new modules.

## Critical Implementation Paths

### Invoice Approval

Current implemented path:

1. Load document with items.
2. Validate status is draft/sent and partner/product data exists.
3. For sale invoice, decrease stock for stock-tracked products.
4. For purchase invoice, increase stock.
5. Create stock movements tied to document and user.
6. Update business partner balance.
7. If payment is supplied, require an account and create an account transaction.
8. Set document status to approved or paid depending on payment state.
9. Save via ABP repository/application-service unit of work.

Current sign rules:

- Sale invoice stock movement quantity is negative.
- Purchase invoice stock movement quantity is positive.
- Sale invoice collection increases account balance.
- Purchase invoice payment decreases account balance.
- Sale invoice unpaid amount increases customer balance.
- Purchase invoice unpaid amount decreases supplier balance, representing payable position in the current model.

### POS Sale

Expected path:

1. Resolve products by barcode/search.
2. Calculate totals.
3. Create a sale document or POS transaction record.
4. Decrease stock.
5. If cash/bank payment, increase selected account.
6. If customer credit sale, increase customer receivable.
7. Persist audit trail.

### Customer Sales History

`CustomerSalesHistoryAppService` is a tenant-only read model, not a new ledger or aggregate. It reads customer-linked `SaleInvoice` and `PosSale` `DocumentItem` rows only after their documents reach `Approved`, `PartiallyPaid`, `Paid` or `Completed`.

- Use a projection joining document, item, product, brand and category; do not call document/product services per row.
- Customer lookup must remain tenant filtered and must validate `Customer` or `Both` partner type before the history query.
- Amount summaries must remain separated by stored document currency. Do not convert or add cross-currency values implicitly.
- Keep dates bounded to 366 days and rows bounded to a page cap of 100. Fixed newest-first order is `Date desc`, then `DocumentItemId desc`.
- Cancelled source documents are excluded. Current returns-only filters are empty until a return/credit-note document model exists; cancellation is represented by existing reversal ledger entries, not by rewriting a positive sales row.

### Cancellation/Reversal

Current implemented path:

1. Check whether the operation is cancellable.
2. Create reversing stock movements.
3. Create reversing account movements.
4. Reverse business partner balance effects.
4. Mark source document/sale cancelled.
5. Preserve original records; do not delete commercial history.

Important invariant: original commercial movement rows remain in place. New rows with reversal types undo their effects.

### Inventory Source Of Truth

`Product.Quantity` is a transitional single-location balance, not the long-term inventory source. The approved target is an append-only `StockMovement` ledger plus a `WarehouseStock` current-balance projection keyed by `(TenantId, WarehouseId, ProductId)`.

Warehouse entities are intentionally deferred until the staged backfill and reconciliation plan in `memory-bank/inventoryArchitecture.md` is implemented. Do not switch reads or remove `Product.Quantity` in the same release that introduces warehouse tables.
