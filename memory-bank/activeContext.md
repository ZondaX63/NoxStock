# Active Context

Last updated: 2026-07-10

## Current Work Focus

The current focus is advancing to Phase 2: Invoice, Payment, and Current Account Workflows. Phase 1 (Commercial Foundation) is complete, meaning the basic ledger, account history, stock movement, and initial UI wiring are in place. Now we need to implement robust, multi-item invoice creation flows and advanced payment handling (partial payments, due dates).

## Recent Changes

- Removed Book/Author source modules:
  - Domain entities and shared constants/enums
  - Application services and cache items
  - Application.Contracts DTOs/interfaces
  - Web Razor Pages and scripts
  - Book application tests
- Removed `BookStoreDataSeederContributor`.
- Removed ABP getting-started home page and unused getting-started image assets.
- Root `/` now redirects to `/Dashboard`.
- Added real NexStock permission constants and definitions.
- Connected Products, Brands, Categories, BusinessPartners, and Documents app services to ABP policy names.
- Updated ABP menu contributor to use NexStock commercial modules.
- Updated localization keys for NexStock modules and permissions.
- Added migration `20260710113202_Add_CommercialCore_Remove_BookStore` to drop `AppBooks` and `AppAuthors`.
- Restored migration `20260623195606_Add_BusinessPartner_Document` after EF tooling removed it during migration cleanup.
- Updated Web test for root-to-dashboard redirect.
- Added `Account`, `AccountTransaction`, and `StockMovement` domain models.
- Added account, account-transaction, stock-movement, document-workflow, and dashboard DTO/contracts.
- Added `AccountAppService` with explicit create/update handling so tenant/default values are controlled by the service.
- Added `DocumentWorkflowAppService` for sale/purchase invoice approval and cancellation.
- Document approval now:
  - validates status/type/items/payment amount
  - updates stock for stock-tracked products
  - updates customer/supplier balance
  - records cash/bank account movement when a payment account is supplied
  - sets status to `Approved` or `Paid`
- Document cancellation now preserves original records and creates reversal stock/account movements.
- `DocumentAppService` now creates/updates document items explicitly and recalculates totals from lines.
- Dashboard page now reads `IDashboardAppService` instead of static zero values.
- Phase 1 UI integrations completed: Account UI, Purchase/Sale invoice UI, and historical movement views (Customer Detail, Product Movements, Account Transactions).
- EF workflow tests prove sale and purchase invoice approval/cancellation update stock, customer balance, account balance, and ledger rows correctly.

## Verification Status

- `dotnet build src/NexStock.Web/NexStock.Web.csproj --no-restore -v:minimal -m:1` passes.
- `dotnet build src/NexStock.DbMigrator/NexStock.DbMigrator.csproj --no-restore -v:minimal -m:1` passes.
- `dotnet test NexStock.slnx --no-build -m:1` passes when run outside the sandbox because VSTest needs local socket access.
- Test output notes that Application, Domain, and TestBase assemblies contain no tests. This is currently informational.
- Local PostgreSQL at `localhost:5433` currently returns `no response`, so the web app was not started in this pass.

## Active Decisions

- The C# ABP project is the final product direction.
- The old React/Node project is reference material only.
- BookStore tutorial source code should not return.
- Existing historical migration designer files may still mention Book/Author because they represent old migration snapshots.
- New schema changes must be done through EF Core migrations.
- Build commands should use `-m:1` in this environment to avoid silent MSBuild project-reference failures.
- Running `dotnet test` may require escalation/outside-sandbox execution because VSTest opens a local socket.
- Commercial history must be reversed, not deleted: cancellations create reversal stock/account rows and mark the source document cancelled.
- Account opening balance is captured on account creation. Later balance edits should be implemented as explicit ledger operations, not silent CRUD changes.
- `DocumentType.SaleInvoice` is the canonical enum name; stock movement uses `StockMovementType.SalesInvoice`.

## Immediate Next Steps

1. **Dashboard UI:** `Index.cshtml` (ana sayfa) üzerinde kartlar (ciro, tahsilat, bakiye vb.) tasarlanması.
2. **Grafikler:** Chart.js veya benzeri bir kütüphane ile en çok satan ürünler grafiğinin eklenmesi.
3. **Kritik Stoklar:** Stok seviyesi `CriticalStockLevel` altına inen ürünlerin bir tabloda uyarılması.
4. **DashboardAppService:** UI'ın ihtiyacı olan özet verileri tek bir endpoint'ten dönecek olan Application Service'in yazılması.
5. Tighten DTO/page-model nullable warnings where they affect real user input and mapping reliability.
