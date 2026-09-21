# Domain-layer guidance

Keep the domain independent of Web and EF implementation details. Preserve the
existing aggregate identities, tenant ownership and audited entity types.

- Document status changes must use `Document.TransitionTo`.
- Historical cost, exchange-rate and pricing records are evidence; corrections
  use new history/reversal records rather than overwriting their meaning.
- New tenant-owned entities require explicit tenant construction and mapping.
- Do not introduce `double`/`float` for money, rates or quantities.
- A public setter is not permission to bypass the owning application workflow.

For business effects, read `docs/architecture/commercial-posting.md`; for an
accepted policy, read the relevant ADR.
