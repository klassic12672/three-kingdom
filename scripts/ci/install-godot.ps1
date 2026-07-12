$ErrorActionPreference = 'Stop'
if (-not $env:RUNNER_TEMP) { throw 'RUNNER_TEMP is required.' }

$Version = '4.6.1'
$TemplateVersion = '4.6.1.stable.mono'
$BaseUrl = "https://github.com/godotengine/godot-builds/releases/download/$Version-stable"
$Work = Join-Path $env:RUNNER_TEMP "godot-$Version"
$EditorArchive = Join-Path $Work 'editor.zip'
$TemplateArchive = Join-Path $Work 'templates.zip'
$EditorDirectory = Join-Path $Work 'editor'
$TemplateExtract = Join-Path $Work 'template-extract'
New-Item -ItemType Directory -Force $EditorDirectory, $TemplateExtract | Out-Null

Invoke-WebRequest "$BaseUrl/Godot_v$Version-stable_mono_win64.zip" -OutFile $EditorArchive
Invoke-WebRequest "$BaseUrl/Godot_v$Version-stable_mono_export_templates.tpz" -OutFile $TemplateArchive
Expand-Archive $EditorArchive -DestinationPath $EditorDirectory -Force
Expand-Archive $TemplateArchive -DestinationPath $TemplateExtract -Force

$Godot = Get-ChildItem $EditorDirectory -Recurse -File -Filter '*console.exe' | Select-Object -First 1
if (-not $Godot) { throw 'Godot console executable was not found after extraction.' }
$TemplateSource = Get-ChildItem $TemplateExtract -Recurse -Directory -Filter 'templates' | Select-Object -First 1
if (-not $TemplateSource) { throw 'Godot templates were not found after extraction.' }
$TemplateDestination = Join-Path $env:APPDATA "Godot/export_templates/$TemplateVersion"
New-Item -ItemType Directory -Force $TemplateDestination | Out-Null
Copy-Item "$($TemplateSource.FullName)/*" $TemplateDestination -Recurse -Force

if ($env:GITHUB_ENV) {
    "GODOT_BIN=$($Godot.FullName)" | Out-File $env:GITHUB_ENV -Encoding utf8 -Append
    "GODOT_EXPORT_TEMPLATES_DIR=$TemplateDestination" | Out-File $env:GITHUB_ENV -Encoding utf8 -Append
}
Write-Host "Installed Godot $Version and matching templates."
