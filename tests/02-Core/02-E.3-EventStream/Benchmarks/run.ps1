# Builds the benchmarks against a baseline commit and against the working tree,
# then runs every scenario on both builds in turn and prints the change.
# Values from different runs are not comparable, so the baseline is always measured again.
#
# Usage:
#   ./run.ps1 -Baseline <commit> [-Quick] [-Runs 2] [-Affinity 0x3] [-Output <folder>]

param(
    [string]$Baseline = 'HEAD',
    [switch]$Quick,
    [int]$Runs = 2,
    [string]$Affinity = '0x3',
    [string]$Output
)

$ErrorActionPreference = 'Stop'
$culture = [System.Globalization.CultureInfo]::InvariantCulture
$benchDir = $PSScriptRoot
$project = Join-Path $benchDir 'EventStreamBenchmarks.csproj'
$root = (git -C $benchDir rev-parse --show-toplevel).Trim()
$baselineCommit = (git -C $root rev-parse --short $Baseline).Trim()
$currentCommit = (git -C $root rev-parse --short HEAD).Trim()
$dirty = [bool](git -C $root status --porcelain)

if (-not $Output) {
    $Output = Join-Path $benchDir ('results/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
New-Item -ItemType Directory -Force -Path $Output | Out-Null
$work = Join-Path ([System.IO.Path]::GetTempPath()) ('eventstream-bench-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $work | Out-Null

function Invoke-Build([string]$projectPath, [string]$outputPath, [string[]]$extra) {
    $arguments = @('build', $projectPath, '-c', 'Release', '-o', $outputPath, '-nologo', '-v', 'q') + $extra
    & dotnet @arguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $projectPath" }
}

try {
    # Current: the working tree as it is, including uncommitted changes.
    Invoke-Build $project (Join-Path $work 'current') @()

    # Baseline: the library and build settings of the given commit, measured by these same scenarios.
    $tracked = git -C $root ls-tree --name-only $Baseline
    $paths = @('src', 'Directory.Build.props', 'NuGet.config', 'packages') | Where-Object { $tracked -contains $_ }
    $archive = Join-Path $work 'baseline.zip'
    $source = Join-Path $work 'baseline-source'
    git -C $root archive --format=zip -o $archive $Baseline -- @paths
    if ($LASTEXITCODE -ne 0) { throw "Could not read the baseline commit: $Baseline" }
    Expand-Archive -Path $archive -DestinationPath $source

    $benchCopy = Join-Path $source 'bench'
    New-Item -ItemType Directory -Force -Path $benchCopy | Out-Null
    Get-ChildItem -Path $benchDir -File | Where-Object { $_.Extension -in '.cs', '.csproj' } |
        Copy-Item -Destination $benchCopy
    $library = Join-Path $source 'src/UniEngine.csproj'
    Invoke-Build (Join-Path $benchCopy 'EventStreamBenchmarks.csproj') (Join-Path $work 'baseline') @("-p:UniEngineProject=$library")

    $builds = @('baseline', 'current')
    $current = Join-Path $work 'current/EventStreamBenchmarks.dll'
    $listCommand = if ($Quick) { 'quick' } else { 'list' }
    $scenarios = @(& dotnet $current $listCommand)

    @(
        "baseline: $Baseline ($baselineCommit)"
        "current: working tree on $currentCommit$(if ($dirty) { ' with uncommitted changes' })"
        "date: $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
        "dotnet: $((dotnet --version).Trim())"
        "affinity: $Affinity"
        "runs: $Runs"
        "scenarios: $($scenarios.Count)"
    ) | Set-Content -Path (Join-Path $Output 'info.txt') -Encoding ascii

    # Alternate the builds inside each run so that a slow period affects both.
    for ($run = 1; $run -le $Runs; $run++) {
        foreach ($build in $builds) {
            $dll = Join-Path $work "$build/EventStreamBenchmarks.dll"
            $file = Join-Path $Output "$build-$run.csv"
            foreach ($scenario in $scenarios) {
                Write-Host "[$run/$Runs] $build $scenario"
                $line = & dotnet $dll $scenario $Affinity
                if ($LASTEXITCODE -ne 0) { throw "Scenario failed: $build $scenario" }
                Add-Content -Path $file -Value $line -Encoding ascii
            }
        }
    }

    # Summary: the mean of the runs for each build, the change and the spread between runs.
    $medians = @{}
    foreach ($build in $builds) {
        $medians[$build] = @{}
        for ($run = 1; $run -le $Runs; $run++) {
            foreach ($line in Get-Content (Join-Path $Output "$build-$run.csv")) {
                $parts = $line.Split(',')
                if (-not $medians[$build].ContainsKey($parts[0])) { $medians[$build][$parts[0]] = @() }
                $medians[$build][$parts[0]] += [double]::Parse($parts[1], $culture)
            }
        }
    }

    $summary = foreach ($scenario in $scenarios) {
        $before = $medians['baseline'][$scenario]
        $after = $medians['current'][$scenario]
        $beforeMean = ($before | Measure-Object -Average).Average
        $afterMean = ($after | Measure-Object -Average).Average
        [pscustomobject]@{
            Scenario = $scenario
            BaselineNs = [math]::Round($beforeMean, 1)
            CurrentNs = [math]::Round($afterMean, 1)
            ChangePercent = [math]::Round(($afterMean / $beforeMean - 1) * 100, 1)
            BaselineSpreadPercent = [math]::Round((($before | Measure-Object -Maximum).Maximum / ($before | Measure-Object -Minimum).Minimum - 1) * 100, 0)
            CurrentSpreadPercent = [math]::Round((($after | Measure-Object -Maximum).Maximum / ($after | Measure-Object -Minimum).Minimum - 1) * 100, 0)
        }
    }

    $summary | Export-Csv -Path (Join-Path $Output 'summary.csv') -NoTypeInformation -Encoding ascii
    $summary | Format-Table -AutoSize | Out-Host
    Write-Host "Results: $Output"
}
finally {
    $tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\', '/')
    $resolvedWork = [System.IO.Path]::GetFullPath($work)
    $workParent = [System.IO.Path]::GetDirectoryName($resolvedWork)
    $workName = [System.IO.Path]::GetFileName($resolvedWork)
    if ($workParent.Equals($tempRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
        $workName -match '^eventstream-bench-[0-9a-f]{32}$') {
        Remove-Item -Recurse -Force -LiteralPath $resolvedWork -ErrorAction SilentlyContinue
    }
    else {
        throw "Refusing to remove an unexpected benchmark directory: $resolvedWork"
    }
}
