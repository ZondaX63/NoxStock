# SaaS Licensing

Last updated: 2026-07-24

## Status

The private-beta tenant licensing foundation is implemented. Licensing data is host-owned and is not `IMultiTenant`; tenant users can only read their own effective access projection. Host management is protected by host-only ABP permissions, explicit host-context checks, and host-route middleware.

## Domain Model

- `LicensePlan`
- `TenantLicense`
- `TenantLicenseFeature`
- `TenantLicenseLimit`
- `TenantLicenseAuditLog`

The first plans are `PILOT` and `CUSTOM`. A filtered unique index allows only one current license per tenant. License references are globally unique. Statuses are Trial, Active, GracePeriod, PastDue, Suspended, Expired, and Cancelled. Cancelled licenses are terminal and no longer current.

Migration: `20260724154448_Add_SaaS_Licensing`

## Enforcement

- Trial, Active, and GracePeriod permit commercial writes.
- PastDue, Suspended, Expired, Cancelled, and missing licenses are read-only.
- Unsafe tenant HTTP requests are blocked by `TenantLicenseEnforcementMiddleware`.
- Application-service mutations are marked with `TenantLicenseWriteAttribute` and intercepted independently of the UI.
- POS, dynamic pricing, and multi-currency operations check feature entitlements in the application layer.
- Product creation enforces `MaxProducts`.
- Error codes are stable: `LICENSE_REQUIRED`, `LICENSE_EXPIRED`, `LICENSE_SUSPENDED`, `LICENSE_CANCELLED`, `LICENSE_PAST_DUE_READ_ONLY`, `LICENSE_LIMIT_REACHED`, and `FEATURE_NOT_INCLUDED`.
- The two-minute access cache ignores current-tenant cache prefixes, so host suspension invalidates the same cache entry used by tenant requests. Restrictive states are written fail-closed before transaction completion and invalidated again after commit.

## Lifecycle And Audit

- Assign, plan change, extension, suspension, reactivation, cancellation, and automatic status changes produce audit rows.
- An hourly hosted service moves eligible licenses to GracePeriod or Expired using server UTC time.
- 14/7/1-day expiry notice events are idempotently recorded in the license audit log.
- Existing tenants receive an idempotent PILOT trial during host data seed. Newly provisioned tenants receive the same default trial through host tenant creation.

## UI

- Host sidebar exposes `/LicenseManagement` only on the platform side.
- HostAdmin can assign a pilot, change plan, extend, switch to PastDue read-only, suspend, reactivate, and inspect audit history.
- Tenant users see only a read-only plan/status indicator in the topbar.
- New license UI text is localized in TR/EN and supports dark mode.

## Deliberately Pending

- `MaxUsers` must be enforced in the host user-provisioning path before it is treated as an active limit. `MaxBranches` remains data-only until Branch exists.
- CUSTOM per-feature/per-limit editing is not exposed yet; CUSTOM currently receives all features with unlimited limits.
- Expiry notice events exist, but tenant-facing notification delivery/email is not implemented.
- `MaxUsers` and CUSTOM entitlement editing remain release follow-ups; an authenticated host/tenant browser pass is still required before production deployment.

## Verified

- Domain transition tests include UTC grace/expiry, suspension reason, reactivation, invalid periods, and terminal cancellation.
- EF integration covers assignment, single-current-license protection, audit, host/tenant boundary, PILOT entitlements, and immediate write denial after host suspension.
- Host-only routes and permission tenancy sides are covered by Web/EF tests.
- Final automated verification passes: Domain 8/8, EntityFrameworkCore 58/58, and Web 48/48.
- Docker DbMigrator applied `20260724154448_Add_SaaS_Licensing` to local PostgreSQL and exited with code 0. PILOT/CUSTOM plans and Trial licenses for both existing tenants were queried directly from PostgreSQL.
- The rebuilt Web container is running at `http://localhost:8080`; `/health-status` reports both process liveness and database readiness as Healthy.
