#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIGURATION="${1:-Debug}"
"$ROOT/scripts/validate.sh"
dotnet run --project "$ROOT/tools/Tools.ContentPipeline/Tools.ContentPipeline.csproj" --no-restore -- content geography \
  --repository-root "$ROOT" \
  --output "$ROOT/game/generated/geography-191.json"
dotnet build "$ROOT/ThreeKingdom.slnx" --configuration "$CONFIGURATION" --no-restore
