#!/bin/bash
# Start the Sugarista print agent in the foreground (for testing / manual runs).
set -euo pipefail
cd "$(dirname "$0")"

if [ ! -x venv/bin/python ]; then
  echo "Python environment missing. Run ./install.sh once first." >&2
  exit 1
fi

exec node agent.js
