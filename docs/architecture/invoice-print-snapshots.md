# Invoice print snapshots

Finalized invoices are printed from the commercial identity values captured on
the document at approval. Directly approved POS documents capture the same
values during creation. The snapshot covers the configured company identity,
the business partner identity and address, and each line's product name, SKU
and unit. Existing line price, discount, tax, quantity, total, currency and
exchange-rate fields remain the financial snapshot.

`Draft` documents may use live company, partner and product records. Documents
in `Approved`, `PartiallyPaid`, `Paid` or `Cancelled` state with
`PrintSnapshotCapturedAt` set must use only their snapshot fields for print
data. Cancellation preserves the source document snapshot.

Historical finalized documents are not backfilled or guessed. A finalized
document without `PrintSnapshotCapturedAt` uses the legacy live-data fallback
and the print view displays a visible note. This is a compatibility path, not a
legal-history guarantee; it ends for each document when it is newly approved
under the snapshot schema.
