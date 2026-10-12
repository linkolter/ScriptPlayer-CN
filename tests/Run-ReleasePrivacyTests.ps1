param([Parameter(Mandatory=$true)][string]$PrivateBin,
      [Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$workspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$project=Split-Path $PSScriptRoot -Parent
$source=(Resolve-Path -LiteralPath (Join-Path $workspace $PrivateBin)).Path
$output=[IO.Path]::GetFullPath((Join-Path $project $OutputDirectory))
if (!$output.StartsWith($project+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Test output must stay in project' }
if (Test-Path -LiteralPath $output) { throw 'Use a new test output directory' }
$app=Join-Path $output 'app'
New-Item -ItemType Directory -Path $app | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ReleasePrivacyTests.cs') -Destination $output
Get-ChildItem -LiteralPath $source -File | Where-Object {
    ($_.Extension -eq '.dll' -and $_.Name -notlike 'libmpv*') -or $_.Name -in @('ScriptPlayer.exe','ScriptPlayer.exe.config')
} | Copy-Item -Destination $app
$compiler='C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe'
$reference=Join-Path $workspace 'net48-reference\build\.NETFramework\v4.8'
$refs=@('mscorlib','System','System.Core','System.Xml','System.Security','WindowsBase','PresentationCore','PresentationFramework','System.Xaml') |
    ForEach-Object { '/r:'+(Join-Path $reference ($_+'.dll')) }
& $compiler /nologo /target:exe /nostdlib+ @refs "/r:$app\ScriptPlayer.exe" "/r:$app\ScriptPlayer.Shared.dll" "/out:$app\ReleasePrivacyTests.exe" (Join-Path $output 'ReleasePrivacyTests.cs') *> (Join-Path $output 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Privacy tests compilation failed' }
& (Join-Path $app 'ReleasePrivacyTests.exe') (Join-Path $output 'fixtures') *> (Join-Path $output 'results.log')
Get-Content (Join-Path $output 'results.log')
if ($LASTEXITCODE -ne 0) { throw 'Privacy settings tests failed' }
