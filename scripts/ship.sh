#!/usr/bin/env bash
# Runs everything from the laptop, in one go:
#   1. ./scripts/release.sh        — build images + pack dist/sugarista-<tag>.tar.gz
#   2. ./scripts/upload-images.sh  — SFTP the bundle (and optionally files) to the server
#   3. ssh … server-deploy.sh      — on the server: load images, backup DB, restart, health-check
#
# Usage:
#   ./scripts/ship.sh                              # full chain
#   SHIP_NO_DEPLOY=1 ./scripts/ship.sh             # build + upload only
#   SKIP_BUILD=1 ./scripts/ship.sh                 # reuse the newest dist/ bundle
#   SKIP_UPLOAD=1 ./scripts/ship.sh               # deploy a bundle already on the server
#   IMAGE_TAG=<tag> ./scripts/ship.sh              # pin a specific tag end-to-end
#   WITH_FILES=1 ./scripts/ship.sh                 # also sync compose + scripts
#
# Deployment target (like upload-images):
#   SERVER_HOST (default: root@sugarista-web)
#   SERVER_PATH (default: /opt/sugarista/Sigarista)

set -euo pipefail

APP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$APP_DIR"

[ -f "$APP_DIR/deploy/.env-upload" ] && . "$APP_DIR/deploy/.env-upload"

SERVER_HOST="${SERVER_HOST:-root@sugarista-web}"
SERVER_PATH="${SERVER_PATH:-/opt/sugarista/Sigarista}"
SKIP_BUILD="${SKIP_BUILD:-0}"
SKIP_UPLOAD="${SKIP_UPLOAD:-0}"
SHIP_NO_DEPLOY="${SHIP_NO_DEPLOY:-0}"

say() { printf '\n\033[1;34m== %s ==\033[0m\n' "$*"; }

if [ "$SKIP_BUILD" != "1" ]; then
    say "1/3 Building images on this machine"
    ./scripts/release.sh
    [ -n "${IMAGE_TAG:-}" ] || IMAGE_TAG="$(ls -t dist/ | head -1 | sed -E 's/^sugarista-(.+)\.(tar\.gz|tar)$/\1/')"
    export IMAGE_TAG
fi

if [ "$SKIP_UPLOAD" != "1" ]; then
    say "2/3 Uploading bundle via SFTP"
    ./scripts/upload-images.sh
fi

if [ "$SHIP_NO_DEPLOY" != "1" ]; then
    say "3/3 Deploying on $SERVER_HOST"
    ssh "$SERVER_HOST" "cd $SERVER_PATH && ./scripts/server-deploy.sh"
fi

say "All done."
