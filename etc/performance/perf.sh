#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
COMPOSE_FILE="$ROOT_DIR/docker-compose.performance.yml"
DOCKER_CONTEXT="${DOCKER_CONTEXT:-default}"
PROFILE="${2:-${NOXSTOCK_PERF_PROFILE:-small}}"

compose() {
  docker --context "$DOCKER_CONTEXT" compose -f "$COMPOSE_FILE" "$@"
}

case "${1:-help}" in
  up)
    export NOXSTOCK_PERF_PROFILE="$PROFILE"
    compose up --build -d --force-recreate --remove-orphans
    compose stop web-performance
    compose run --rm seeder-performance
    compose up -d --no-deps web-performance
    ;;
  seed)
    export NOXSTOCK_PERF_PROFILE="$PROFILE"
    export NOXSTOCK_PERF_ACTION=seed
    compose stop web-performance
    compose run --rm seeder-performance
    compose up -d --no-deps web-performance
    ;;
  clean)
    export NOXSTOCK_PERF_ACTION=clean
    compose stop web-performance
    compose run --rm seeder-performance
    ;;
  baseline)
    compose run --rm --no-deps observer-performance
    "$ROOT_DIR/etc/performance/summarize-results.sh"
    ;;
  status)
    compose ps -a
    ;;
  down)
    compose down --remove-orphans
    ;;
  reset)
    echo "Removing only the isolated noxstock-performance containers and volumes."
    compose down -v --remove-orphans
    ;;
  *)
    cat <<'EOF'
Usage: etc/performance/perf.sh <command> [small|medium|target]

Commands:
  up        Start isolated DB, migrate, deterministically seed, and start Web
  seed      Clean and reseed the performance tenants
  clean     Remove only deterministic performance tenant data
  baseline  Capture EXPLAIN, pg_stat_statements, connections, locks, and indexes
  status    Show isolated performance containers
  down      Stop containers and preserve the performance volume
  reset     Remove isolated performance containers and volumes
EOF
    ;;
esac
