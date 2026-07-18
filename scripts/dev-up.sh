#!/usr/bin/env sh
set -e

cd "$(dirname "$0")/.."

echo "Rebuilding and recreating UI containers so local changes are applied..."
docker compose up -d --build --force-recreate admin-ui public-ui proxy "$@"

echo ""
echo "Done."
echo "  Digital menu: http://localhost/"
echo "  Admin panel:  http://localhost/admin/"
echo ""
echo "Theme file changes apply immediately (volume mount)."
echo "Admin changes apply on each recreate (entrypoint rebuilds from source)."
