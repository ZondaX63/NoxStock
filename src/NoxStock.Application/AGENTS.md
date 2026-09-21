# Application-layer guidance

Application services own use-case orchestration. Keep domain-specific rules in
domain entities/services where practical, but keep cross-aggregate posting,
authorization and persistence coordination here.

- Commercial mutations need explicit authorization, a tenant context and the
  `TenantLicenseWrite` boundary. Feature and limit checks belong at the actual
  operation boundary.
- Approval, payment, POS, inventory-count completion, transfer and cancellation
  are transactional workflows. Preserve all coupled stock, document, account
  and partner effects in one unit of work.
- Reuse existing operation-key/idempotency conventions. Do not add a retryable
  command that can post stock or money twice.
- Never turn cancellation into deletion: retain the original source and create
  the prescribed reversal/history records.
- Validate referenced entities through tenant-filtered repositories; a GUID is
  not authorization.
- Do not recalculate historical pricing/cost/rates from live exchange rates.
- Batch collection queries in line-oriented workflows; avoid repository calls
  inside loops unless the bounded size is intentional and tested.

Read `docs/architecture/commercial-posting.md` for posting work and
`docs/architecture/pricing-and-currency.md` for pricing work. Read the matching
task specification before changing known risk areas.
