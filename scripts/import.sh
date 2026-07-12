#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GODOT_COMMAND="${GODOT_BIN:-godot}"
if command -v "$GODOT_COMMAND" >/dev/null 2>&1; then
  GODOT_COMMAND="$(realpath "$(command -v "$GODOT_COMMAND")")"
fi
"$ROOT/scripts/preflight.sh"
"$GODOT_COMMAND" --headless --path "$ROOT/game" --import
