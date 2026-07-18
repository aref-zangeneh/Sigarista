#!/bin/sh
set -e

cd /app

if [ ! -d node_modules/react ]; then
  echo "[admin-ui] Installing dependencies..."
  npm install --no-audit --no-fund
fi

publish_build() {
  mkdir -p /usr/share/nginx/html
  rm -rf /usr/share/nginx/html/*
  cp -r dist/* /usr/share/nginx/html/
}

build_and_publish() {
  echo "[admin-ui] Building from mounted source..."
  npm run build
  echo "[admin-ui] Publishing build to nginx..."
  publish_build
}

build_and_publish

if [ "${ADMIN_UI_WATCH:-1}" = "1" ]; then
  (
    echo "[admin-ui] Watching source files for changes..."
    while true; do
      inotifywait -r -e modify,create,delete,move \
        /app/src /app/index.html /app/public 2>/dev/null || sleep 2
      echo "[admin-ui] Source changed — rebuilding..."
      if build_and_publish; then
        echo "[admin-ui] Rebuild complete. Refresh the browser to see changes."
      else
        echo "[admin-ui] Build failed. Keeping the previous build."
      fi
    done
  ) &
fi

echo "[admin-ui] Starting nginx..."
exec nginx -g 'daemon off;'
