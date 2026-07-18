$ErrorActionPreference = 'Stop'
$env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"
Set-Location (Split-Path -Parent $MyInvocation.MyCommand.Path) | Out-Null
Set-Location ..

$out = Join-Path (Get-Location) 'artifacts/publish/win-x64'
dotnet publish src/Faktum.ScreenMarker.App/Faktum.ScreenMarker.App.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false -o $out --no-restore
exit $LASTEXITCODE
