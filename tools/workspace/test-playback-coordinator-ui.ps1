param([Parameter(Mandatory=$true)][string]$RunDirectory)
$workspace = & (Join-Path $PSScriptRoot '../../tests/Find-LocalWorkspace.ps1')
$ErrorActionPreference='Stop'
$run=(Resolve-Path -LiteralPath $RunDirectory).Path
$bin=Join-Path $run 'app'
if(!(Test-Path -LiteralPath (Join-Path $bin 'ScriptPlayer.exe'))){
    throw "测试程序副本已归档或不存在。请先按 $run\APP-ARCHIVED.txt 的说明恢复 app 目录。"
}
$ref=Join-Path $workspace 'net48-reference/build/.NETFramework/v4.8'
$refs=@('mscorlib','System','System.Core','System.Xml','System.Xml.Linq','WindowsBase','PresentationCore','PresentationFramework','System.Xaml') | ForEach-Object {'/r:'+(Join-Path $ref ($_+'.dll'))}
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /nostdlib+ @refs "/r:$bin/ScriptPlayer.exe" "/r:$bin/ScriptPlayer.Shared.dll" "/out:$bin/PlaybackCoordinatorUiTests.exe" (Join-Path $workspace 'github-upload/ScriptPlayer-CN-20261012/tests/PlaybackCoordinatorUiTests.cs')
if($LASTEXITCODE -ne 0){throw 'UI integration compilation failed'}
$profile=Join-Path $run ('coordinator-ui-profile-'+[guid]::NewGuid().ToString('N'))
$previousAppData=$env:APPDATA
try{
    $env:APPDATA=$profile
    & (Join-Path $bin 'PlaybackCoordinatorUiTests.exe') (Join-Path $workspace 'ui-test-video.mp4') (Join-Path $run 'coordinator-ui.log') (Join-Path $workspace 'github-upload/ScriptPlayer-CN-20261012/ScriptPlayer/ScriptPlayer/App.xaml')
    if($LASTEXITCODE -ne 0){throw 'Coordinator UI tests failed'}
}finally{$env:APPDATA=$previousAppData}
Get-Content -LiteralPath (Join-Path $run 'coordinator-ui.log')
