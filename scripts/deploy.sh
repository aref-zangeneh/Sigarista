#!/usr/bin/env bash
# One-command production deploy.
#
#   ./scripts/deploy.sh              # pull latest main, back up DB, rebuild, restart
#   SKIP_PULL=1 ./scripts/deploy.sh  # deploy the commit that is already checked out
#
# What it does, in order:
#   1. refuses to run on a dirty working tree
#   2. pulls origin/<branch> (fast-forward only)
#   3. takes a full database backup (the safety net)
#   4. rebuilds images and recreates containers
#   5. waits for /health and verifies the public pages
#
# Named volumes (postgres-data, api-uploads) are never touched, so database
# and uploaded files survive every deploy.

set -euo pipefail

APP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$APP_DIR"

BRANCH="${DEPLOY_BRANCH:-main}"
BACKUP_DIR="${BACKUP_DIR:-$APP_DIR/backups}"
SKIP_PULL="${SKIP_PULL:-0}"

say() { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }
die() { printf '\n\033[1;31m!!  %s\033[0m\n' "$*" >&2; exit 1; }

[ -f .env ] || die ".env is missing. Create it once from .env.example before deploying."
docker compose version >/dev/null 2>&1 || die "docker compose (v2) is not available."

# ── 1. code ────────────────────────────────────────────────────────────────
if [ "$SKIP_PULL" != "1" ]; then
    git rev-parse --is-inside-work-tree >/dev/null 2>&1 \
        || die "$APP_DIR is not a git checkout. Clone the repo on the server instead of copying files."

    if ! git diff --quiet || ! git diff --cached --quiet; then
        die "Working tree has uncommitted changes on the server. Commit/stash/discard them first."
    fi

    say "Fetching $BRANCH"
    git fetch --prune origin "$BRANCH"
    git checkout "$BRANCH"
    git merge --ff-only "origin/$BRANCH"
fi

REV="$(git rev-parse --short HEAD)"
say "Deploying commit $REV"

# ── 2. database backup ─────────────────────────────────────────────────────
say "Backing up database"
BACKUP_FILE="$(BACKUP_DIR="$BACKUP_DIR" "$APP_DIR/scripts/backup-db.sh")"
say "Backup written to $BACKUP_FILE"

# ── 3. build + restart ─────────────────────────────────────────────────────
say "Building images"
docker compose build

say "Applying changes"
docker compose up -d --remove-orphans

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
    docker compose logs --tail=100 api
    die "API did not become healthy.
   Your data is safe. Inspect:  docker compose logs -f api
   Roll back code:              git checkout <previous-commit> && SKIP_PULL=1 ./scripts/deploy.sh
   Restore database:            ./scripts/restore-db.sh $BACKUP_FILE"
fi

# ── 5. smoke test ──────────────────────────────────────────────────────────
say "Smoke test"
# -L: on the production server Caddy 308-redirects HTTP -> HTTPS; follow it.
code_health="$(curl -sL -o /dev/null -w '%{http_code}' http://127.0.0.1/health)"
code_admin="$(curl -sL -o /dev/null -w '%{http_code}' http://127.0.0.1/admin/)"
code_theme="$(curl -sL -o /dev/null -w '%{http_code}' http://127.0.0.1/)"
printf '  /health  -> %s\n  /admin/  -> %s\n  /        -> %s\n' "$code_health" "$code_admin" "$code_theme"

[ "$code_health" = "200" ] && [ "$code_admin" = "200" ] && [ "$code_theme" = "200" ] \
    || die "One of the endpoints did not return 200. Check: docker compose logs --tail=100"

say "Deployed $REV successfully."
printf '\nRollback code:   git checkout <previous-commit> && SKIP_PULL=1 ./scripts/deploy.sh\n'
printf 'Restore DB:      ./scripts/restore-db.sh %s\n' "$BACKUP_FILE"
