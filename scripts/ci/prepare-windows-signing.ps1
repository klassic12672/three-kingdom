$ErrorActionPreference = 'Stop'
if (-not $env:RUNNER_TEMP) { throw 'RUNNER_TEMP is required.' }
if (-not $env:WINDOWS_CERTIFICATE_BASE64) { throw 'WINDOWS_CERTIFICATE_BASE64 is required.' }
if (-not $env:WINDOWS_CERTIFICATE_PASSWORD) { throw 'WINDOWS_CERTIFICATE_PASSWORD is required.' }

$CertificatePath = Join-Path $env:RUNNER_TEMP 'release-signing.pfx'
[IO.File]::WriteAllBytes($CertificatePath, [Convert]::FromBase64String($env:WINDOWS_CERTIFICATE_BASE64))
if ($env:GITHUB_ENV) {
    "WINDOWS_CERTIFICATE_PATH=$CertificatePath" | Out-File $env:GITHUB_ENV -Encoding utf8 -Append
}
Write-Host 'Ephemeral Windows signing certificate is ready.'
