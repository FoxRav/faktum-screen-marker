param(
    [switch]$SkipZip
)

$ErrorActionPreference = 'Stop'
$env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

& (Join-Path $scriptDir 'build.ps1')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& (Join-Path $scriptDir 'test.ps1')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $SkipZip) {
    & (Join-Path $scriptDir 'create-review-zip.ps1')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

exit 0
