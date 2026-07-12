#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REQUIRE_TEMPLATES=false
if [[ "${1:-}" == "--require-templates" ]]; then
  REQUIRE_TEMPLATES=true
fi

EXPECTED_DOTNET="10.0.301"
EXPECTED_GODOT="4.6.1.stable.mono.official.14d19694e"
EXPECTED_TEMPLATES="4.6.1.stable.mono"
GODOT_COMMAND="${GODOT_BIN:-godot}"
if command -v "$GODOT_COMMAND" >/dev/null 2>&1; then
  GODOT_COMMAND="$(realpath "$(command -v "$GODOT_COMMAND")")"
fi

fail() {
  printf 'preflight error: %s\n' "$1" >&2
  exit 1
}

command -v dotnet >/dev/null 2>&1 || fail "dotnet is missing; install SDK ${EXPECTED_DOTNET}."
ACTUAL_DOTNET="$(dotnet --version)"
[[ "$ACTUAL_DOTNET" == "$EXPECTED_DOTNET" ]] || fail "expected .NET SDK ${EXPECTED_DOTNET}, found ${ACTUAL_DOTNET}."

command -v git >/dev/null 2>&1 || fail "git is missing."
git lfs version >/dev/null 2>&1 || fail "Git LFS is missing; install it and run 'git lfs install'."

command -v "$GODOT_COMMAND" >/dev/null 2>&1 || fail "Godot is missing; set GODOT_BIN or install Godot ${EXPECTED_GODOT}."
ACTUAL_GODOT="$($GODOT_COMMAND --version)"
[[ "$ACTUAL_GODOT" == "$EXPECTED_GODOT" ]] || fail "expected Godot ${EXPECTED_GODOT}, found ${ACTUAL_GODOT}."

if [[ "$REQUIRE_TEMPLATES" == true ]]; then
  if [[ -n "${GODOT_EXPORT_TEMPLATES_DIR:-}" ]]; then
    TEMPLATE_DIR="$GODOT_EXPORT_TEMPLATES_DIR"
  elif [[ "$(uname -s)" == "Darwin" ]]; then
    TEMPLATE_DIR="$HOME/Library/Application Support/Godot/export_templates/$EXPECTED_TEMPLATES"
  else
    TEMPLATE_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/godot/export_templates/$EXPECTED_TEMPLATES"
  fi

  [[ -f "$TEMPLATE_DIR/version.txt" ]] || fail "matching export templates are missing at $TEMPLATE_DIR."
  ACTUAL_TEMPLATES="$(tr -d '\r\n' < "$TEMPLATE_DIR/version.txt")"
  [[ "$ACTUAL_TEMPLATES" == "$EXPECTED_TEMPLATES" ]] || fail "expected templates ${EXPECTED_TEMPLATES}, found ${ACTUAL_TEMPLATES}."
fi

printf 'Toolchain OK: .NET %s, Godot %s, Git LFS available.\n' "$ACTUAL_DOTNET" "$ACTUAL_GODOT"
