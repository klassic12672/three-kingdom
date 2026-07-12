[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
& "$PSScriptRoot/validate.ps1"
dotnet run --project "$Root/tools/Tools.ContentPipeline/Tools.ContentPipeline.csproj" --no-restore -- content geography `
    --repository-root $Root `
    --output "$Root/game/generated/geography-191.json"
if ($LASTEXITCODE -ne 0) { throw 'geography artifact generation failed.' }
dotnet build "$Root/ThreeKingdom.slnx" --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }
