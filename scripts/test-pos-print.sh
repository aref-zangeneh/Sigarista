#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT_DIR"

echo "==> Checking print agent health"
if curl -fsS http://127.0.0.1:9101/health >/tmp/sugarista-print-health.json 2>/dev/null; then
  cat /tmp/sugarista-print-health.json
  echo
else
  echo "Print agent is not running. Start it with:"
  echo "  node scripts/pos-print-server.mjs"
  exit 1
fi

echo "==> Sending test customer + barista receipts"
curl -fsS -X POST http://127.0.0.1:9101/test | tee /tmp/sugarista-print-test.json
echo

echo "==> Verifying saved receipt files"
ls -1 .pos-receipts | tail -2 | while read -r file; do
  echo "--- ${file} ---"
  cat ".pos-receipts/${file}"
  echo
done

echo "Done."
