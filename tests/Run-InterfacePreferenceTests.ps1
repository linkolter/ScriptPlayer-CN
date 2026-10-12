param([Parameter(Mandatory=$true)][string]$PrivateBin,
      [string]$OutputDirectory='.scratch/round2-ui/extra-tests')
$ErrorActionPreference='Stop'
$workspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$project=Split-Path $PSScriptRoot -Parent
$source=(Resolve-Path -LiteralPath (Join-Path $workspace $PrivateBin)).Path
$output=[IO.Path]::GetFullPath((Join-Path $project $OutputDirectory))
if (!$output.StartsWith($project+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Test output must stay in project' }
if (Test-Path -LiteralPath $output) { throw 'Use a new test output directory' }
$app=Join-Path $output 'app'
New-Item -ItemType Directory -Path $app | Out-Null
# These headless checks need managed assemblies only; do not copy media/native runtimes.
Get-ChildItem -LiteralPath $source -File |
    Where-Object { ($_.Extension -eq '.dll' -and $_.Name -notlike 'libmpv*') -or $_.Name -in @('ScriptPlayer.exe','ScriptPlayer.exe.config') } |
    Copy-Item -Destination $app
$compiler='C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe'
$reference=Join-Path $workspace 'net48-reference\build\.NETFramework\v4.8'
$refs=@('mscorlib','System','System.Core','System.Xml','WindowsBase','PresentationCore','PresentationFramework','System.Xaml') |
    ForEach-Object { '/r:'+(Join-Path $reference ($_+'.dll')) }
foreach ($test in @('SeekBoundsProbe','InterfacePreferenceTests')) {
    & $compiler /nologo /target:exe /nostdlib+ @refs "/r:$app\ScriptPlayer.exe" "/r:$app\ScriptPlayer.Shared.dll" "/out:$app\$test.exe" (Join-Path $PSScriptRoot "$test.cs") *> (Join-Path $output "$test-compile.log")
    if ($LASTEXITCODE -ne 0) { throw "$test compilation failed" }
}
& (Join-Path $app 'SeekBoundsProbe.exe') (Join-Path $app 'ScriptPlayer.exe') *> (Join-Path $output 'seek-bounds.log')
if ($LASTEXITCODE -ne 0) { throw 'Seek bounds failed' }
& (Join-Path $app 'InterfacePreferenceTests.exe') *> (Join-Path $output 'interface-core.log')
if ($LASTEXITCODE -ne 0) { throw 'Interface preferences failed' }
Get-Content -LiteralPath (Join-Path $output 'seek-bounds.log')
Get-Content -LiteralPath (Join-Path $output 'interface-core.log')
