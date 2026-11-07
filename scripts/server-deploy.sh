#!/usr/bin/env bash
# Deploys pre-built images on the server.
#
#   ./scripts/server-deploy.sh
#   IMAGE_TAG=2026.11.06-0900 ./scripts/server-deploy.sh   # rollback / pin
#
# Requirements on the server (one-time):
#   - docker + docker compose v2
#   - docker-compose.prod.yml
#   - .env with the real secrets
#   - docker login <registry>   (only if the images are private)
#
# What it does:
#   1. backs up the database (if it is already running)
#   2. pulls the images for IMAGE_TAG
#   3. recreates the containers, keeping the postgres-data / api-uploads volumes
#   4. waits for /health and smoke-tests the public endpoints
#
# It never builds, never reads source code, and never touches .env.

set -euo pipefail

APP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$APP_DIR"

COMPOSE_FILE="${COMPOSE_FILE:-$APP_DIR/docker-compose.prod.yml}"
BACKUP_DIR="${BACKUP_DIR:-$APP_DIR/backups}"

say() { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }
die() { printf '\n\033[1;31m!!  %s\033[0m\n' "$*" >&2; exit 1; }

compose() { docker compose -f "$COMPOSE_FILE" "$@"; }

[ -f "$COMPOSE_FILE" ] || die "$COMPOSE_FILE not found."
[ -f "$APP_DIR/.env" ] || die ".env not found in $APP_DIR."
docker compose version >/dev/null 2>&1 || die "docker compose (v2) is not available."

say "Target image tag: ${IMAGE_TAG:-latest}"

# ── 1. backup ──────────────────────────────────────────────────────────────
BACKUP_FILE=""
if compose ps --status running --services 2>/dev/null | grep -qx postgres; then
    say "Backing up database"
    BACKUP_FILE="$(COMPOSE_FILE="$COMPOSE_FILE" BACKUP_DIR="$BACKUP_DIR" \
        "$APP_DIR/scripts/backup-db.sh")"
    say "Backup written to $BACKUP_FILE"
else
    say "postgres is not running yet — skipping backup (first deploy)"
fi

# ── 2. pull ────────────────────────────────────────────────────────────────
say "Pulling images"
compose pull

# ── 3. recreate containers ─────────────────────────────────────────────────
say "Applying changes"
compose up -d --remove-orphans

# ── 4. health check ────────────────────────────────────────────────────────
say "Waiting for API health"
healthy=0
for _ in $(seq 1 60); do
    if curl -fsS http://127.0.0.1/health >/dev/null 2>&1; then
        healthy=1
        break
    fi
    sleep 3
done

if [ "$healthy" != "1" ]; then
    compose logs --tail=100 api
    die "API did not become healthy.
   Data is safe. Inspect:      docker compose -f $COMPOSE_FILE logs -f api
   Retry same tag:             IMAGE_TAG=${IMAGE_TAG:-latest} ./scripts/server-deploy.sh
   Roll back to older build:   IMAGE_TAG=<older-tag> ./scripts/server-deploy.sh
   Restore database:           ./scripts/restore-db.sh ${BACKUP_FILE:-<backup>}"
fi

# ── 5. smoke test ──────────────────────────────────────────────────────────
say "Smoke test"
code_health="$(curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1/health)"
code_admin="$(curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1/admin/)"
code_theme="$(curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1/)"
printf '  /health  -> %s\n  /admin/  -> %s\n  /        -> %s\n' "$code_health" "$code_admin" "$code_theme"

[ "$code_health" = "200" ] && [ "$code_admin" = "200" ] && [ "$code_theme" = "200" ] \
    || die "One of the endpoints did not return 200. Check: docker compose -f $COMPOSE_FILE logs --tail=100"

say "Deployed image tag ${IMAGE_TAG:-latest} successfully."
compose images

printf '\nRoll back:   IMAGE_TAG=<older-tag> ./scripts/server-deploy.sh\n'
[ -n "$BACKUP_FILE" ] && printf 'Restore DB:  ./scripts/restore-db.sh %s\n' "$BACKUP_FILE"

if [ "${PRUNE:-0}" = "1" ]; then
    say "Removing dangling images"
    docker image prune -f
fi
