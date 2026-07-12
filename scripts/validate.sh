#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
"$ROOT/scripts/preflight.sh"
dotnet restore "$ROOT/ThreeKingdom.slnx" --locked-mode
dotnet run --project "$ROOT/tools/Tools.ContentPipeline/Tools.ContentPipeline.csproj" --no-restore -- validate --repository-root "$ROOT"
