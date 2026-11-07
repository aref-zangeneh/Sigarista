#!/usr/bin/env bash
# Replaces the live database with a backup produced by backup-db.sh.
# The API is stopped while the database is being replaced.
#
# Usage:
#   ./scripts/restore-db.sh backups/sugarista-20261107-120000.sql.gz
#
# Type RESTORE at the prompt to continue.

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

FILE="${1:-}"

if [ -z "$FILE" ] || [ ! -f "$FILE" ]; then
    echo "usage: ./scripts/restore-db.sh <path-to-backup.sql.gz>" >&2
    echo "available backups:" >&2
    ls -1t backups/sugarista-*.sql.gz 2>/dev/null >&2 || echo "  (none)" >&2
    exit 1
fi

echo "About to REPLACE the live database with:"
echo "  $FILE"
echo

# Ask first: docker compose exec attaches stdin and would swallow the answer.
read -r -p "Type RESTORE to continue: " answer
[ "$answer" = "RESTORE" ] || { echo "Aborted."; exit 1; }

DB_USER="$(compose exec -T postgres printenv POSTGRES_USER < /dev/null | tr -d '\r')"
DB_NAME="$(compose exec -T postgres printenv POSTGRES_DB < /dev/null | tr -d '\r')"

echo "Stopping API..."
compose stop api

echo "Recreating database '$DB_NAME'..."
compose exec -T postgres psql -U "$DB_USER" -d postgres -v ON_ERROR_STOP=1 < /dev/null \
    -c "DROP DATABASE IF EXISTS \"$DB_NAME\" WITH (FORCE);"
compose exec -T postgres psql -U "$DB_USER" -d postgres -v ON_ERROR_STOP=1 < /dev/null \
    -c "CREATE DATABASE \"$DB_NAME\" OWNER \"$DB_USER\";"

echo "Restoring dump..."
gunzip -c "$FILE" \
    | compose exec -T postgres psql -U "$DB_USER" -d "$DB_NAME" -v ON_ERROR_STOP=1 \
    > /dev/null

echo "Starting API..."
compose start api

echo "Done. Pending EF migrations (if any) will be applied automatically on startup."
