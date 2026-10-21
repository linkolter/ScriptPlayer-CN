param([Parameter(Mandatory=$true)][string]$PrivateBin,
      [Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$workspace=& (Join-Path $PSScriptRoot 'Find-LocalWorkspace.ps1')
$project=Split-Path $PSScriptRoot -Parent
$source=(Resolve-Path -LiteralPath (Join-Path $workspace $PrivateBin)).Path
$output=[IO.Path]::GetFullPath((Join-Path $project $OutputDirectory))
if (!$output.StartsWith($project+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Test output must stay in project' }
if (Test-Path -LiteralPath $output) { throw 'Use a new test output directory' }
$app=Join-Path $output 'app'
New-Item -ItemType Directory -Path $app | Out-Null
Get-ChildItem -LiteralPath $source | Copy-Item -Destination $app -Recurse
$compiler='C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe'
$reference=Join-Path $workspace 'net48-reference\build\.NETFramework\v4.8'
$refs=@('mscorlib','System','System.Core','System.Xml','System.Xml.Linq','WindowsBase','PresentationCore','PresentationFramework','System.Xaml') | ForEach-Object { '/r:'+(Join-Path $reference ($_+'.dll')) }
foreach($test in @('MotionPreferenceTests','MotionUiTests','MotionNativeHoverTests')) {
    & $compiler /nologo /target:exe /nostdlib+ @refs "/r:$app\ScriptPlayer.exe" "/r:$app\ScriptPlayer.Shared.dll" "/out:$app\$test.exe" (Join-Path $PSScriptRoot "$test.cs") *> (Join-Path $output "$test-compile.log")
    if ($LASTEXITCODE -ne 0) { throw "$test compilation failed" }
}
$previous=$env:APPDATA
try {
    $env:APPDATA=Join-Path $output 'profile'
    & (Join-Path $app 'MotionPreferenceTests.exe') *> (Join-Path $output 'preferences.log')
    Get-Content (Join-Path $output 'preferences.log')
    if ($LASTEXITCODE -ne 0) { throw 'Motion preferences failed' }
    & (Join-Path $app 'MotionUiTests.exe') (Join-Path $project 'ScriptPlayer/ScriptPlayer/App.xaml') $output
    $testExit=$LASTEXITCODE
    Get-Content (Join-Path $output 'results.log')
    if($testExit -ne 0 -or !(Select-String -LiteralPath (Join-Path $output 'results.log') -Pattern '^Failures: 0$') -or
       (Select-String -LiteralPath (Join-Path $output 'results.log') -Pattern '^(FAIL:|ERROR:)')) { throw 'Motion UI failed' }
    & (Join-Path $app 'MotionNativeHoverTests.exe') (Join-Path $project 'ScriptPlayer/ScriptPlayer/App.xaml') (Join-Path $workspace 'ui-test-video.mp4') (Join-Path $output 'native-hover.log')
    $testExit=$LASTEXITCODE
    Get-Content (Join-Path $output 'native-hover.log')
    if($testExit -ne 0 -or !(Select-String -LiteralPath (Join-Path $output 'native-hover.log') -Pattern '^Failures: 0$') -or
       (Select-String -LiteralPath (Join-Path $output 'native-hover.log') -Pattern '^(FAIL:|ERROR:)')) { throw 'Native hover failed' }
} finally { $env:APPDATA=$previous }
