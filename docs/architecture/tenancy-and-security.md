# Tenancy and security

## Contract

Operational entities implement `IMultiTenant`; ABP repository filters isolate
tenant data. Host-owned exchange rates, license records/plans and ABP platform
administration intentionally do not use the operational tenant filter.

Tenant resolution accepts the authenticated user's tenant claim and the
`/t/{tenant-name}` path contributor. `TenantPathMiddleware` normalizes the
path before ABP multi-tenancy. `TenantActivationMiddleware` verifies a path
tenant against the claim, rejects inactive tenants and canonicalizes workspace
URLs. `PlatformAdministrationMiddleware` denies host routes to tenant contexts.

## Security boundaries

Application-service `[Authorize]` policies are the commercial authorization
boundary. `TenantLicenseEnforcementMiddleware` is an additional HTTP boundary;
`TenantLicenseWrite` interception protects marked service mutations. Razor UI
and JavaScript do not authorize access.

Tenant filters may be disabled only for reviewed host/login/audit operations.
Any new filter bypass must prove its tenant selection, authorization and tests.

## Change checklist

- Does the entity belong to a tenant or host?
- Is every route/API/policy correct for that side?
- Can host, tenant A, tenant B, anonymous and inactive sessions access it only
  as intended?
- Does the change preserve middleware order and API 401/403 behavior?
