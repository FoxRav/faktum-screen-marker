$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $MyInvocation.MyCommand.Path) | Out-Null
Set-Location ..

$repoRoot = Get-Location
$obsoletePatterns = @(
    @{ Pattern = '§\s*\+\s*1\s*\+\s*2'; Label = 'three-key chord (§ + 1 + 2)' },
    @{ Pattern = '§\+1\+2'; Label = 'three-key chord (§+1+2)' },
    @{ Pattern = 'WH_KEYBOARD_LL'; Label = 'low-level keyboard hook' },
    @{ Pattern = 'SendInput'; Label = 'SendInput replay' },
    @{ Pattern = 'suppression/replay'; Label = 'suppression/replay' },
    @{ Pattern = 'three-key chord'; Label = 'three-key chord' },
    @{ Pattern = '0002-low-level-keyboard-hook\.md'; Label = 'obsolete ADR filename' }
)

$searchRoots = @(
    (Join-Path $repoRoot 'COMPOSER_MASTER_IMPLEMENTATION_BRIEF.md'),
    (Join-Path $repoRoot 'README.md'),
    (Join-Path $repoRoot 'docs'),
    (Join-Path $repoRoot 'artifacts/reference'),
    (Join-Path $repoRoot 'src')
)

$allowedHistoricalMarkers = @(
    'historical',
    'obsolete',
    'superseded',
    'retained for context',
    'not implemented',
    'replacement ADR'
)

$violations = @()

foreach ($root in $searchRoots) {
    if (-not (Test-Path $root)) { continue }

    $files = if (Test-Path $root -PathType Leaf) {
        @(Get-Item $root)
    }
    else {
        Get-ChildItem -Path $root -Recurse -File -Include *.md,*.cs,*.xaml,*.ps1,*.yml,*.yaml
    }

    foreach ($file in $files) {
        $relative = $file.FullName.Substring($repoRoot.Path.Length).TrimStart('\', '/')
        if ($relative -match '(\\|/)(bin|obj)(\\|/)') { continue }

        $lines = Get-Content -Path $file.FullName -Encoding UTF8
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]
            foreach ($entry in $obsoletePatterns) {
                if ($line -notmatch $entry.Pattern) { continue }

                $lowerLine = $line.ToLowerInvariant()
                $isHistorical = $false
                foreach ($marker in $allowedHistoricalMarkers) {
                    if ($lowerLine.Contains($marker)) {
                        $isHistorical = $true
                        break
                    }
                }

                if ($relative -eq 'docs/adr/0002-register-hotkey-activation.md') {
                    continue
                }

                if ($lowerLine -match '\b(no|not|without|never|exclude|removed|reject|avoid)\b.*(hook|sendinput|suppression|replay|three-key|§\+1\+2|§\s*\+\s*1)') {
                    continue
                }

                if ($lowerLine -match '(hook|sendinput|suppression|replay|three-key|§\+1\+2|§\s*\+\s*1).*\b(no|not|without|never|obsolete|superseded|removed)\b') {
                    continue
                }

                if (-not $isHistorical) {
                    $violations += [pscustomobject]@{
                        File = $relative
                        Line = $i + 1
                        Term = $entry.Label
                        Text = $line.Trim()
                    }
                }
            }
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Host 'Architecture consistency check FAILED.' -ForegroundColor Red
    $violations | Format-Table -AutoSize | Out-String | Write-Host
    exit 1
}

Write-Host 'Architecture consistency check passed.' -ForegroundColor Green
exit 0
