# NexStock Project Brief

Last updated: 2026-07-10

## Purpose

NexStock is a commercial business management web application for small and medium-sized businesses. The product must bring inventory, customers, suppliers, invoices, orders, offers, POS sales, cash/bank accounts, basic accounting, reporting, personnel, roles, permissions, audit logs, settings, barcode/QR labels, currency rates, and later AI-assisted workflows into one coherent panel.

## Product Direction

The final product is the ASP.NET Core + Razor Pages + ABP Framework implementation under this solution. The older React + Node.js + Express + PostgreSQL + Sequelize implementation under `../A/` is only a reference for product vision, screens, data concepts, and workflows. It must not be copied blindly.

## Core Requirements

- Use ABP layered architecture: Domain, Application, Application.Contracts, EntityFrameworkCore, HttpApi, Web, DbMigrator.
- Use ABP Identity, Permissions, Audit Logging, Multi-Tenancy, Repositories, Application Services, and EF Core migrations.
- Avoid runtime table creation endpoints and raw-SQL-heavy temporary backend patterns.
- Model commercial actions as connected business operations, not isolated CRUD:
  - invoice approval changes stock and receivable/payable balance
  - collection/payment affects cash or bank account
  - POS sale decreases stock and affects account or customer balance
  - cancellation/reversal restores affected stock and financial balances
- Keep BookStore tutorial artifacts out of the product surface and source model.

## Current Product Modules

Implemented or started:

- Product
- Product OEM codes
- Brand
- Category
- Business partner
- Document
- Document item
- Document type
- Document status
- Razor Pages for dashboard, products, brands, categories, customers, suppliers, invoices, sales/POS, accounts, reports, settings, personnel, labels, offers, orders, and product movements

## Success Criteria

- The system can support day-to-day commercial operations from a single web panel.
- Business rules create consistent stock, account, and current-account movements.
- Permissions map to real NexStock modules, not tutorial entities.
- Migrations are the only schema evolution mechanism.
- Pages expose useful data, not static placeholder values.
- Tests cover high-risk business flows before each phase is considered done.

