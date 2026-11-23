$ErrorActionPreference = 'Stop'
$nativeRoot = Split-Path $PSScriptRoot -Parent
$env:CARGO_HOME = Join-Path $nativeRoot 'obj/tools/cargo'
$env:RUSTUP_HOME = Join-Path $nativeRoot 'obj/tools/rustup'
$env:RUSTUP_TOOLCHAIN = '1.99.1-x86_64-pc-windows-msvc'
$env:PATH = (Join-Path $env:CARGO_HOME 'bin') + [IO.Path]::PathSeparator + $env:PATH

if (-not (Test-Path -LiteralPath (Join-Path $env:CARGO_HOME 'bin/cargo.exe'))) {
    throw 'Install the approved Rust toolchain before running this script.'
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$visualStudioPath = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if ($LASTEXITCODE -ne 0 -or -not $visualStudioPath) {
    throw 'The Windows x64 investigation requires the installed MSVC tools.'
}

Import-Module (Join-Path $visualStudioPath 'Common7/Tools/Microsoft.VisualStudio.DevShell.dll')
Enter-VsDevShell -VsInstallPath $visualStudioPath -SkipAutomaticLocation -DevCmdArguments '-arch=x64 -host_arch=x64' | Out-Null
