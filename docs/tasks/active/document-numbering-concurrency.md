# Document numbering concurrency

## Goal

Replace in-memory next-number derivation with an atomic tenant/type/year design.

## Current state

`DocumentNumberGenerator` loads matching document numbers, computes `max + 1`,
then relies on the unique document index at persistence time. Concurrent callers
can choose the same candidate.

## Affected domains

Documents, settings, EF Core migrations, PostgreSQL and commercial conversion.

## Relevant files

- `src/NoxStock.Application/Documents/DocumentNumberGenerator.cs`
- `src/NoxStock.Application/Documents/DocumentAppService.cs`
- `src/NoxStock.EntityFrameworkCore/EntityFrameworkCore/NoxStockDbContext.cs`

## Invariants and constraints

- Number scope remains tenant + document type + year/prefix behavior.
- Existing document numbers and applied migrations remain valid.
- Do not allocate a duplicate number under concurrent requests.
- Use a reversible EF/PostgreSQL migration; do not use a process-local lock.

## Acceptance and validation

- PostgreSQL concurrent test creates many numbers with no duplicates.
- Retry/unique-conflict behavior is deterministic and user-safe.
- Existing sequential numbering and document conversion tests pass.
