[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('windows', 'macos')]
    [string]$Platform,
    [ValidateSet('development', 'release')]
    [string]$Flavor = 'development'
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$GodotCommand = if ($env:GODOT_BIN) { $env:GODOT_BIN } else { 'godot' }

$Settings = switch ("${Platform}:${Flavor}") {
    'windows:development' { @('Windows x64 Development', 'windows-x64-development', 'ThreeKingdom.exe', 'x86_64', 'Development', '--export-debug') }
    'windows:release' { @('Windows x64 Release', 'windows-x64-release', 'ThreeKingdom.exe', 'x86_64', 'Release', '--export-release') }
    'macos:development' { @('macOS arm64 Development', 'macos-arm64-development', 'ThreeKingdom.app', 'arm64', 'Development', '--export-debug') }
    'macos:release' { @('macOS arm64 Release', 'macos-arm64-release', 'ThreeKingdom.app', 'arm64', 'Release', '--export-release') }
}

$Preset, $DirectoryName, $FileName, $Architecture, $Configuration, $ExportFlag = $Settings
$OutputDirectory = Join-Path $Root "artifacts/exports/$DirectoryName"
$Output = Join-Path $OutputDirectory $FileName
$Manifest = Join-Path $Root 'game/generated/build-manifest.json'

& "$PSScriptRoot/preflight.ps1" -RequireTemplates
if ($Platform -eq 'macos') {
    & "$PSScriptRoot/prepare-macos-arm64-template.sh"
    if ($LASTEXITCODE -ne 0) { throw 'macOS arm64 template preparation failed.' }
}
& "$PSScriptRoot/test.ps1" -Configuration Release
& "$PSScriptRoot/import.ps1"
Remove-Item -Recurse -Force $OutputDirectory -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null

dotnet run --project "$Root/tools/Tools.ContentPipeline/Tools.ContentPipeline.csproj" --configuration Release --no-build -- manifest `
    --repository-root $Root `
    --platform $Platform `
    --architecture $Architecture `
    --configuration $Configuration `
    --output $Manifest
if ($LASTEXITCODE -ne 0) { throw 'Build manifest generation failed.' }

& $GodotCommand --headless --path "$Root/game" $ExportFlag $Preset $Output
if ($LASTEXITCODE -ne 0) { throw 'Godot export failed.' }
if ($Platform -eq 'macos') {
    $AppExecutable = Get-ChildItem "$Output/Contents/MacOS" -File | Where-Object { $_.UnixFileMode -band [IO.UnixFileMode]::UserExecute } | Select-Object -First 1
    if (-not $AppExecutable) { throw 'No macOS executable was produced.' }
    $Architectures = (& lipo -archs $AppExecutable.FullName).Trim()
    if ($Architectures -ne 'arm64') { throw 'macOS export is not arm64-only.' }
}
Copy-Item $Manifest (Join-Path $OutputDirectory 'build-manifest.json')
Write-Host "Exported $Preset to $Output"
