param(
    [Parameter(Mandatory)][string]$Manifest,
    [Parameter(Mandatory)][string]$SourceDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$metadataText = & cargo metadata --manifest-path $Manifest --locked --offline --format-version 1
if ($LASTEXITCODE -ne 0) { throw 'Cargo dependency resolution failed.' }
$metadata = $metadataText | ConvertFrom-Json
$packages = @($metadata.packages | Where-Object { $_.name -match '^(rapier|parry)[23]d(-f64)?$' })
if ($packages.Count -ne 8) { throw 'Expected eight resolved Rapier/Parry packages.' }
foreach ($package in $packages) {
    if ($package.name.StartsWith('rapier')) {
        $version = '0.36.1'
        $expected = Join-Path $SourceDirectory "$($package.name)-$version/Cargo.toml"
        if ($null -ne $package.source) { throw 'Rapier resolved to registry instead of the revision.' }
    }
    else {
        $version = '0.31.1'
        $expected = Join-Path $env:CARGO_HOME "registry/src/index.crates.io-1949cf8c6b5b557f/$($package.name)-$version/Cargo.toml"
        if ($package.source -notlike 'registry+*') { throw 'Parry must remain an unmodified registry dependency.' }
    }
    if ($package.version -ne $version -or
        [IO.Path]::GetFullPath($package.manifest_path) -ne [IO.Path]::GetFullPath($expected)) {
        throw "Unexpected dependency source: $($package.name)."
    }
}
$metadataText | Set-Content -LiteralPath (Join-Path $OutputDirectory 'cargo-metadata.json') -Encoding utf8
