# ADR-005: Host-owned license enforcement

## Status

Accepted.

## Context

Plans and licenses are platform records, while tenant operations must be gated
by status, feature and limit. The current implementation has host-only license
management, middleware enforcement and application-service interception.

## Decision

Keep license data host-owned. Enforce access at tenant HTTP and application
service write boundaries; check features/limits in the concrete gated use case.
Use a tenant-keyed access cache with invalidation on lifecycle changes.

## Alternatives

- UI-only entitlement checks: rejected.
- Tenant-owned license records: rejected because host management would require
  broad tenant-filter bypasses.

## Consequences

Every new mutation must opt into the write boundary, and each new product
feature/limit needs backend enforcement and negative tests.
