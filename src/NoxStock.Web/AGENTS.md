# Web-layer guidance

The Web project is Razor Pages plus ABP conventional APIs. It presents and
protects workflows; it does not own commercial business rules.

- Razor pages and JS must call authorized application-service contracts. UI
  visibility, client validation and license badges are never enforcement.
- Preserve middleware order in `NoxStockWebModule`: tenant-path normalization
  precedes ABP multi-tenancy; activation, platform-route protection and license
  enforcement precede authorization.
- `/t/{tenant}` is the canonical tenant workspace route. Do not bypass path or
  tenant-claim checks, and keep host-only prefixes inaccessible to tenants.
- Preserve cookie/API 401/403 behavior, anti-forgery configuration, secure
  production cookies, OpenIddict production certificate requirements and trusted
  proxy validation.
- Keep public routes deliberately small. Add authorization tests when exposing a
  new page, conventional API or host administration route.
- Do not put secrets in appsettings, page scripts, diagnostics or browser tests.

Read `docs/architecture/tenancy-and-security.md` before changing auth, routes,
middleware, tenant UI or host administration.
