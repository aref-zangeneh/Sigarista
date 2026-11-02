#!/bin/bash
# One-time installer for the Sugarista print agent on macOS.
# - creates the Python venv used for receipt rendering
# - installs a LaunchAgent so the agent starts at login and restarts itself
#
# Usage:  ./install.sh            (installs + starts)
#         ./install.sh --uninstall (stops + removes the LaunchAgent)
set -euo pipefail

cd "$(dirname "$0")"
AGENT_DIR="$(pwd)"
LABEL="com.sugarista.printagent"
PLIST="$HOME/Library/LaunchAgents/$LABEL.plist"
LOG_DIR="$HOME/Library/Logs"
LOG_FILE="$LOG_DIR/SugaristaPrintAgent.log"

if [ "${1:-}" = "--uninstall" ]; then
  launchctl bootout "gui/$(id -u)/$LABEL" 2>/dev/null || true
  rm -f "$PLIST"
  echo "Removed $LABEL. (Log kept at $LOG_FILE)"
  exit 0
fi

command -v node >/dev/null 2>&1 || { echo "Node.js is required (brew install node)." >&2; exit 1; }
command -v lp >/dev/null 2>&1 || { echo "CUPS lp command not found." >&2; exit 1; }

echo "→ Setting up Python environment…"
if [ ! -x venv/bin/python ]; then
  python3 -m venv venv
fi

# The packages are only needed once; skip the network when they are already there
# (so the installer still works offline / on a flaky connection).
if venv/bin/python -c "import PIL, arabic_reshaper, bidi" >/dev/null 2>&1; then
  echo "  ✓ python packages already installed"
else
  venv/bin/pip install --quiet --upgrade pip || echo "  ⚠ could not upgrade pip (offline?) — continuing"
  if ! venv/bin/pip install --quiet pillow arabic-reshaper python-bidi; then
    echo "  ✗ could not install pillow / arabic-reshaper / python-bidi." >&2
    echo "    Connect to the internet and re-run ./install.sh." >&2
    exit 1
  fi
  echo "  ✓ python packages installed"
fi

echo "→ Checking printer queue…"
PRINTER_NAME="$(node -e "process.stdout.write(require('./config.json').printerName || '')")"
if [ -n "$PRINTER_NAME" ]; then
  if lpstat -p "$PRINTER_NAME" >/dev/null 2>&1; then
    echo "  ✓ printer '$PRINTER_NAME' is available"
  else
    echo "  ⚠ printer '$PRINTER_NAME' not found. Available queues:" >&2
    lpstat -p >&2 || true
    echo "  Update printerName in config.json, then re-run ./install.sh." >&2
  fi
fi

NODE_BIN="$(command -v node)"
mkdir -p "$HOME/Library/LaunchAgents" "$LOG_DIR"

cat > "$PLIST" <<PLIST_EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key>
  <string>$LABEL</string>
  <key>ProgramArguments</key>
  <array>
    <string>$NODE_BIN</string>
    <string>$AGENT_DIR/agent.js</string>
  </array>
  <key>WorkingDirectory</key>
  <string>$AGENT_DIR</string>
  <key>RunAtLoad</key>
  <true/>
  <key>KeepAlive</key>
  <true/>
  <key>StandardOutPath</key>
  <string>$LOG_FILE</string>
  <key>StandardErrorPath</key>
  <string>$LOG_FILE</string>
</dict>
</plist>
PLIST_EOF

launchctl bootout "gui/$(id -u)/$LABEL" 2>/dev/null || true
launchctl bootstrap "gui/$(id -u)" "$PLIST"
launchctl kickstart -k "gui/$(id -u)/$LABEL"

echo "✓ Print agent installed and running."
echo "  logs: $LOG_FILE"
echo "  stop: ./install.sh --uninstall"
