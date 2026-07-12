[CmdletBinding()]
param(
    [ValidateSet('windows')]
    [string]$Platform = 'windows'
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$Executable = Join-Path $Root 'artifacts/exports/windows-x64-development/ThreeKingdom.exe'
if (-not (Test-Path $Executable)) {
    & "$PSScriptRoot/export.ps1" -Platform windows -Flavor development
}

$Process = Start-Process -FilePath $Executable -ArgumentList '--headless', '--', '--smoke-test' -PassThru -Wait -NoNewWindow
if ($Process.ExitCode -ne 0) {
    throw "Windows x64 smoke build exited with code $($Process.ExitCode)."
}
Write-Host 'Windows x64 smoke build launched successfully.'
