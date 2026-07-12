#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIGURATION="${1:-Debug}"
"$ROOT/scripts/build.sh" "$CONFIGURATION"
dotnet test "$ROOT/ThreeKingdom.slnx" --configuration "$CONFIGURATION" --no-build --no-restore
