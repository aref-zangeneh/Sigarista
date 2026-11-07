#!/usr/bin/env bash
# Dumps the whole Sugarista database into backups/sugarista-<timestamp>.sql.gz
# and keeps the newest $KEEP_BACKUPS files.
#
# Works with both compose files:
#   - local dev stack   (docker-compose.yml, if present)
#   - image-only server (docker-compose.prod.yml)
# Override explicitly with COMPOSE_FILE=/path/to/file.
#
# Usage:
#   ./scripts/backup-db.sh
#
# Env overrides:
#   BACKUP_DIR=/opt/sugarista/backups
#   KEEP_BACKUPS=14

set -euo pipefail

APP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$APP_DIR"

if [ -z "${COMPOSE_FILE:-}" ]; then
    if [ -f "$APP_DIR/docker-compose.yml" ]; then
        COMPOSE_FILE="$APP_DIR/docker-compose.yml"
    else
        COMPOSE_FILE="$APP_DIR/docker-compose.prod.yml"
    fi
fi

compose() { docker compose -f "$COMPOSE_FILE" "$@"; }

BACKUP_DIR="${BACKUP_DIR:-$APP_DIR/backups}"
KEEP_BACKUPS="${KEEP_BACKUPS:-14}"

mkdir -p "$BACKUP_DIR"

if ! compose ps --status running --services | grep -qx postgres; then
    echo "postgres container is not running." >&2
    exit 1
fi

DB_USER="$(compose exec -T postgres printenv POSTGRES_USER < /dev/null | tr -d '\r')"
DB_NAME="$(compose exec -T postgres printenv POSTGRES_DB < /dev/null | tr -d '\r')"

if [ -z "$DB_USER" ] || [ -z "$DB_NAME" ]; then
    echo "Could not read POSTGRES_USER / POSTGRES_DB from the postgres container." >&2
    exit 1
fi

STAMP="$(date +%Y%m%d-%H%M%S)"
FILE="$BACKUP_DIR/sugarista-$STAMP.sql.gz"

compose exec -T postgres \
    pg_dump -U "$DB_USER" -d "$DB_NAME" --no-owner --no-privileges \
    | gzip > "$FILE"

if [ ! -s "$FILE" ]; then
    rm -f "$FILE"
    echo "Backup failed: the dump is empty." >&2
    exit 1
fi

# Keep only the newest N backups.
ls -1t "$BACKUP_DIR"/sugarista-*.sql.gz 2>/dev/null \
    | tail -n "+$((KEEP_BACKUPS + 1))" \
    | xargs -r rm -f

# The path is printed on stdout so callers can capture it.
echo "$FILE"
