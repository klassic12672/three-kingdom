[CmdletBinding()]
param(
    [switch]$RequireTemplates
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$ExpectedDotnet = '10.0.301'
$ExpectedGodot = '4.6.1.stable.mono.official.14d19694e'
$ExpectedTemplates = '4.6.1.stable.mono'
$GodotCommand = if ($env:GODOT_BIN) { $env:GODOT_BIN } else { 'godot' }

function Fail([string]$Message) {
    throw "preflight error: $Message"
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Fail "dotnet is missing; install SDK $ExpectedDotnet."
}
$ActualDotnet = (dotnet --version).Trim()
if ($ActualDotnet -ne $ExpectedDotnet) {
    Fail "expected .NET SDK $ExpectedDotnet, found $ActualDotnet."
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Fail 'git is missing.'
}
git lfs version *> $null
if ($LASTEXITCODE -ne 0) {
    Fail "Git LFS is missing; install it and run 'git lfs install'."
}

if (-not (Get-Command $GodotCommand -ErrorAction SilentlyContinue)) {
    Fail "Godot is missing; set GODOT_BIN or install Godot $ExpectedGodot."
}
$ActualGodot = (& $GodotCommand --version).Trim()
if ($ActualGodot -ne $ExpectedGodot) {
    Fail "expected Godot $ExpectedGodot, found $ActualGodot."
}

if ($RequireTemplates) {
    $TemplateDir = if ($env:GODOT_EXPORT_TEMPLATES_DIR) {
        $env:GODOT_EXPORT_TEMPLATES_DIR
    } elseif ($IsWindows) {
        Join-Path $env:APPDATA "Godot/export_templates/$ExpectedTemplates"
    } elseif ($IsMacOS) {
        Join-Path $HOME "Library/Application Support/Godot/export_templates/$ExpectedTemplates"
    } else {
        Join-Path $HOME ".local/share/godot/export_templates/$ExpectedTemplates"
    }

    $VersionFile = Join-Path $TemplateDir 'version.txt'
    if (-not (Test-Path $VersionFile)) {
        Fail "matching export templates are missing at $TemplateDir."
    }
    $ActualTemplates = (Get-Content -Raw $VersionFile).Trim()
    if ($ActualTemplates -ne $ExpectedTemplates) {
        Fail "expected templates $ExpectedTemplates, found $ActualTemplates."
    }
}

Write-Host "Toolchain OK: .NET $ActualDotnet, Godot $ActualGodot, Git LFS available."
