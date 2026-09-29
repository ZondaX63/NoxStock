# Tenant backup operational model

## Scope and safety boundary

Tenant backup is a tenant-scoped portability artifact.  Restore and new-tenant
clone remain **host-only and Development-only**.  Neither operation writes over
an existing tenant and neither is an infrastructure disaster-recovery mechanism.

## State machines

An export moves `Queued -> Processing -> Succeeded | Failed`.  A successful
export can subsequently be deleted by retention cleanup; an expired package is
not downloadable or cloneable.

A clone operation moves through:

`Queued -> Validating -> CreatingTenant -> ImportingMasterData ->
ImportingCommercialData -> Verifying -> Succeeded | Failed`.

`Cancelled` is permitted only while still `Queued`; it is not a way to
interrupt an import transaction.  Terminal states are immutable.  A failed
operation never makes its target reusable: its transaction rolls back the
tenant and all tenant-owned imports.

## Ownership, idempotency and retry

The host request persists a host-owned operation and uploads the encrypted
package to private backup storage before it queues a durable ABP background
job.  A database unique constraint on operation key (when supplied) and on
the active source-backup/target-code combination prevents duplicate target
creation.  A duplicate request returns the existing operation rather than
running a second import.

Jobs may retry only before a target import has committed.  A failed operation
is terminal and a user must submit a new operation key and target code.  The
source tenant is read only throughout.

## Failure handling and visibility

Progress updates are persisted outside the import transaction.  The target
tenant is created and activated only in the single import transaction, after
the package has been validated.  It is therefore not visible to normal tenant
traffic before invariant verification and final commit.  Failure codes are
safe, localized business codes; package passwords, operation keys, customer
data and financial values are never logged.

## Retention and package cleanup

Successful tenant exports retain their blob until `ExpiresAtUtc`.  Clone input
blobs are private, operation-bound, and deleted at terminal completion or
failure.  A bounded cleanup worker removes expired export blobs plus metadata,
and terminal clone input blobs.  A storage delete failure leaves metadata for a
later retry; it never marks an active package as cleaned.

Download claims remain backup- and tenant-bound and are rejected once the
metadata is deleted or expires.

## Large packages

The V1 envelope supports chunked encryption, but the V1 manifest/import format
currently builds the ZIP and validated payload files in memory.  Until a
streaming JSONL contributor contract is designed, the system enforces a 512 MB
encrypted package limit for both export and clone upload.  Requests beyond the
limit fail with a controlled business error before import.  The UI explains
that users must reduce the dataset or use an operational export process.

## Data Protection

Background-job arguments and download claims use ASP.NET Core Data Protection.
Multi-instance deployments must configure a shared, persistent key ring.  The
web startup validator emits a production warning when no persistent key-ring
path is configured; deployment validation is the final enforcement point.
