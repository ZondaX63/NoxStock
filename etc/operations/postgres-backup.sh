#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
COMPOSE_FILE="${NOXSTOCK_BACKUP_COMPOSE_FILE:-$ROOT_DIR/docker-compose.production.yml}"
SERVICE="${NOXSTOCK_BACKUP_DB_SERVICE:-postgres}"
DATABASE="${NOXSTOCK_BACKUP_DATABASE:-NoxStock}"
DB_USER="${NOXSTOCK_BACKUP_DB_USER:-postgres}"
BACKUP_DIR="${NOXSTOCK_BACKUP_DIR:-$ROOT_DIR/backups}"
RETENTION_DAYS="${NOXSTOCK_BACKUP_RETENTION_DAYS:-14}"
PASSPHRASE_FILE="${NOXSTOCK_BACKUP_PASSPHRASE_FILE:-}"
DOCKER_CONTEXT="${DOCKER_CONTEXT:-default}"

if [[ -z "$PASSPHRASE_FILE" || ! -s "$PASSPHRASE_FILE" ]]; then
  echo "NOXSTOCK_BACKUP_PASSPHRASE_FILE must point to a non-empty runtime secret file." >&2
  exit 2
fi

command -v openssl >/dev/null 2>&1 || { echo "openssl is required." >&2; exit 2; }
command -v sha256sum >/dev/null 2>&1 || { echo "sha256sum is required." >&2; exit 2; }

mkdir -p "$BACKUP_DIR"
umask 077
timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
plain_file="$(mktemp "$BACKUP_DIR/.noxstock-$timestamp.XXXXXX.dump")"
encrypted_file="$BACKUP_DIR/noxstock-$timestamp.dump.enc"
checksum_file="$encrypted_file.sha256"

cleanup() {
  rm -f "$plain_file"
}
trap cleanup EXIT

docker --context "$DOCKER_CONTEXT" compose -f "$COMPOSE_FILE" exec -T "$SERVICE" \
  pg_dump -U "$DB_USER" -d "$DATABASE" --format=custom --compress=9 --no-owner --no-privileges \
  > "$plain_file"

if [[ ! -s "$plain_file" ]]; then
  echo "pg_dump produced an empty backup." >&2
  exit 1
fi

openssl enc -aes-256-cbc -salt -pbkdf2 -iter 200000 \
  -pass "file:$PASSPHRASE_FILE" -in "$plain_file" -out "$encrypted_file"
(cd "$BACKUP_DIR" && sha256sum "$(basename "$encrypted_file")") > "$checksum_file"

find "$BACKUP_DIR" -maxdepth 1 -type f \
  \( -name 'noxstock-*.dump.enc' -o -name 'noxstock-*.dump.enc.sha256' \) \
  -mtime "+$RETENTION_DAYS" -delete

printf 'backup=%s\nchecksum=%s\nretention_days=%s\n' \
  "$encrypted_file" "$checksum_file" "$RETENTION_DAYS"
