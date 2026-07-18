$ErrorActionPreference = 'Stop'
$env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"
Set-Location (Split-Path -Parent $MyInvocation.MyCommand.Path) | Out-Null
Set-Location ..

dotnet restore --locked-mode
if ($LASTEXITCODE -ne 0) {
    dotnet restore
    dotnet restore --locked-mode
}
dotnet build Faktum.ScreenMarker.slnx -c Release --no-restore
exit $LASTEXITCODE
