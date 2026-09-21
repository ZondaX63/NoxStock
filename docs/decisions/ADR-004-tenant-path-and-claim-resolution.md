# ADR-004: Tenant URL plus authenticated tenant claim

## Status

Accepted.

## Context

Tenant workspaces need canonical URLs while authentication carries tenant
identity. Custom middleware already validates `/t/{tenant}` paths, tenant claims,
activation state and host-route boundaries.

## Decision

Resolve tenancy from the current user and canonical `/t/{tenant-name}` path.
Require path/claim agreement for authenticated tenant requests; keep host routes
outside tenant workspaces.

## Alternatives

- Subdomain-only resolution: not implemented.
- Claim-only resolution without workspace URLs: rejected because it weakens
  canonical navigation and host/tenant separation.

## Consequences

Middleware order and tenant-route regression tests are security-sensitive. New
host routes must be added deliberately to platform route protection.
