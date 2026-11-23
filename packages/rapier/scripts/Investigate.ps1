param(
    [switch]$SkipBuild,
    [string]$OutputDirectory,
    [string]$TargetDirectory,
    [ValidateSet('world', 'zero-step')]
    [string]$TestCollisionPath = 'world'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'EnterBuildEnvironment.ps1')
$nativeRoot = Split-Path $PSScriptRoot -Parent
$experiment = Join-Path $nativeRoot 'investigation'
if ($SkipBuild -and -not $OutputDirectory) { throw 'SkipBuild requires an existing OutputDirectory.' }
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $nativeRoot ('obj/collision-update/runs/' + (Get-Date -Format 'yyyyMMdd-HHmmssfff'))
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
$target = 'x86_64-pc-windows-msvc'
if (-not $TargetDirectory) { $TargetDirectory = Join-Path $nativeRoot 'obj/collision-update/patched-target' }
$targetDirectory = [IO.Path]::GetFullPath($TargetDirectory)
if (-not $SkipBuild -and (Test-Path -LiteralPath $output)) {
    throw 'Use a new OutputDirectory to preserve previous investigation evidence.'
}
New-Item -ItemType Directory -Path $output -Force | Out-Null
$script:FailedChecks = @()
$env:UE_PROBE_TEST_COLLISION_PATH = $TestCollisionPath
$inputRoots = @($experiment, $PSScriptRoot, (Join-Path $nativeRoot 'revisions/0.36.1-r1'),
    (Join-Path $nativeRoot 'upstream/0.36.1'))
$inputFiles = @(Get-ChildItem -LiteralPath $inputRoots -Recurse -File |
    Where-Object { $_.Extension -in @('.rs', '.toml', '.lock', '.h', '.c', '.ps1', '.patch', '.json', '.crate') } |
    Sort-Object FullName | ForEach-Object {
        [ordered]@{
            path = [IO.Path]::GetRelativePath($nativeRoot, $_.FullName)
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    })
$inputJson = ConvertTo-Json -InputObject $inputFiles -Depth 3 -Compress
$inputSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
    [Text.Encoding]::UTF8.GetBytes($inputJson)))
if ($SkipBuild) {
    $previousManifest = Get-Content -LiteralPath (Join-Path $output 'binaries.json') -Raw | ConvertFrom-Json
    if ($previousManifest.inputSha256 -ne $inputSha256 -or
        $previousManifest.testCollisionPath -ne $TestCollisionPath) {
        throw 'Source inputs or comparison path changed; a fresh investigation is required.'
    }
}
$inputJson | Set-Content -LiteralPath (Join-Path $output 'inputs.json') -Encoding utf8
& (Join-Path $PSScriptRoot 'VerifyDependencies.ps1') -OutputDirectory $output

