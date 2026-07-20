# NoxStock Performance Environment

This environment is isolated from the development and production databases. It uses the
`noxstock-performance` Compose project, the `NoxStockPerformance` database, a dedicated
Compose volume, PostgreSQL on loopback port `5440`, and Web on loopback port `8081`.

## Profiles

| Profile | Products / tenant | OEM / product | Partners / tenant | Documents / tenant | Items / document | Stock movements / tenant | Account transactions / tenant |
|---|---:|---:|---:|---:|---:|---:|---:|
| `small` | 1,000 | 2 | 200 | 500 | 3 | 2,000 | 1,000 |
| `medium` | 25,000 | 2 | 2,500 | 10,000 | 4 | 50,000 | 20,000 |
| `target` | 100,000 | 3 | 10,000 | 50,000 | 4 | 200,000 | 100,000 |

Every profile creates two fixed performance tenants. Data values and UUIDs are derived
from the profile parameters and the default `2026-07-01` anchor date. Running a seed
again removes only these two tenants and recreates the same deterministic records.

## Commands

Run these commands from the repository root:

```bash
etc/performance/perf.sh up small
etc/performance/perf.sh seed medium
etc/performance/perf.sh seed target
etc/performance/perf.sh baseline
etc/performance/perf.sh status
etc/performance/perf.sh clean
etc/performance/perf.sh down
etc/performance/perf.sh reset
```

`clean` removes only the fixed performance tenants. `reset` removes only the isolated
performance Compose containers, network, and volumes. Neither command targets the normal
NoxStock Compose project or its database.

## PostgreSQL Budgets

The defaults below apply only to this Compose file and can be overridden per run:

| Variable | Default |
|---|---:|
| `NOXSTOCK_PERF_SLOW_QUERY_MS` | `250` |
| `NOXSTOCK_PERF_STATEMENT_TIMEOUT` | `60s` |
| `NOXSTOCK_PERF_LOCK_TIMEOUT` | `5s` |
| `NOXSTOCK_PERF_IDLE_TX_TIMEOUT` | `30s` |
| `NOXSTOCK_PERF_MAX_POOL_SIZE` | `40` |
| `NOXSTOCK_PERF_COMMAND_TIMEOUT_SECONDS` | `60` |

PostgreSQL starts with `pg_stat_statements`, `track_io_timing`, and slow-query logging.
These settings are intentionally not copied into production configuration.

## Baseline Artifacts

`perf.sh baseline` captures the 14 required JSON plans and writes the summary to
`docs/performance/p1.0-baseline.md`. Supporting CSV/JSON files under
`docs/performance/results` include:

- active and idle connections, locks, deadlocks, database size, and temp-file usage;
- `pg_stat_statements` top 20 statements;
- table I/O and index usage;
- tenant row counts and parent/child TenantId mismatch checks;
- seed manifest and Web/DB health response.

The plan baseline is a single-user SQL diagnostic, not a p95/p99 or concurrency result.
Target-scale readiness still requires the authenticated multi-tenant load tests in P4.
