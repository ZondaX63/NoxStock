# Memory Bank index

The memory bank is supplemental context, not the primary source of truth. Read
it after source, tests, schema/current configuration and `docs/architecture`.
For normal work, begin with root `AGENTS.md` and load only task-relevant files.

## Classification

| Location | Classification | Use |
|---|---|---|
| `projectbrief.md`, `productContext.md` | Permanent product context | Product intent and boundaries. |
| `techContext.md` | Current technical reference | Stack, verified commands and infrastructure map. |
| `inventoryArchitecture.md` | Future design reference | Read only for warehouse/branch work; it is not implemented architecture. |
| `features/business-partner-transfers.md`, `customer-sales-history.md`, `inventory-counts.md` | Module context | Read only for that module; source remains authoritative. |
| `features/enterprise-roadmap.md` | Product planning | Not an active engineering task list. |
| `activeContext.md` | Current state | Small index of active engineering work. |
| `archive/` | Historical record | Never treat as current architecture or verification status. |

Canonical architecture lives in `docs/architecture/`; operations are in
`docs/operations`, `docs/performance` and `docs/release`; accepted decisions
live in `docs/decisions/`; focused current work lives in `docs/tasks/active/`.