$preparedDirectory = Join-Path $output ('prepared-' + (Get-Date -Format 'yyyyMMdd-HHmmssfff'))
$prepared = & (Join-Path $PSScriptRoot 'PrepareRevision.ps1') -OutputDirectory $preparedDirectory
$manifest = $prepared.manifest
& (Join-Path $PSScriptRoot 'VerifyResolvedDependencies.ps1') -Manifest $manifest `
    -SourceDirectory $prepared.sources -OutputDirectory $output
if ($SkipBuild -and $previousManifest.patchSha256 -ne $prepared.patchSha256) {
    throw 'The revision patch differs from the previously tested DLLs.'
}

function Invoke-Recorded {
    param([string]$Name, [string]$Program, [string[]]$Arguments, [switch]$AllowFailure)
    $watch = [Diagnostics.Stopwatch]::StartNew()
    & $Program @Arguments 2>&1 | Tee-Object -FilePath (Join-Path $output "$Name.log")
    $exitCode = $LASTEXITCODE
    $watch.Stop()
    "exit=$exitCode elapsedSeconds=$($watch.Elapsed.TotalSeconds)" |
        Add-Content -LiteralPath (Join-Path $output "$Name.log")
    if ($exitCode -ne 0) {
        $script:FailedChecks += $Name
        if (-not $AllowFailure) { throw "$Name failed with exit code $exitCode." }
    }
}

$cargoArguments = @('--manifest-path', $manifest, '--workspace', '--release', '--locked', '--offline',
    '--target', $target, '--target-dir', $targetDirectory)
if (-not $SkipBuild) {
    Invoke-Recorded 'build' 'cargo' (@('build') + $cargoArguments)
    Invoke-Recorded 'rust-tests' 'cargo' (@('test', '--no-fail-fast') + $cargoArguments + @('--', '--nocapture')) -AllowFailure
    $debugArguments = @($cargoArguments | Where-Object { $_ -ne '--release' })
    Invoke-Recorded 'rust-debug-tests' 'cargo' (@('test', '--no-fail-fast') + $debugArguments +
        @('collision_', '--', '--nocapture', '--test-threads=1')) -AllowFailure
}
else {
    foreach ($testLog in 'rust-tests', 'rust-debug-tests') {
        $previousTests = Join-Path $output "$testLog.log"
        if (-not (Test-Path -LiteralPath $previousTests) -or
            (Get-Content -LiteralPath $previousTests -Tail 1) -notmatch '^exit=0 ') {
            $script:FailedChecks += $testLog
        }
    }
}

$expectedExports = @('ue_probe_callback', 'ue_probe_check_abi', 'ue_probe_get_abi',
    'ue_probe_last_error', 'ue_probe_panic', 'ue_probe_run')
$binaries = @()
New-Item -ItemType Directory -Path (Join-Path $output 'dlls') -Force | Out-Null
foreach ($dimension in 2, 3) {
    foreach ($precision in 32, 64) {
        $variant = "${dimension}d-f$precision"
        $dllName = "uniengine_rapier${dimension}d_f${precision}_probe.dll"
        $dll = Join-Path $output "dlls/$dllName"
        if (-not $SkipBuild) {
            Copy-Item -LiteralPath (Join-Path $targetDirectory "$target/release/$dllName") -Destination $dll
        }
        elseif ((Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash -ne
            ($previousManifest.binaries | Where-Object { $_.file -eq $dllName }).sha256) {
            throw "Previously verified DLL changed: $dllName."
        }
        Invoke-Recorded "exports-$variant" 'dumpbin' @('/exports', $dll)
        Invoke-Recorded "dependents-$variant" 'dumpbin' @('/dependents', $dll)
        $exportText = Get-Content -LiteralPath (Join-Path $output "exports-$variant.log") -Raw
        $exports = @([regex]::Matches($exportText, '(?m)^\s+\d+\s+[0-9A-F]+\s+[0-9A-F]+\s+(\w+)(?:\s+=.*)?\s*$') |
            ForEach-Object { $_.Groups[1].Value } | Sort-Object)
        if (@(Compare-Object $expectedExports $exports).Count -ne 0) {
            throw "Unexpected exports for $variant."
        }

        $consumer = Join-Path $output "consumer-$variant.exe"
        $compilerArguments = @('/nologo', '/std:c11', '/W4', '/WX', '/MD',
            "/DUE_PROBE_DIMENSION=$dimension", "/I$(Join-Path $experiment 'include')",
            (Join-Path $experiment 'tests/consumer.c'), "/Fe:$consumer",
            "/Fo:$(Join-Path $output "consumer-$variant.obj")")
        if ($precision -eq 64) { $compilerArguments += '/DUE_PROBE_F64' }
        Invoke-Recorded "compile-consumer-$variant" 'cl' $compilerArguments
        Invoke-Recorded "consumer-$variant" $consumer @($dll)
        $binaries += [ordered]@{
            variant = $variant
            file = [IO.Path]::GetFileName($dll)
            size = (Get-Item -LiteralPath $dll).Length
            sha256 = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
            exports = $exports
        }
    }
}

[ordered]@{
    purpose = 'Stage 0 investigation; not adopted by UniEngine'
    version = '0.0.1'
    collisionUpdate = 'physics-world-zero-dt-with-island-initialization'
    testCollisionPath = $TestCollisionPath
    upstreamModified = $true
    revision = $prepared.revision
    patchSha256 = $prepared.patchSha256
    provenanceSha256 = $prepared.provenanceSha256
    preparedDirectory = $preparedDirectory
    inputSha256 = $inputSha256
    target = $target
    rust = (& rustc --version)
    cargo = (& cargo --version)
    lockSha256 = (Get-FileHash -LiteralPath (Join-Path (Split-Path $manifest -Parent) 'Cargo.lock') -Algorithm SHA256).Hash
    failedChecks = $script:FailedChecks
    binaries = $binaries
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'binaries.json') -Encoding utf8

if ($script:FailedChecks.Count -ne 0) {
    throw "Completed independent checks; failures remain: $($script:FailedChecks -join ', ')."
}
