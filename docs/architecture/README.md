# Architecture reading guide

Read only the document relevant to the task:

| Task | Read first |
|---|---|
| Tenant, authentication, authorization, host routes | `tenancy-and-security.md` |
| Documents, POS, stock, payment, cancellation | `commercial-posting.md` |
| Price, cost, currency or TCMB rates | `pricing-and-currency.md` |
| EF mappings, migrations, indexes, query behavior | `persistence-and-migrations.md` |
| Context selection, risk level, validation scope | `development-workflow.md` |

Then read a focused task specification, the relevant ADR, implementation and
tests. Source code and tests remain authoritative; these documents summarize
stable contracts and must be updated when those contracts intentionally change.
