#!/usr/bin/env bash
# Builds the production images on THIS machine and packs them into one
# self-contained bundle:  dist/sugarista-<tag>.tar.gz
#
# No registry, no push — the bundle is moved to the server with
# SFTP (./scripts/upload-images.sh) and loaded there with
# ./scripts/server-deploy.sh.
#
# Usage:
#   ./scripts/release.sh                       # build + pack, auto tag
#   IMAGE_TAG=v1.2 ./scripts/release.sh        # custom tag
#   TARGET_PLATFORM=linux/arm64 ./scripts/release.sh   # ARM server
#   BUILD_ONLY=1 ./scripts/release.sh          # skip packing into dist/
#
# Env:
#   IMAGE_TAG        bundle + image tag     (default: <date>-<time>)
#   TARGET_PLATFORM  target platform        (default: linux/amd64)
#   BUILD_ONLY       1 = skip the docker save step

set -euo pipefail

APP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$APP_DIR"

TAG="${IMAGE_TAG:-$(date +%Y.%m.%d-%H%M)}"
PLATFORM="${TARGET_PLATFORM:-linux/amd64}"
BUILD_ONLY="${BUILD_ONLY:-0}"
DIST_DIR="$APP_DIR/dist"

say() { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }
die() { printf '\n\033[1;31m!!  %s\033[0m\n' "$*" >&2; exit 1; }

docker buildx version >/dev/null 2>&1 || die "docker buildx is not available."

build() {
    local name="$1"
    local context="$2"
    local dockerfile="${3:-}"
    local target="${4:-}"

    local -a args=(
        buildx build
        --platform "$PLATFORM"
        --tag "sugarista-$name:$TAG"
        --tag "sugarista-$name:latest"
    )

    [ -n "$dockerfile" ] && args+=(-f "$dockerfile")
    [ -n "$target" ] && args+=(--target "$target")

    say "Building sugarista-$name ($PLATFORM)"
    docker "${args[@]}" --load "$context"
}

build api     "$APP_DIR/Sugarista"       "$APP_DIR/Sugarista/src/Sugarista.Api/Dockerfile"
build admin   "$APP_DIR/Sugarista-Admin" "" "runtime"
build theme   "$APP_DIR/Sugarista-Theme"
build proxy   "$APP_DIR/deploy/nginx"

if [ "$BUILD_ONLY" = "1" ]; then
    say "Built all images with tag $TAG (not packed — BUILD_ONLY=1)"
    exit 0
fi

say "Packing images into dist/sugarista-$TAG.tar.gz"
mkdir -p "$DIST_DIR"
docker save \
    "sugarista-api:$TAG"     "sugarista-api:latest" \
    "sugarista-admin:$TAG"   "sugarista-admin:latest" \
    "sugarista-theme:$TAG"   "sugarista-theme:latest" \
    "sugarista-proxy:$TAG"   "sugarista-proxy:latest" \
    | gzip > "$DIST_DIR/sugarista-$TAG.tar.gz"

BUNDLE="$DIST_DIR/sugarista-$TAG.tar.gz"
SIZE="$(du -h "$BUNDLE" | cut -f1)"

cat <<EOF

Bundle   : $BUNDLE ($SIZE)
Tag      : $TAG  (also tagged :latest)
Platform : $PLATFORM

Next step — upload via SFTP to the server:
  ./scripts/upload-images.sh

Or do everything in one go (build + upload + remote deploy):
  ./scripts/ship.sh

To roll back later, load an older bundle on the server:
  IMAGE_TAG=<older-tag> ./scripts/server-deploy.sh

EOF
