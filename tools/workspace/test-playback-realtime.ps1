param([Parameter(Mandatory=$true)][string]$RunDirectory)
$workspace = & (Join-Path $PSScriptRoot '../../tests/Find-LocalWorkspace.ps1')
$ErrorActionPreference='Stop'
$run=(Resolve-Path -LiteralPath $RunDirectory).Path
$bin=Join-Path $run 'app'
$ref=Join-Path $workspace 'net48-reference/build/.NETFramework/v4.8'
$refs=@('mscorlib','System','System.Core','System.Xml','System.Xml.Linq','WindowsBase','PresentationCore','PresentationFramework','System.Xaml') | ForEach-Object {'/r:'+(Join-Path $ref ($_+'.dll'))}
foreach($name in @('RealtimePlaybackTests','RealtimePlaybackUiTests')) {
    & 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /nostdlib+ @refs "/r:$bin/ScriptPlayer.exe" "/r:$bin/ScriptPlayer.Shared.dll" "/r:$bin/Newtonsoft.Json.dll" "/out:$bin/$name.exe" (Join-Path $workspace "github-upload/ScriptPlayer-CN-20261012/tests/$name.cs")
    if($LASTEXITCODE -ne 0){throw "Realtime test compilation failed: $name"}
}
& (Join-Path $bin 'RealtimePlaybackTests.exe') (Join-Path $run 'realtime-core.log')
if($LASTEXITCODE -ne 0){throw 'Realtime core tests failed'}
# A real companion-axis script exercises the production TCode adapter over local UDP.
@{version='1.0';actions=@(0..12 | ForEach-Object {@{at=$_*1000;pos=$(if($_%2 -eq 0){30}else{70})}})} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $run 'fixtures/A.twist.funscript') -Encoding UTF8
$previousAppData=$env:APPDATA
try {
    $env:APPDATA=Join-Path $run ('realtime-profile-'+[guid]::NewGuid().ToString('N'))
    & (Join-Path $bin 'RealtimePlaybackUiTests.exe') (Join-Path $run 'fixtures/A.mp4') (Join-Path $run 'fixtures/B.mp4') $run (Join-Path $workspace 'github-upload/ScriptPlayer-CN-20261012/ScriptPlayer/ScriptPlayer/App.xaml')
    if($LASTEXITCODE -ne 0){throw 'Realtime integration tests failed'}
} finally {$env:APPDATA=$previousAppData}
Get-Content -LiteralPath (Join-Path $run 'realtime-core.log')
Get-Content -LiteralPath (Join-Path $run 'realtime-ui.log')
