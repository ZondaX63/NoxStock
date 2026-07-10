# System Patterns

Last updated: 2026-07-10

## Architecture

NexStock uses the ABP layered monolith structure:

- `NexStock.Domain`: entities, domain services, domain constants, settings, data seed/migration services
- `NexStock.Domain.Shared`: shared enums, localization resource, multi-tenancy constants
- `NexStock.Application.Contracts`: DTOs, app service interfaces, permission constants and definitions
- `NexStock.Application`: application services and object mapping
- `NexStock.EntityFrameworkCore`: DbContext, EF model configuration, migrations
- `NexStock.HttpApi`: HTTP API module
- `NexStock.Web`: Razor Pages UI, menu contributors, custom layout/sidebar/topbar
- `NexStock.DbMigrator`: migration runner and seed host

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

### Entity Configuration

EF configuration lives in `NexStockDbContext.OnModelCreating`.

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

- `src/NexStock.Application.Contracts/Permissions/NexStockPermissions.cs`
- `src/NexStock.Application.Contracts/Permissions/NexStockPermissionDefinitionProvider.cs`

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

- ABP menu contributor: `src/NexStock.Web/Menus/NexStockMenuContributor.cs`
- Custom sidebar: `src/NexStock.Web/Components/AppSidebar/Default.cshtml`

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

### Cancellation/Reversal

Current implemented path:

1. Check whether the operation is cancellable.
2. Create reversing stock movements.
3. Create reversing account movements.
4. Reverse business partner balance effects.
4. Mark source document/sale cancelled.
5. Preserve original records; do not delete commercial history.

Important invariant: original commercial movement rows remain in place. New rows with reversal types undo their effects.
