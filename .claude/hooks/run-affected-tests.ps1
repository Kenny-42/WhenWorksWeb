<#
.SYNOPSIS
Stop hook: runs xUnit tests for any Controllers/Services/Models source file that changed
during this session and hasn't been tested since, then reports pass/fail. Never blocks the
turn - always exits 0, regardless of test outcome.

See CODING_CONVENTIONS.md's Testing Conventions and Spec/Features/FEATURES-automated-test-system.ospec
Step 8 for why this exists: closes the loop on the rule that Controllers/Services/Models
changes must ship with test updates in the same change.
#>

$ErrorActionPreference = 'Stop'

# Resolve repo root (this script lives in <repo>/.claude/hooks/).
$repoRoot = Resolve-Path (Join-Path (Join-Path $PSScriptRoot '..') '..')
Set-Location $repoRoot

$stateFile = Join-Path $repoRoot '.claude/hooks/test-hook-state.json'
$testProject = 'WhenWorksWeb.Tests/WhenWorksWeb.Tests.csproj'
$watchDirs = @('WhenWorksWeb/Controllers', 'WhenWorksWeb/Services', 'WhenWorksWeb/Models')

function Write-HookMessage([string]$message) {
    # Stop hooks surface a systemMessage to the user when the command's stdout is this JSON shape.
    (@{ systemMessage = $message } | ConvertTo-Json -Compress) | Write-Output
}

try {
    # Load prior "last tested" state (path -> content hash). Missing/corrupt state is treated
    # as empty rather than failing the hook.
    $state = @{}
    if (Test-Path $stateFile) {
        try {
            $raw = Get-Content $stateFile -Raw | ConvertFrom-Json
            if ($raw) {
                foreach ($prop in $raw.PSObject.Properties) { $state[$prop.Name] = $prop.Value }
            }
        } catch {
            $state = @{}
        }
    }

    # Files with uncommitted changes (modified or untracked) under the watched directories.
    $statusLines = git status --porcelain -- $watchDirs 2>$null
    if (-not $statusLines) { exit 0 }

    $changedFiles = $statusLines |
        ForEach-Object { $_.Substring(3).Trim() } |
        Where-Object { $_ -like '*.cs' }

    if (-not $changedFiles) { exit 0 }

    # Only files whose content actually differs from what was recorded at last test run.
    $toTest = @()
    foreach ($file in $changedFiles) {
        $fullPath = Join-Path $repoRoot $file
        if (-not (Test-Path $fullPath)) { continue } # deleted/renamed away

        $hash = (Get-FileHash -Path $fullPath -Algorithm SHA256).Hash
        if ($state[$file] -ne $hash) {
            $toTest += [PSCustomObject]@{ Path = $file; Hash = $hash }
        }
    }

    if ($toTest.Count -eq 0) { exit 0 }

    # Map each changed source file to its mirrored-path test class(es). Partial classes (e.g.
    # EventsController.cs / .SignIn.cs / .Home.cs / .AccessCookie.cs) share one base name and
    # match by prefix against every WhenWorksWeb.Tests/<Dir>/<BaseName>*Tests.cs file.
    $classNames = New-Object System.Collections.Generic.HashSet[string]
    foreach ($item in $toTest) {
        $file = $item.Path
        $leaf = [System.IO.Path]::GetFileNameWithoutExtension($file)   # e.g. EventsController.SignIn
        $baseName = $leaf.Split('.')[0]                                # e.g. EventsController
        $sourceDirName = Split-Path (Split-Path $file -Parent) -Leaf   # Controllers / Services / Models
        $testDir = Join-Path $repoRoot "WhenWorksWeb.Tests/$sourceDirName"

        if (Test-Path $testDir) {
            Get-ChildItem -Path $testDir -Filter "$baseName*Tests.cs" -File -ErrorAction SilentlyContinue |
                ForEach-Object { $classNames.Add([System.IO.Path]::GetFileNameWithoutExtension($_.Name)) | Out-Null }
        }
    }

    if ($classNames.Count -eq 0) {
        # Changed file has no corresponding test file at all - nothing to run, but still worth
        # a nudge since CODING_CONVENTIONS.md requires one.
        $fileList = ($toTest | ForEach-Object { $_.Path }) -join ', '
        Write-HookMessage "No matching test class found for changed file(s): $fileList - CODING_CONVENTIONS.md requires test coverage for Controllers/Services/Models changes."
        exit 0
    }

    $filter = ($classNames | ForEach-Object { "FullyQualifiedName~$_" }) -join '|'
    $testOutput = & dotnet test $testProject --filter $filter --nologo 2>&1 | Out-String

    # Record these files as tested-at-this-content regardless of pass/fail - we DID test them;
    # a failure should be visible now, not repeated on every subsequent unrelated Stop.
    foreach ($item in $toTest) { $state[$item.Path] = $item.Hash }
    ($state | ConvertTo-Json) | Set-Content -Path $stateFile -Encoding utf8

    $summaryMatch = [regex]::Match($testOutput, '(Passed!|Failed!)[^\r\n]*')
    $summary = if ($summaryMatch.Success) { $summaryMatch.Value } else { "dotnet test exited without a summary line" }

    $classList = $classNames -join ', '
    Write-HookMessage "Auto-tested changed file(s) [$classList]: $summary"
}
catch {
    # A hook failure must never block the turn - report it and move on.
    Write-HookMessage "run-affected-tests hook error (non-blocking): $($_.Exception.Message)"
}

exit 0
