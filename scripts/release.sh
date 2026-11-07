#!/usr/bin/env bash
# Builds the production images on your machine and pushes them to a registry.
# The server then only pulls and runs them — no source code, no build, no git.
#
# Usage:
#   ./scripts/release.sh                     # build + push, auto tag
#   IMAGE_TAG=v1 ./scripts/release.sh        # custom tag
#   PUSH=0 ./scripts/release.sh              # build only (no push)
#   TARGET_PLATFORM=linux/arm64 ./scripts/release.sh
#
# Env:
#   IMAGE_REGISTRY   registry + owner      (default: ghcr.io/aref-zangeneh)
#   IMAGE_TAG        image tag             (default: <date>-<time>)
#   TARGET_PLATFORM  target platform       (default: linux/amd64)
#   PUSH             1 = push, 0 = load    (default: 1)

set -euo pipefail

APP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$APP_DIR"

REGISTRY="${IMAGE_REGISTRY:-ghcr.io/aref-zangeneh}"
TAG="${IMAGE_TAG:-$(date +%Y.%m.%d-%H%M)}"
PLATFORM="${TARGET_PLATFORM:-linux/amd64}"
PUSH="${PUSH:-1}"

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
        --tag "$REGISTRY/sugarista-$name:$TAG"
        --tag "$REGISTRY/sugarista-$name:latest"
    )

    [ -n "$dockerfile" ] && args+=(-f "$dockerfile")
    [ -n "$target" ] && args+=(--target "$target")

    if [ "$PUSH" = "1" ]; then
        args+=(--push)
    else
        args+=(--load)
    fi

    say "Building sugarista-$name ($PLATFORM)"
    docker "${args[@]}" "$context"
}

build api     "$APP_DIR/Sugarista"       "$APP_DIR/Sugarista/src/Sugarista.Api/Dockerfile"
build admin   "$APP_DIR/Sugarista-Admin" "" "runtime"
build theme   "$APP_DIR/Sugarista-Theme"
build proxy   "$APP_DIR/deploy/nginx"

if [ "$PUSH" = "1" ]; then
    say "Pushed all images with tag $TAG"
else
    say "Built all images locally with tag $TAG (not pushed)"
fi

cat <<EOF

Registry : $REGISTRY
Tag      : $TAG  (also tagged :latest)
Platform : $PLATFORM

On the server:
  cd /opt/sugarista/Sigarista
  ./scripts/server-deploy.sh

To roll back to a previous build:
  IMAGE_TAG=<older-tag> ./scripts/server-deploy.sh

EOF
