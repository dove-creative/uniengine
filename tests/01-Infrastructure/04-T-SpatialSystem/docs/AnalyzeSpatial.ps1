# Checks the SpatialSystem sources with Roslyn diagnostics only: nothing is emitted, built or run.
# The boundary check compiles the Unity wrappers and tests against the common sources as a separate assembly,
# so it also finds uses of internal members.
#
# Requires PowerShell 7, the .NET reference packs and, for the Unity modes, the named Unity editors.
# -IncludeTests also needs a restored tests project. The Unity tests are included only when the NUnit and
# TestRunner assemblies of a Unity project that has the Test Framework package are given.
param(
    [switch]$IncludeTests,
    [string]$UnityEditors = 'C:/Program Files/Unity/Hub/Editor',
    [string[]]$UnityVersions = @('2022.3.60f1', '6000.3.18f1'),
    [string]$UnityNUnit,
    [string]$UnityTestRunner
)

$ErrorActionPreference = 'Stop'
$null = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]

$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../../..')).Path
$packs = Join-Path (Split-Path -Parent (Get-Command dotnet).Source) 'packs'
$module = 'src/01-Infrastructure'

function Get-Sources([string]$path) {
    Get-ChildItem -LiteralPath (Join-Path $root $path) -Recurse -Filter '*.cs'
}

$sources = @(Get-Sources "$module/04-T-SpatialSystem")
$sources += @(Get-Sources "$module/01-T.1-Math")
$sources += @(Get-Item -LiteralPath (Join-Path $root "$module/02-O.1-Objects/Transform2D.cs"))
$sources += @(Get-Item -LiteralPath (Join-Path $root "$module/02-O.1-Objects/Transform3D.cs"))
$sources += @(Get-Item -LiteralPath (Join-Path $root "$module/03-M.2-ExceptionHandler/ExceptionHandler.cs"))

$tests = @()
$testReferences = @()
if ($IncludeTests) {
    $tests += @(Get-Sources 'tests/01-Infrastructure/04-T-SpatialSystem')
    $tests += @(Get-Item -LiteralPath (Join-Path $root 'tests/Support/RandomContinuousTest.cs'))
    $tests += @(Get-Item -LiteralPath (Join-Path $root 'tests/Support/InfrastructureContinuousProject.cs'))

    $assetsPath = Join-Path $root 'tests/obj/project.assets.json'
    if (-not (Test-Path -LiteralPath $assetsPath)) { throw 'Restore tests/UniEngine.Tests.csproj first.' }

    $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json -AsHashtable
    $target = @($assets.targets.Values)[0]
    foreach ($name in $target.Keys) {
        $library = $assets.libraries[$name]
        if ($library.type -ne 'package' -or -not $target[$name].ContainsKey('compile')) { continue }

        foreach ($relative in $target[$name].compile.Keys) {
            if ((Split-Path -Leaf $relative) -notin 'BlackThunder.UniTest.dll', 'nunit.framework.dll') { continue }

            foreach ($folder in $assets.packageFolders.Keys) {
                $candidate = Join-Path $folder ($library.path + '/' + $relative)
                if (Test-Path -LiteralPath $candidate) { $testReferences += Get-Item -LiteralPath $candidate; break }
            }
        }
    }
}

function Get-Diagnostics([string]$label, $compilation) {
    $diagnostics = @($compilation.GetDiagnostics() | Where-Object { $_.Severity -in @('Error', 'Warning') })
    $errors = @($diagnostics | Where-Object { $_.Severity -eq 'Error' })
    Write-Output "$label : $($errors.Count) errors; $($diagnostics.Count - $errors.Count) warnings; no Emit."
    $diagnostics | ForEach-Object { $_.ToString() } | Sort-Object -Unique | Write-Output
    $script:failures += $errors.Count
}

$failures = 0
$skipped = 0
foreach ($mode in @('Common') + $UnityVersions) {
    $unity = $mode -ne 'Common'
    $editor = Join-Path $UnityEditors "$mode/Editor/Data/Managed/UnityEngine"
    if ($unity -and -not (Test-Path -LiteralPath $editor)) {
        Write-Output "$mode : skipped, the editor is not installed."
        $skipped++
        continue
    }

    $withTests = $IncludeTests -and (-not $unity -or ($UnityNUnit -and $UnityTestRunner))
    if ($IncludeTests -and -not $withTests) {
        Write-Output "$mode : tests left out, give -UnityNUnit and -UnityTestRunner to include them."
    }

    $framework = Join-Path $packs 'NETStandard.Library.Ref/2.1.0/ref/netstandard2.1'
    if ($withTests -and -not $unity) {
        $pack = Get-ChildItem -LiteralPath (Join-Path $packs 'Microsoft.NETCore.App.Ref') -Directory |
            Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
        $framework = Join-Path $pack.FullName 'ref/net10.0'
    }

    $referenceFiles = @(Get-ChildItem -LiteralPath $framework -Filter '*.dll')
    $symbols = [System.Collections.Generic.List[string]]::new()
    if ($withTests) { $referenceFiles += $testReferences }
    if ($unity) {
        $symbols.Add('UNITY_2022_3_OR_NEWER')
        if ($mode.StartsWith('6000')) { $symbols.Add('UNITY_6000_0_OR_NEWER') }
        $referenceFiles += @(Get-ChildItem -LiteralPath $editor -Filter 'UnityEngine*.dll')
        if ($withTests) {
            # Unity tests use the NUnit that Unity ships instead of the package one.
            $symbols.Add('UNITY_INCLUDE_TESTS')
            $referenceFiles = @($referenceFiles | Where-Object { $_.Name -ne 'nunit.framework.dll' })
            $referenceFiles += @(Get-Item -LiteralPath $UnityNUnit, $UnityTestRunner)
        }
    }

    $references = [System.Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]]::new()
    foreach ($file in $referenceFiles) {
        $references.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($file.FullName))
    }

    $parseOptions = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithLanguageVersion(
        [Microsoft.CodeAnalysis.CSharp.LanguageVersion]::CSharp9).WithPreprocessorSymbols(
        [System.Collections.Generic.IEnumerable[string]]$symbols)
    $options = [Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new(
        [Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary)

    $allTrees = [System.Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]]::new()
    $commonTrees = [System.Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]]::new()
    $adapterTrees = [System.Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]]::new()
    $files = $sources
    if ($withTests) { $files += $tests }
    foreach ($file in $files) {
        $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
            [System.IO.File]::ReadAllText($file.FullName), $parseOptions, $file.FullName,
            [System.Text.Encoding]::UTF8, [System.Threading.CancellationToken]::None)
        $allTrees.Add($tree)
        $adapter = $file.FullName -match '02\.2-UnityWrappers' -or $file.FullName -in $tests.FullName
        if ($adapter) { $adapterTrees.Add($tree) } else { $commonTrees.Add($tree) }
    }

    $common = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create(
        'SpatialCommon', $commonTrees, $references, $options)
    $boundaryReferences = [System.Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]]::new($references)
    $boundaryReferences.Add($common.ToMetadataReference())
    $boundary = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create(
        'SpatialAdapter', $adapterTrees, $boundaryReferences, $options)
    $whole = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create(
        'SpatialStaticAnalysis', $allTrees, $references, $options)

    Get-Diagnostics "$mode assembly boundary" $boundary
    Get-Diagnostics $mode $whole
}

if ($skipped -ne 0) { Write-Output "$skipped Unity versions were skipped." }
if ($failures -ne 0) { exit 1 }
