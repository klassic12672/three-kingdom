$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$GodotCommand = if ($env:GODOT_BIN) { $env:GODOT_BIN } else { 'godot' }

& "$PSScriptRoot/preflight.ps1"
& $GodotCommand --headless --path "$Root/game" --import
if ($LASTEXITCODE -ne 0) { throw 'Godot headless import failed.' }
