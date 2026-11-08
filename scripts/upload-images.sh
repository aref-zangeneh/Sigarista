#!/usr/bin/env bash
# Uploads the image bundle built by ./scripts/release.sh straight to the
# server over SFTP — no registry, no intermediary, nothing between the
# laptop and the server.
#
# Usage:
#   ./scripts/upload-images.sh                         # newest bundle
#   IMAGE_TAG=2026.11.09-1200 ./scripts/upload-images.sh  # specific bundle
#   WITH_FILES=1 ./scripts/upload-images.sh            # also sync the
#                                                      # compose file + scripts
#
# Env (pick once — put them in deploy/.env-upload or export them):
#   SERVER_HOST   user@host            (default: root@sugarista-web)
#   SERVER_PATH   remote directory     (default: /opt/sugarista/Sigarista)

set -euo pipefail

APP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$APP_DIR"

# Optional local config: deploy/.env-upload (never committed)
[ -f "$APP_DIR/deploy/.env-upload" ] && . "$APP_DIR/deploy/.env-upload"

SERVER_HOST="${SERVER_HOST:-root@sugarista-web}"
SERVER_PATH="${SERVER_PATH:-/opt/sugarista/Sigarista}"
WITH_FILES="${WITH_FILES:-0}"
DIST_DIR="$APP_DIR/dist"

say() { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }
die() { printf '\n\033[1;31m!!  %s\033[0m\n' "$*" >&2; exit 1; }

ssh -o BatchMode=yes -o ConnectTimeout=5 "$SERVER_HOST" true 2>/dev/null \
    || die "SSH to $SERVER_HOST failed.
   Check the host alias in ~/.ssh/config, that ssh-copy-id was done,
   or set SERVER_HOST='user@server-ip' before running."

TAG_FILE=""
if [ -n "${IMAGE_TAG:-}" ]; then
    for ext in tar.gz tar; do
        [ -f "$DIST_DIR/sugarista-$IMAGE_TAG.$ext" ] && TAG_FILE="$DIST_DIR/sugarista-$IMAGE_TAG.$ext" && break
    done
else
    TAG_FILE="$(ls -t "$DIST_DIR"/sugarista-*.tar.gz "$DIST_DIR"/sugarista-*.tar 2>/dev/null | head -1 || true)"
fi
[ -n "$TAG_FILE" ] && [ -f "$TAG_FILE" ] || \
    die "No image bundle found in $DIST_DIR. Run './scripts/release.sh' first."

TAG="${IMAGE_TAG:-$(basename "$TAG_FILE" | sed -E 's/^sugarista-(.+)\.(tar\.gz|tar)$/\1/')}"
SIZE="$(du -h "$TAG_FILE" | cut -f1)"

say "Uploading $(basename "$TAG_FILE") ($SIZE) to $SERVER_HOST:$SERVER_PATH/dist/"

ssh "$SERVER_HOST" "mkdir -p $SERVER_PATH/dist"

# SFTP batch upload — resumes nothing, but fails loudly and shows progress.
sftp -b - "$SERVER_HOST" <<EOF
cd "$SERVER_PATH"
put -r "$TAG_FILE" dist/
EOF

if [ "$WITH_FILES" = "1" ]; then
    say "Syncing docker-compose.prod.yml and scripts/"
    sftp -b - "$SERVER_HOST" <<EOF
cd "$SERVER_PATH"
put "$APP_DIR/docker-compose.prod.yml"
put -r "$APP_DIR/scripts"
EOF
fi

say "Uploaded."
cat <<EOF

On the server, deploy it:
  ssh $SERVER_HOST
  cd $SERVER_PATH && ./scripts/server-deploy.sh

Or from right here, in one line:
  ssh $SERVER_HOST "cd $SERVER_PATH && ./scripts/server-deploy.sh"

EOF
