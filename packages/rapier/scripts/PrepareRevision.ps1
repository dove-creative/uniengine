param([Parameter(Mandatory)][string]$OutputDirectory)

$ErrorActionPreference = 'Stop'
$nativeRoot = Split-Path $PSScriptRoot -Parent
$repositoryRoot = Split-Path (Split-Path $nativeRoot -Parent) -Parent
$revision = Join-Path $nativeRoot 'revisions/0.36.1-r1'
$provenance = Get-Content -LiteralPath (Join-Path $revision 'provenance.json') -Raw | ConvertFrom-Json
$output = [IO.Path]::GetFullPath($OutputDirectory)
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $nativeRoot 'obj')) + [IO.Path]::DirectorySeparatorChar
if (-not $output.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Revision preparation must use a new directory inside packages/rapier/obj.'
}
if (Test-Path -LiteralPath $output) { throw 'Revision output already exists; preserve previous evidence.' }

function Assert-Hash([string]$Path, [string]$Expected) {
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Expected) {
        throw "Revision checksum mismatch: $Path"
    }
}

$patch = Join-Path $revision $provenance.patch
Assert-Hash $patch $provenance.patchSha256
Assert-Hash (Join-Path $revision 'Cargo.lock') $provenance.lockSha256
Assert-Hash (Join-Path $nativeRoot 'investigation/Cargo.lock') $provenance.originalLockSha256
$expectedPackages = @('rapier2d', 'rapier2d-f64', 'rapier3d', 'rapier3d-f64')
if (@(Compare-Object $expectedPackages @($provenance.packages.package)).Count -ne 0) {
    throw 'Revision must contain the four pinned Rapier packages.'
}
foreach ($package in $provenance.packages) {
    if ($package.version -ne '0.36.1' -or $package.archive -ne "$($package.package)-0.36.1.crate") {
        throw 'Unexpected revision package identity.'
    }
    Assert-Hash (Join-Path $nativeRoot "upstream/0.36.1/$($package.archive)") $package.archiveSha256
}

New-Item -ItemType Directory -Path $output | Out-Null
$sources = Join-Path $output 'sources'
Add-Type -AssemblyName System.Formats.Tar
$records = @()
foreach ($package in $provenance.packages) {
    $source = Join-Path $sources "$($package.package)-0.36.1"
    $sourcePrefix = [IO.Path]::GetFullPath($source) + [IO.Path]::DirectorySeparatorChar
    $hashes = @{}
    $archive = Join-Path $nativeRoot "upstream/0.36.1/$($package.archive)"
    $stream = [IO.File]::OpenRead($archive)
    $gzip = [IO.Compression.GZipStream]::new($stream, [IO.Compression.CompressionMode]::Decompress)
    $reader = [System.Formats.Tar.TarReader]::new($gzip)
    try {
        while ($null -ne ($entry = $reader.GetNextEntry())) {
            $destination = [IO.Path]::GetFullPath((Join-Path $sources $entry.Name))
            if (-not $destination.StartsWith($sourcePrefix, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'Archive entry escapes its package directory.'
            }
            if ($entry.EntryType -eq 'Directory') { continue }
            if ($entry.EntryType -notin @('RegularFile', 'V7RegularFile')) { throw 'Unsupported archive entry.' }
            New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
            $file = [IO.File]::Create($destination)
            try { $entry.DataStream.CopyTo($file) } finally { $file.Dispose() }
            $relative = [IO.Path]::GetRelativePath($source, $destination).Replace('\', '/')
            $hashes[$relative] = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
        }
    }
    finally { $reader.Dispose(); $gzip.Dispose(); $stream.Dispose() }

    foreach ($change in $package.files) {
        if ($hashes[$change.path] -ne $change.originalSha256) { throw 'Patch input differs from its original.' }
        $hashes[$change.path] = $change.modifiedSha256
    }
    $prefix = [IO.Path]::GetRelativePath($repositoryRoot, $source).Replace('\', '/')
    & git -c core.autocrlf=false -C $repositoryRoot apply --check "--directory=$prefix" $patch
    if ($LASTEXITCODE -ne 0) { throw "Patch does not apply to $($package.package)." }
    & git -c core.autocrlf=false -C $repositoryRoot apply "--directory=$prefix" $patch
    if ($LASTEXITCODE -ne 0) { throw "Patch application failed for $($package.package)." }
    $sourceFiles = @(Get-ChildItem -LiteralPath $source -Recurse -File -Force)
    if ($sourceFiles.Count -ne $hashes.Count) { throw 'Unexpected patched source file count.' }
    foreach ($file in $sourceFiles) {
        $relative = [IO.Path]::GetRelativePath($source, $file.FullName).Replace('\', '/')
        Assert-Hash $file.FullName $hashes[$relative]
    }
    $records += [ordered]@{ package = $package.package; files = $hashes.Count; source = $source }
}

$workspace = Join-Path $output 'workspace'
$originalWorkspace = Join-Path $nativeRoot 'investigation'
foreach ($file in Get-ChildItem -LiteralPath $originalWorkspace -Recurse -File) {
    if ($file.Extension -notin @('.rs', '.toml', '.lock', '.h', '.c')) { continue }
    $destination = Join-Path $workspace ([IO.Path]::GetRelativePath($originalWorkspace, $file.FullName))
    New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination
}
$manifest = Join-Path $workspace 'Cargo.toml'
$text = [IO.File]::ReadAllText($manifest).TrimEnd() + "`n`n[patch.crates-io]`n"
foreach ($package in $provenance.packages) {
    $text += "$($package.package) = { path = `"../sources/$($package.package)-0.36.1`" }`n"
}
[IO.File]::WriteAllText($manifest, $text.Replace("`r`n", "`n"), [Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath (Join-Path $revision 'Cargo.lock') -Destination (Join-Path $workspace 'Cargo.lock')
$result = [ordered]@{
    revision = $provenance.revision
    patchSha256 = $provenance.patchSha256
    provenanceSha256 = (Get-FileHash -LiteralPath (Join-Path $revision 'provenance.json') -Algorithm SHA256).Hash
    manifest = $manifest
    sources = $sources
    packages = $records
}
$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'preparation.json') -Encoding utf8
[pscustomobject]$result
