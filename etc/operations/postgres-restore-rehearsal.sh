#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
COMPOSE_FILE="$ROOT_DIR/docker-compose.restore-test.yml"
BACKUP_FILE="${1:-}"
PASSPHRASE_FILE="${NOXSTOCK_BACKUP_PASSPHRASE_FILE:-}"
DOCKER_CONTEXT="${DOCKER_CONTEXT:-default}"
EVIDENCE_DIR="$ROOT_DIR/docs/release/evidence"
EVIDENCE_FILE="$EVIDENCE_DIR/restore-rehearsal.txt"

if [[ -z "$BACKUP_FILE" || ! -f "$BACKUP_FILE" ]]; then
  echo "Usage: NOXSTOCK_BACKUP_PASSPHRASE_FILE=/runtime/secret $0 <backup.dump.enc>" >&2
  exit 2
fi

if [[ -z "$PASSPHRASE_FILE" || ! -s "$PASSPHRASE_FILE" ]]; then
  echo "NOXSTOCK_BACKUP_PASSPHRASE_FILE must point to a non-empty runtime secret file." >&2
  exit 2
fi

mkdir -p "$EVIDENCE_DIR"
umask 077
plain_file="$(mktemp /tmp/noxstock-restore.XXXXXX.dump)"
cleanup() { rm -f "$plain_file"; }
trap cleanup EXIT

openssl enc -d -aes-256-cbc -pbkdf2 -iter 200000 \
  -pass "file:$PASSPHRASE_FILE" -in "$BACKUP_FILE" -out "$plain_file"

compose() {
  docker --context "$DOCKER_CONTEXT" compose -f "$COMPOSE_FILE" "$@"
}

compose build db-migrator-restore web-restore
compose down -v --remove-orphans
compose up -d postgres-restore

for _ in $(seq 1 30); do
  if compose exec -T postgres-restore pg_isready -U postgres -d NoxStockRestore >/dev/null 2>&1; then
    break
  fi
  sleep 1
done
compose exec -T postgres-restore pg_isready -U postgres -d NoxStockRestore >/dev/null

compose exec -T postgres-restore pg_restore -U postgres -d NoxStockRestore \
  --no-owner --no-privileges --exit-on-error < "$plain_file"

compose run --rm db-migrator-restore

{
  echo "rehearsal_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "backup_sha256=$(sha256sum "$BACKUP_FILE" | awk '{print $1}')"
  compose exec -T postgres-restore psql -U postgres -d NoxStockRestore -X -f /operations/restore-integrity.sql
} > "$EVIDENCE_FILE"

compose up -d --no-deps web-restore
for _ in $(seq 1 30); do
  if curl -fsS http://127.0.0.1:8085/health/ready >/dev/null 2>&1; then
    break
  fi
  sleep 1
done
curl -fsS http://127.0.0.1:8085/health/ready >> "$EVIDENCE_FILE"
printf '\nrestore_status=passed\n' >> "$EVIDENCE_FILE"
chmod 600 "$EVIDENCE_FILE"

echo "Restore rehearsal passed. Evidence: $EVIDENCE_FILE"
