$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot

& "$PSScriptRoot/preflight.ps1"
dotnet restore "$Root/ThreeKingdom.slnx" --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }
dotnet run --project "$Root/tools/Tools.ContentPipeline/Tools.ContentPipeline.csproj" --no-restore -- validate --repository-root $Root
if ($LASTEXITCODE -ne 0) { throw 'repository validation failed.' }
