#!/bin/sh
set -eu

mkdir -p /results
rm -f /results/*.json /results/*.csv /results/*.txt

psql -X -v ON_ERROR_STOP=1 -c "CREATE EXTENSION IF NOT EXISTS pg_stat_statements;"
psql -X -v ON_ERROR_STOP=1 -c "SELECT pg_stat_statements_reset();"
psql -X -qAt -v ON_ERROR_STOP=1 -f /performance/baseline.sql

psql -X --csv -v ON_ERROR_STOP=1 -c "
SELECT name, setting, unit, source
FROM pg_settings
WHERE name IN (
  'max_connections', 'shared_preload_libraries', 'pg_stat_statements.track',
  'track_io_timing', 'log_min_duration_statement', 'statement_timeout',
  'lock_timeout', 'idle_in_transaction_session_timeout')
ORDER BY name;" > /results/postgres-settings.csv

psql -X --csv -v ON_ERROR_STOP=1 -c "
SELECT datname, state, count(*) AS connections,
       max(EXTRACT(epoch FROM (clock_timestamp() - state_change)))::numeric(12,2) AS max_state_seconds
FROM pg_stat_activity
WHERE datname = current_database()
GROUP BY datname, state
ORDER BY state;" > /results/connections.csv

psql -X --csv -v ON_ERROR_STOP=1 -c "
SELECT d.datname, d.numbackends, d.xact_commit, d.xact_rollback,
       d.deadlocks, d.temp_files, d.temp_bytes,
       pg_size_pretty(pg_database_size(d.datname)) AS database_size
FROM pg_stat_database d
WHERE d.datname = current_database();" > /results/database-stats.csv

psql -X --csv -v ON_ERROR_STOP=1 -c "
SELECT locktype, mode, granted, count(*) AS lock_count
FROM pg_locks
GROUP BY locktype, mode, granted
ORDER BY locktype, mode, granted;" > /results/locks.csv

psql -X --csv -v ON_ERROR_STOP=1 -c "
SELECT relname AS table_name, seq_scan, seq_tup_read, idx_scan, idx_tup_fetch,
       n_live_tup, n_dead_tup
FROM pg_stat_user_tables
WHERE relname LIKE 'App%'
ORDER BY relname;" > /results/table-io.csv

psql -X --csv -v ON_ERROR_STOP=1 -c "
SELECT relname AS table_name, indexrelname AS index_name,
       idx_scan, idx_tup_read, idx_tup_fetch,
       pg_size_pretty(pg_relation_size(indexrelid)) AS index_size
FROM pg_stat_user_indexes
WHERE relname LIKE 'App%'
ORDER BY relname, indexrelname;" > /results/index-usage.csv

psql -X --csv -v ON_ERROR_STOP=1 -c "
SELECT queryid,
       left(regexp_replace(query, E'[\\n\\r\\t]+', ' ', 'g'), 180) AS query,
       calls, round(total_exec_time::numeric, 3) AS total_exec_ms,
       round(mean_exec_time::numeric, 3) AS mean_exec_ms,
       rows, shared_blks_hit, shared_blks_read,
       temp_blks_read, temp_blks_written
FROM pg_stat_statements
WHERE dbid = (SELECT oid FROM pg_database WHERE datname = current_database())
ORDER BY total_exec_time DESC
LIMIT 20;" > /results/top-queries.csv

psql -X --csv -v ON_ERROR_STOP=1 -c "
WITH tenants AS (
  SELECT unnest(ARRAY[
    '10000000-0000-0000-0000-000000000001'::uuid,
    '10000000-0000-0000-0000-000000000002'::uuid]) AS tenant_id
), counts AS (
  SELECT 'products' AS entity, \"TenantId\" AS tenant_id, count(*) AS row_count FROM \"AppProducts\" GROUP BY \"TenantId\"
  UNION ALL SELECT 'oem_codes', \"TenantId\", count(*) FROM \"AppProductOEMCodes\" GROUP BY \"TenantId\"
  UNION ALL SELECT 'partners', \"TenantId\", count(*) FROM \"AppBusinessPartners\" GROUP BY \"TenantId\"
  UNION ALL SELECT 'documents', \"TenantId\", count(*) FROM \"AppDocuments\" GROUP BY \"TenantId\"
  UNION ALL SELECT 'document_items', \"TenantId\", count(*) FROM \"AppDocumentItems\" GROUP BY \"TenantId\"
  UNION ALL SELECT 'stock_movements', \"TenantId\", count(*) FROM \"AppStockMovements\" GROUP BY \"TenantId\"
  UNION ALL SELECT 'account_transactions', \"TenantId\", count(*) FROM \"AppAccountTransactions\" GROUP BY \"TenantId\"
)
SELECT c.entity, c.tenant_id, c.row_count
FROM counts c JOIN tenants t ON t.tenant_id = c.tenant_id
ORDER BY c.entity, c.tenant_id;" > /results/tenant-counts.csv

psql -X --csv -v ON_ERROR_STOP=1 -c "SELECT * FROM noxstock_perf.seed_manifest;" > /results/seed-manifest.csv

psql -X --csv -v ON_ERROR_STOP=1 -c "
SELECT check_name, mismatch_count
FROM (
  SELECT 'oem_product_tenant' AS check_name, count(*) AS mismatch_count
  FROM \"AppProductOEMCodes\" o
  JOIN \"AppProducts\" p ON p.\"Id\" = o.\"ProductId\"
  WHERE o.\"TenantId\" IS DISTINCT FROM p.\"TenantId\"
  UNION ALL
  SELECT 'document_partner_tenant', count(*)
  FROM \"AppDocuments\" d
  JOIN \"AppBusinessPartners\" p ON p.\"Id\" = d.\"BusinessPartnerId\"
  WHERE d.\"TenantId\" IS DISTINCT FROM p.\"TenantId\"
  UNION ALL
  SELECT 'document_item_parent_tenant', count(*)
  FROM \"AppDocumentItems\" i
  JOIN \"AppDocuments\" d ON d.\"Id\" = i.\"DocumentId\"
  JOIN \"AppProducts\" p ON p.\"Id\" = i.\"ProductId\"
  WHERE i.\"TenantId\" IS DISTINCT FROM d.\"TenantId\"
     OR i.\"TenantId\" IS DISTINCT FROM p.\"TenantId\"
  UNION ALL
  SELECT 'stock_movement_product_tenant', count(*)
  FROM \"AppStockMovements\" m
  JOIN \"AppProducts\" p ON p.\"Id\" = m.\"ProductId\"
  WHERE m.\"TenantId\" IS DISTINCT FROM p.\"TenantId\"
  UNION ALL
  SELECT 'account_transaction_account_tenant', count(*)
  FROM \"AppAccountTransactions\" t
  JOIN \"AppAccounts\" a ON a.\"Id\" = t.\"AccountId\"
  WHERE t.\"TenantId\" IS DISTINCT FROM a.\"TenantId\"
) checks
ORDER BY check_name;" > /results/tenant-integrity.csv

if command -v wget >/dev/null 2>&1; then
  if wget -qO /results/web-health.json http://web-performance/health-status; then
    printf 'status=200 timing_source=web-health.json:totalDuration\n' > /results/web-health.txt
  else
    printf 'status=unreachable\n' > /results/web-health.txt
  fi
fi

chmod -R a+rwX /results
echo "Performance baseline captured under docs/performance/results."
