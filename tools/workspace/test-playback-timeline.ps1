param([Parameter(Mandatory=$true)][string]$RunDirectory)
$workspace = & (Join-Path $PSScriptRoot '../../tests/Find-LocalWorkspace.ps1')
$ErrorActionPreference='Stop'
$run=(Resolve-Path -LiteralPath $RunDirectory).Path
$bin=Join-Path $run 'app'
$ref=Join-Path $workspace 'net48-reference/build/.NETFramework/v4.8'
$refs=@('mscorlib','System','System.Core','System.Xml','System.Xml.Linq','WindowsBase','PresentationCore','PresentationFramework','System.Xaml') | ForEach-Object {'/r:'+(Join-Path $ref ($_+'.dll'))}
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /nostdlib+ @refs "/r:$bin/ScriptPlayer.exe" "/r:$bin/ScriptPlayer.Shared.dll" "/r:$bin/Newtonsoft.Json.dll" "/out:$bin/TimelineUiTests.exe" (Join-Path $workspace 'github-upload/ScriptPlayer-CN-20261012/tests/TimelineUiTests.cs')
if($LASTEXITCODE -ne 0){throw 'Timeline test compilation failed'}
$previousAppData=$env:APPDATA
try {
    $env:APPDATA=Join-Path $run ('timeline-profile-'+[guid]::NewGuid().ToString('N'))
    & (Join-Path $bin 'TimelineUiTests.exe') (Join-Path $run 'fixtures/A.mp4') (Join-Path $run 'fixtures/B.mp4') $run (Join-Path $workspace 'github-upload/ScriptPlayer-CN-20261012/ScriptPlayer/ScriptPlayer/App.xaml')
    if($LASTEXITCODE -ne 0){throw 'Timeline integration tests failed'}
} finally {$env:APPDATA=$previousAppData}
Get-Content -LiteralPath (Join-Path $run 'timeline-ui.log')
