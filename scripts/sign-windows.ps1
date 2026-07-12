[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Executable
)

$ErrorActionPreference = 'Stop'
if (-not $env:WINDOWS_CERTIFICATE_PATH) { throw 'WINDOWS_CERTIFICATE_PATH is required for release signing.' }
if (-not $env:WINDOWS_CERTIFICATE_PASSWORD) { throw 'WINDOWS_CERTIFICATE_PASSWORD is required for release signing.' }
$TimestampUrl = if ($env:WINDOWS_TIMESTAMP_URL) { $env:WINDOWS_TIMESTAMP_URL } else { 'http://timestamp.digicert.com' }
$SignTool = if ($env:SIGNTOOL_PATH) {
    $env:SIGNTOOL_PATH
} else {
    $Command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($Command) {
        $Command.Source
    } else {
        $SdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
        $Candidate = Get-ChildItem $SdkRoot -Recurse -File -Filter 'signtool.exe' -ErrorAction SilentlyContinue |
            Where-Object { $_.DirectoryName -match '[\\/]x64$' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if (-not $Candidate) { throw 'signtool.exe was not found; install the Windows SDK or set SIGNTOOL_PATH.' }
        $Candidate.FullName
    }
}

& $SignTool sign /fd SHA256 /td SHA256 /tr $TimestampUrl /f $env:WINDOWS_CERTIFICATE_PATH /p $env:WINDOWS_CERTIFICATE_PASSWORD $Executable
if ($LASTEXITCODE -ne 0) { throw 'Windows Authenticode signing failed.' }
& $SignTool verify /pa /all /v $Executable
if ($LASTEXITCODE -ne 0) { throw 'Windows Authenticode verification failed.' }

$Signature = Get-AuthenticodeSignature $Executable
if ($Signature.Status -ne 'Valid') {
    throw "Windows signature status is $($Signature.Status)."
}
Write-Host "Signed and verified $Executable"
