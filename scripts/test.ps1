$ErrorActionPreference = 'Stop'
$env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"
Set-Location (Split-Path -Parent $MyInvocation.MyCommand.Path) | Out-Null
Set-Location ..

dotnet test Faktum.ScreenMarker.slnx -c Release --no-build
if ($LASTEXITCODE -ne 0) {
    dotnet test Faktum.ScreenMarker.slnx -c Release
}
exit $LASTEXITCODE
