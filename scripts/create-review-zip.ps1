$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $MyInvocation.MyCommand.Path) | Out-Null
Set-Location ..

$repoRoot = Get-Location
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$destDir = Join-Path $repoRoot 'artifacts/review-zips'
New-Item -ItemType Directory -Force -Path $destDir | Out-Null
$zipPath = Join-Path $destDir "faktum-screen-marker-review-$timestamp.zip"
$reportPath = Join-Path $destDir "faktum-screen-marker-review-$timestamp.md"

$staging = Join-Path ([System.IO.Path]::GetTempPath()) "faktum-sm-staging-$timestamp"
$archExit = $null
$restoreExit = $null
$buildExit = $null
$testExit = $null
$publishExit = $null
$smokeExit = $null
$platformSmokeExit = $null
$testSummary = 'not run'
$testOutput = ''
$exePath = Join-Path $repoRoot 'artifacts/publish/win-x64/FaktumScreenMarker.exe'
$exeSize = 'n/a'
$exeSha256 = 'n/a'

function Invoke-RepoCommand {
    param(
        [string]$Label,
        [scriptblock]$Command
    )

    Write-Host ">> $Label"
    $output = & $Command 2>&1 | Out-String
    if ($output.Trim().Length -gt 0) {
        Write-Host $output
    }

    if ($null -eq $LASTEXITCODE) {
        return 0, $output
    }

    return [int]$LASTEXITCODE, $output
}

try {
    if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
    New-Item -ItemType Directory -Path $staging | Out-Null

    $restoreExit, $_ = Invoke-RepoCommand 'dotnet restore --locked-mode' { dotnet restore --locked-mode }
    if ($restoreExit -ne 0) { throw "restore failed with exit code $restoreExit" }

    $buildExit, $_ = Invoke-RepoCommand 'dotnet build Release' { dotnet build Faktum.ScreenMarker.slnx -c Release --no-restore }
    if ($buildExit -ne 0) { throw "build failed with exit code $buildExit" }

    $testExit, $testOutput = Invoke-RepoCommand 'dotnet test Release' { dotnet test Faktum.ScreenMarker.slnx -c Release --no-build --logger 'console;verbosity=minimal' }
    if ($testExit -ne 0) { throw "tests failed with exit code $testExit" }

    if ($testOutput -match 'Total tests:\s*(\d+).*Passed:\s*(\d+).*Failed:\s*(\d+).*Skipped:\s*(\d+)') {
        $testSummary = "Total=$($Matches[1]); Passed=$($Matches[2]); Failed=$($Matches[3]); Skipped=$($Matches[4])"
    }
    else {
        $testSummary = ($testOutput -split "`n" | Select-Object -Last 5) -join ' '
    }

    $archExit, $_ = Invoke-RepoCommand 'verify architecture consistency' { & (Join-Path $repoRoot 'scripts/verify-current-architecture.ps1') }
    if ($archExit -ne 0) { throw "architecture consistency check failed with exit code $archExit" }

    $publishExit, $_ = Invoke-RepoCommand 'publish win-x64' { & (Join-Path $repoRoot 'scripts/publish-win-x64.ps1') }
    if ($publishExit -ne 0) { throw "publish failed with exit code $publishExit" }

    if (Test-Path $exePath) {
        $exeSize = (Get-Item $exePath).Length
        $exeSha256 = (Get-FileHash -Path $exePath -Algorithm SHA256).Hash
    }

    $smokeExit, $_ = Invoke-RepoCommand 'smoke test published exe' { & $exePath '--smoke-test' }
    if ($smokeExit -ne 0) { throw "smoke test failed with exit code $smokeExit" }

    $platformSmokeExit, $_ = Invoke-RepoCommand 'platform smoke test published exe' { & $exePath '--platform-smoke-test' }
    if ($platformSmokeExit -ne 0) { throw "platform smoke test failed with exit code $platformSmokeExit" }

    $trackedFiles = @(git -C $repoRoot ls-files)
    $untrackedCandidates = @(git -C $repoRoot ls-files --others --exclude-standard)
    $packageFiles = @($trackedFiles + $untrackedCandidates) | Sort-Object -Unique

    foreach ($relative in $packageFiles) {
        if ([string]::IsNullOrWhiteSpace($relative)) { continue }
        $source = Join-Path $repoRoot $relative
        if (-not (Test-Path $source -PathType Leaf)) { continue }
        if ($relative -match '(^|/|\\)(bin|obj|artifacts/publish|artifacts/review-zips|\.git|\.vs|\.idea|TestResults|coverage|node_modules|packages)(/|\\|$)') { continue }
        if ($relative -match '\.(zip|nupkg|pdf|docx)$') { continue }
        if ($relative -match '\.(user|suo)$') { continue }
        if ($relative -like '*.local.json') { continue }

        $target = Join-Path $staging $relative
        New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
        Copy-Item $source $target
    }

    $gitStatus = git -C $repoRoot status --short 2>&1 | Out-String
    $changedFiles = git -C $repoRoot diff --name-only 2>&1 | Out-String
    $untrackedFiles = git -C $repoRoot ls-files --others --exclude-standard 2>&1 | Out-String

    $report = @"
# Faktum Screen Marker review report

Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')

## Git status --short

``````
$gitStatus
``````

## Changed tracked files

``````
$changedFiles
``````

## Untracked files

``````
$untrackedFiles
``````

## Verification

| Step | Command | Exit code |
|------|---------|-----------|
| Restore | ``dotnet restore --locked-mode`` | $restoreExit |
| Build | ``dotnet build Faktum.ScreenMarker.slnx -c Release --no-restore`` | $buildExit |
| Test | ``dotnet test Faktum.ScreenMarker.slnx -c Release --no-build`` | $testExit |
| Architecture | ``./scripts/verify-current-architecture.ps1`` | $archExit |
| Publish | ``./scripts/publish-win-x64.ps1`` | $publishExit |
| Smoke test | ``./artifacts/publish/win-x64/FaktumScreenMarker.exe --smoke-test`` | $smokeExit |
| Platform smoke | ``./artifacts/publish/win-x64/FaktumScreenMarker.exe --platform-smoke-test`` | $platformSmokeExit |

## Test totals

$testSummary

## Published executable

- Path: ``$exePath``
- Size (bytes): $exeSize
- SHA-256: $exeSha256

## Manual tests outstanding

See ``docs/manual-test-plan.md``. Manual GUI scenarios (M1–M19) were not executed in the packaging environment.

## Known limitations

- Hotkey registration may conflict with other applications using the same combination.
- Multi-monitor and mixed-DPI behavior requires hardware verification (M7, M8).
- Code signing and installer packaging are out of scope for this review ZIP.
"@

    Set-Content -Path $reportPath -Value $report -Encoding utf8BOM
    Copy-Item $reportPath (Join-Path $staging 'REVIEW-REPORT.md')

    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zipPath -Force
}
catch {
    Write-Error "Failed to create review ZIP: $_"
    exit 1
}
finally {
    if (Test-Path $staging) { Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue }
}

if (-not (Test-Path $zipPath)) {
    Write-Error "ZIP was not created: $zipPath"
    exit 1
}

Write-Host ''
Write-Host 'Review ZIP created:' -ForegroundColor Green
Write-Host $zipPath
Write-Host 'Review report:' -ForegroundColor Green
Write-Host $reportPath
Write-Host ''
Write-Output $zipPath
exit 0
