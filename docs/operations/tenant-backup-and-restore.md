# Tenant backup, local restore V1 and new-tenant clone V2

Tenant backup is an application-level, tenant-scoped commercial data export. It
is not a PostgreSQL disaster-recovery backup and must not replace WAL archiving,
encrypted off-site database backups, restore drills, or infrastructure recovery.

## Export

Tenant administrators with `NoxStock.TenantBackups.Create` create a backup and
choose a package password of at least 12 characters. Export runs as an ABP
background job against a PostgreSQL repeatable-read snapshot. The status is
`Queued`, `Processing`, `Succeeded`, or `Failed`. Successful files expire after
seven days; download requires a five-minute, backup- and tenant-bound token.

The password is never written to the backup record or job arguments as plain
text. Job transport uses ASP.NET Core Data Protection. Operators must therefore
configure a shared, persistent Data Protection key ring for multi-instance
deployments and restrict access to that key ring.

## Format version 1

`.noxbackup` is a chunked AES-256-GCM envelope. Its key is derived from the user
password with PBKDF2-HMAC-SHA256, a random 128-bit salt, and 310,000 iterations.
The encrypted payload is ZIP. `manifest.json` records format/application/schema
versions, source tenant identity, modules, record counts, SHA-256 file hashes,
encryption metadata, and aggregate verification totals. Module payloads are
JSONL files.

The explicit whitelist contains tenant settings, catalog and pricing/cost
profile data, partners/opening balances/transfers, accounts, documents/items and
print/rate snapshots, stock/cost/count data, account transactions, payment
allocations, document sequences, linked-return source fields, and terminal
commercial commands. Identity credentials/tokens, host exchange rates, license
and billing data, secrets, backup metadata, and Processing commands are excluded.

## V1: local clone with preserved IDs

Restore endpoints are authorized for the host and additionally refuse to run
outside the `Development` environment. Use a clean development database and a
new tenant code. Validation decrypts the package and checks its format, every
hash/count, and exact EF migration version before any write.

Restore preserves the source tenant ID and all commercial entity IDs, creates a
new local tenant path, seeds the explicitly supplied local test administrator,
and imports contributors in FK order within one transaction. Existing tenant
IDs/names and non-empty targets are rejected. After import, product/partner/
document/account counts and quantity/balance/total invariants must match the
manifest or the transaction rolls back.

Operational flow:

1. Apply the current migrations to a clean local development database.
2. Sign in as the host administrator in `Development`.
3. Call validation with the `.noxbackup` file and its out-of-band password.
4. Restore with a new tenant path plus local admin email/password.
5. Sign in at `/t/{tenant}` and verify dashboard, stock, current-account and
   document totals before using the dataset for testing.

Use `RestoreMode.LocalClonePreserveIds`. Never restore a package into production
or over an existing tenant. V1 does not provide merge, incremental sync, user
credential transfer, or automatic conflict resolution.

## V2: new tenant clone with remapped IDs

Use `RestoreMode.NewTenantCloneRemapIds` from the host restore page and provide
a unique tenant code, display name, and credentials for a new local administrator.
The preview validates encryption, hashes, schema compatibility, target uniqueness,
module counts, and the source/target identities without writing data.

V2 creates a new `TenantId`. Each whitelisted commercial entity receives a new
primary key, and an explicit per-entity relationship map rewrites catalog,
partner, account, document/line/source-return, inventory/cost, stock movement,
payment allocation, completed-command, and sequence references. This is not a
generic "rewrite every Guid" operation. Nullable document-item/cost-history
cycles are imported in stages and fixed up only after both sides exist.

`DocumentNumberSequence.LastNumber` is preserved for the new tenant so its next
number continues after the cloned history. Historical money, rate, price, cost,
invoice identity, and print snapshot columns are copied unchanged; they are not
recalculated from live records. Tenant settings are copied from a safe allowlist,
and default account setting IDs are remapped to the cloned accounts.

ABP identity users, password hashes, tokens, sessions, host exchange rates,
licenses, secrets, and backup metadata are never cloned. The supplied admin is
seeded only after commercial import succeeds. Audit-user GUID columns are cleared
instead of being attached to unrelated users. Clone, admin seeding, invariant
verification, and all commercial writes share one transaction; failure removes
the new tenant and every imported row. The source tenant remains unchanged.

The host page creates a durable clone operation and returns its operation ID
without waiting for import. The host can read its stage (`Queued`, `Validating`,
`CreatingTenant`, `ImportingMasterData`, `ImportingCommercialData`, `Verifying`,
`Succeeded`, `Failed`, or a pre-write `Cancelled` state) and processed record
count. A target tenant is inactive until transaction commit and final invariant
verification succeed. A failed job never reuses a partial target; submit a new
operation with a new target code/name. The same operation key, or an active
source-backup/target-code/target-name request, returns the original operation.

Restore application-service calls are audited by ABP while package/admin
passwords are marked out of audit payloads. As with V1, the restore
boundary remains host-only and Development-only; moving packages into another
environment means running the destination's Development restore host, not enabling
restore against a production database.

Operational flow:

1. Export and download the encrypted `.noxbackup` from the source tenant.
2. Open `/TenantBackups` as a host administrator on the destination Development host.
3. Select a non-expired source backup and enter its password.
4. Enter a unique target tenant code/display name and a new local admin credential.
5. Preview module counts and compatibility, then create the tenant.
6. Sign in at `/t/{target-code}` and verify the displayed invariant totals.

## Server disaster recovery

Production operations still require PostgreSQL-native encrypted full backups,
WAL/PITR, blob-store backup, Data Protection key-ring backup, secret/configuration
recovery, retention/immutability policies, monitoring, and regularly evidenced
restore drills. A tenant export is a portability/testing artifact, not server DR.

## Retention and package limits

Successful export metadata and its tenant-scoped blob expire after seven days.
The hourly bounded cleanup worker removes expired successful/failed metadata only
after deleting the corresponding blob; a failed blob deletion leaves metadata for
a later retry. A deleted/expired backup cannot be used as a clone source, and its
normal download-token lookup no longer succeeds. Export failure after a blob was
stored performs an immediate best-effort delete in the same tenant blob context.

The current V1 package builder validates a 512 MB encrypted package limit. It
still constructs ZIP/JSONL content in memory, so this is an enforced safety
ceiling rather than streaming support; packages approaching the limit must be
split operationally until a separately designed streaming format is introduced.
Multi-instance hosts must configure `DataProtection:KeyRingPath` to a shared,
persistent protected location; production startup rejects a missing key-ring path.

## V3 deferred

Existing-tenant overwrite/restore, staging-and-swap, incremental sync,
bidirectional sync, conflict resolution, and automatic merge are explicitly not
implemented. They require a separate design and acceptance suite. Large-package
streaming without in-memory ZIP assembly remains a separate follow-up.

V3 snapshot refresh is intentionally deferred: destructive replacement of an
existing tenant requires a distinct durable operation, source binding,
maintenance admission control, contributor-owned clear/import ordering and a
PostgreSQL rollback proof. V2 new-tenant clone already provides the safe beta
workflow for refreshing a local test dataset: create a new clone instead of
overwriting the existing tenant.
