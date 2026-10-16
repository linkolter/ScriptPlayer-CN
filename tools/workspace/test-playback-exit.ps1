param([string]$Profile='media-ui-v4-exit-profile',[string]$PublishName='')
$workspace = & (Join-Path $PSScriptRoot '../../tests/Find-LocalWorkspace.ps1')
if ([string]::IsNullOrWhiteSpace($PublishName)) { throw 'Specify -PublishName for the build being tested.' }
$ErrorActionPreference='Stop'
$ref=Join-Path $workspace 'net48-reference/build/.NETFramework/v4.8'
$bin=Join-Path $workspace $PublishName
$refs=@('mscorlib','System','System.Core','System.Xml','System.Xml.Linq','WindowsBase','PresentationCore','PresentationFramework','System.Xaml') | ForEach-Object { '/r:'+(Join-Path $ref ($_+'.dll')) }
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /nostdlib+ @refs "/r:$bin/ScriptPlayer.exe" "/r:$bin/ScriptPlayer.Shared.dll" "/r:$bin/Newtonsoft.Json.dll" "/out:$bin/PlaybackExitTests.exe" (Join-Path $workspace 'github-upload/ScriptPlayer-CN-20261012/tests/PlaybackExitTests.cs')
if($LASTEXITCODE -ne 0){throw 'Test compilation failed'}
$env:APPDATA=Join-Path $workspace $Profile
if(Test-Path -LiteralPath $env:APPDATA){throw 'Use a fresh isolated test profile.'}
$fixtureA=Join-Path $workspace 'ui-test-video.mp4'
$fixtureB=Join-Path $workspace 'ui-test-video-second.mp4'
if(!(Test-Path -LiteralPath $fixtureB)){Copy-Item -LiteralPath $fixtureA -Destination $fixtureB}
$log=Join-Path $workspace ($Profile+'.log')
Set-Content -LiteralPath $log -Value 'Paused exit integration tests'
foreach($phase in 1..6){
    & "$bin/PlaybackExitTests.exe" $fixtureA $fixtureB $phase $log (Join-Path $workspace 'github-upload/ScriptPlayer-CN-20261012/ScriptPlayer/ScriptPlayer/App.xaml')
    if($LASTEXITCODE -ne 0){throw "Exit test phase $phase failed; see $log"}
}
Get-Content -LiteralPath $log
