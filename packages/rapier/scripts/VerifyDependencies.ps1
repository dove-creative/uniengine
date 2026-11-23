param([string]$OutputDirectory)

$ErrorActionPreference = 'Stop'
$nativeRoot = Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $nativeRoot ('obj/dependency-check/' + (Get-Date -Format 'yyyyMMdd-HHmmssfff'))
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$cargoRoot = Join-Path $nativeRoot 'obj/tools/cargo'
$lockText = Get-Content -LiteralPath (Join-Path $nativeRoot 'investigation/Cargo.lock') -Raw
$registrySource = Join-Path $cargoRoot 'registry/src/index.crates.io-1949cf8c6b5b557f'
$registryCache = Join-Path $cargoRoot 'registry/cache/index.crates.io-1949cf8c6b5b557f'
Add-Type -AssemblyName System.Formats.Tar
$records = @()

foreach ($package in ($lockText -split '\[\[package\]\]')) {
    if ($package -notmatch '(?m)^name = "((?:rapier|parry)[23]d(?:-f64)?)"$') { continue }
    $name = $Matches[1]
    if ($package -notmatch '(?m)^version = "([^"]+)"$') { throw "Missing version for $name." }
    $version = $Matches[1]
    if ($package -notmatch '(?m)^checksum = "([a-f0-9]+)"$') { throw "Missing checksum for $name." }
    $expected = $Matches[1]
    $directoryName = "$name-$version"
    $source = Join-Path $registrySource $directoryName
    $archive = Join-Path $registryCache "$directoryName.crate"
    $actual = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
    if ($actual -ne $expected) { throw "Archive checksum mismatch: $directoryName." }

    $stream = [IO.File]::OpenRead($archive)
    $gzip = [IO.Compression.GZipStream]::new($stream, [IO.Compression.CompressionMode]::Decompress)
    $reader = [System.Formats.Tar.TarReader]::new($gzip)
    $hasher = [Security.Cryptography.SHA256]::Create()
    $fileCount = 0
    try {
        while ($null -ne ($entry = $reader.GetNextEntry())) {
            if ($entry.EntryType -notin @('RegularFile', 'V7RegularFile')) { continue }
            $sourceFile = [IO.Path]::GetFullPath((Join-Path $registrySource $entry.Name))
            $sourcePrefix = [IO.Path]::GetFullPath($source) + [IO.Path]::DirectorySeparatorChar
            if (-not $sourceFile.StartsWith($sourcePrefix, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'Archive entry escapes the expected package directory.'
            }
            $archiveHash = [Convert]::ToHexString($hasher.ComputeHash($entry.DataStream))
            $sourceHash = (Get-FileHash -LiteralPath $sourceFile -Algorithm SHA256).Hash
            if ($archiveHash -ne $sourceHash) { throw "Extracted source differs from archive: $sourceFile" }
            ++$fileCount
        }
    }
    finally {
        $hasher.Dispose()
        $reader.Dispose()
        $gzip.Dispose()
        $stream.Dispose()
    }

    $vcs = Get-Content -LiteralPath (Join-Path $source '.cargo_vcs_info.json') -Raw | ConvertFrom-Json
    $records += [pscustomobject][ordered]@{
        package = $name
        version = $version
        sha256 = $actual
        publisherGitCommit = $vcs.git.sha1
        publisherDirty = $vcs.git.dirty
        verifiedSourceFiles = $fileCount
    }
}

if ($records.Count -ne 8) { throw 'Expected eight exact-version Rapier/Parry packages.' }
$records | ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath (Join-Path $OutputDirectory 'dependencies.json') -Encoding utf8
$records | Format-Table package, version, publisherGitCommit, publisherDirty, verifiedSourceFiles
