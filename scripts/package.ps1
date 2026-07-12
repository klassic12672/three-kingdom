[CmdletBinding()]
param(
    [ValidateSet('windows')]
    [string]$Platform = 'windows'
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$Version = (Get-Content -Raw "$Root/build/version.json" | ConvertFrom-Json).projectVersion
$ExpectedTag = "v$Version"
$ActualTag = (git -C $Root describe --tags --exact-match HEAD 2>$null)
if ($env:ALLOW_UNTAGGED_RELEASE -ne '1' -and $ActualTag -ne $ExpectedTag) {
    throw "release packaging requires exact tag $ExpectedTag; current tag is $(if ($ActualTag) { $ActualTag } else { 'none' })."
}

& "$PSScriptRoot/export.ps1" -Platform windows -Flavor release
$ExportDirectory = Join-Path $Root 'artifacts/exports/windows-x64-release'
$Executable = Join-Path $ExportDirectory 'ThreeKingdom.exe'
$RequireSigning = if ($env:REQUIRE_SIGNING) { $env:REQUIRE_SIGNING } else { '1' }
$Suffix = 'unsigned'
if ($RequireSigning -eq '1') {
    & "$PSScriptRoot/sign-windows.ps1" -Executable $Executable
    $Suffix = 'signed'
}

$Package = Join-Path $Root "artifacts/ThreeKingdom-$Version-windows-x64-$Suffix.zip"
Remove-Item -Force $Package -ErrorAction SilentlyContinue
Compress-Archive -Path "$ExportDirectory/*" -DestinationPath $Package -CompressionLevel Optimal
Copy-Item (Join-Path $ExportDirectory 'build-manifest.json') (Join-Path $Root "artifacts/ThreeKingdom-$Version-windows-x64-$Suffix-build-manifest.json")
Write-Host "Created auditable release package $Package"
