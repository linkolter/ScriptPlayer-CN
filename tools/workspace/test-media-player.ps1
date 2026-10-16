param([string]$Profile='media-ui-transparent-test-profile',[string]$Log='media-ui-transparent-tests.log',[string]$PublishName='')
$workspace = & (Join-Path $PSScriptRoot '../../tests/Find-LocalWorkspace.ps1')
if ([string]::IsNullOrWhiteSpace($PublishName)) { throw 'Specify -PublishName for the build being tested.' }
$ErrorActionPreference='Stop'
$ref=Join-Path $workspace 'net48-reference/build/.NETFramework/v4.8'
$bin=Join-Path $workspace $PublishName
$refs=@('mscorlib','System','System.Core','System.Xml','System.Xml.Linq','WindowsBase','PresentationCore','PresentationFramework','System.Xaml','UIAutomationProvider','UIAutomationTypes') | ForEach-Object { '/r:'+(Join-Path $ref ($_+'.dll')) }
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /nostdlib+ @refs "/r:$bin/ScriptPlayer.exe" "/r:$bin/ScriptPlayer.Shared.dll" "/out:$bin/MediaShellSmokeTests.exe" (Join-Path $workspace 'github-upload/ScriptPlayer-CN-20261012/tests/MediaShellSmokeTests.cs')
if($LASTEXITCODE -ne 0){throw 'Test compilation failed'}
$previousTestAppData=$env:APPDATA
try {
    $env:APPDATA=Join-Path $workspace $Profile
    & "$bin/MediaShellSmokeTests.exe" (Join-Path $workspace 'ui-test-video.mp4') (Join-Path $workspace $Log) (Join-Path $workspace 'github-upload/ScriptPlayer-CN-20261012/ScriptPlayer/ScriptPlayer/App.xaml')
    if($LASTEXITCODE -ne 0 -or !(Select-String -LiteralPath (Join-Path $workspace $Log) -Pattern '^Failures: 0$')){throw 'Integration tests failed'}
} finally { $env:APPDATA=$previousTestAppData }
