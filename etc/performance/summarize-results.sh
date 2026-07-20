#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
RESULTS_DIR="$ROOT_DIR/docs/performance/results"
OUTPUT="$ROOT_DIR/docs/performance/p1.0-baseline.md"
METADATA="$ROOT_DIR/etc/performance/query-metadata.tsv"

command -v jq >/dev/null 2>&1 || { echo "jq is required to summarize JSON plans" >&2; exit 1; }

profile="$(tail -n +2 "$RESULTS_DIR/seed-manifest.csv" | head -1 | cut -d, -f1 | tr -d '"')"
seeded_at="$(tail -n +2 "$RESULTS_DIR/seed-manifest.csv" | head -1 | cut -d, -f3 | tr -d '"')"

{
  echo "# P1.0 Query Baseline"
  echo
  echo "Generated from the isolated NoxStock performance database."
  echo
  echo "- Profile: \`$profile\`"
  echo "- Seeded at: \`$seeded_at\`"
  echo "- Plans: PostgreSQL \`EXPLAIN (ANALYZE, BUFFERS, SETTINGS, FORMAT JSON)\`"
  echo
  echo "| # | Query | App SQL commands | Execution ms | Root plan | Scan nodes | Rows | Shared hit/read | Tenant filter | Materialized entity/DTO envelope |"
  echo "|---:|---|---:|---:|---|---|---:|---:|---|---|"

  while IFS=$'\t' read -r slug name commands materialized; do
    plan_file="$RESULTS_DIR/$slug.json"
    execution_ms="$(jq -r '.[0]["Execution Time"]' "$plan_file")"
    root_plan="$(jq -r '.[0].Plan["Node Type"]' "$plan_file")"
    rows="$(jq -r '.[0].Plan["Actual Rows"]' "$plan_file")"
    hits="$(jq -r '.[0].Plan["Shared Hit Blocks"] // 0' "$plan_file")"
    reads="$(jq -r '.[0].Plan["Shared Read Blocks"] // 0' "$plan_file")"
    scans="$(jq -r '[.[0].Plan | .. | objects | .["Node Type"]? | select(. != null and test("Scan"))] | unique | join(", ")' "$plan_file")"
    if grep -q 'TenantId' "$plan_file"; then tenant_filter=yes; else tenant_filter=no; fi
    number="${slug%%-*}"
    echo "| $number | $name | $commands | $execution_ms | $root_plan | $scans | $rows | $hits/$reads | $tenant_filter | $materialized |"
  done < "$METADATA"

  echo
  echo "## SLO Interpretation"
  echo
  echo "These are single-user database execution times, not endpoint p95/p99 results. They are valid for plan and buffer prioritization only. The direct SQL times are below the preliminary latency budgets except POS product search, but the product/OEM/POS command counts, deep offset behavior, and unbounded report materialization already fail their structural SLOs. Concurrency readiness remains unproven until P4."

  echo
  echo "## Measured P1.1 Candidates"
  echo
  echo "1. Remove product-page and OEM N+1 reads before index work (52 commands for a 50-row page)."
  echo "2. Split exact SKU/barcode/OEM routing from substring POS search; exact SKU/barcode currently use sequential scans."
  echo "3. Address POS search buffer cost and repeated exchange-rate reads (42-62 commands)."
  echo "4. Replace deep offset navigation after bounded-query cleanup; the last product page spills temp blocks."
  echo "5. Bound report materialization and rate lookup count before report indexing."
  echo "6. Re-measure dashboard/document/history paths before selecting tenant-first composite indexes."

  echo
  echo "## Artifacts"
  echo
  echo "Raw plans, PostgreSQL settings, connection/lock/database statistics, tenant counts, index usage and pg_stat_statements top queries are stored in \`docs/performance/results\`."
} > "$OUTPUT"

echo "Wrote $OUTPUT"
