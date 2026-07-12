[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
& "$PSScriptRoot/build.ps1" -Configuration $Configuration
dotnet test "$Root/ThreeKingdom.slnx" --configuration $Configuration --no-build --no-restore
if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed.' }
