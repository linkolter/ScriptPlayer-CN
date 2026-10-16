param([string]$Output='playback-baseline/coordinator-tests')
$workspace = & (Join-Path $PSScriptRoot '../../tests/Find-LocalWorkspace.ps1')
$ErrorActionPreference='Stop'
$destination=Join-Path $workspace $Output
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$ref=Join-Path $workspace 'net48-reference/build/.NETFramework/v4.8'
$refs=@('mscorlib','System','System.Core') | ForEach-Object {'/r:'+(Join-Path $ref ($_+'.dll'))}
$source=Join-Path $workspace 'github-upload/ScriptPlayer-CN-20261012/ScriptPlayer/ScriptPlayer/Playback'
# Deliberately no WPF or application assembly references: core must compile independently.
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /nostdlib+ @refs "/out:$destination/PlaybackCoordinatorTests.exe" (Join-Path $source 'IMediaEngine.cs') (Join-Path $source 'PlaybackState.cs') (Join-Path $source 'PlaybackCoordinator.cs') (Join-Path $workspace 'github-upload/ScriptPlayer-CN-20261012/tests/PlaybackCoordinatorTests.cs')
if($LASTEXITCODE -ne 0){throw 'Coordinator test compilation failed'}
& (Join-Path $destination 'PlaybackCoordinatorTests.exe') (Join-Path $destination 'results.log')
if($LASTEXITCODE -ne 0){throw 'Coordinator tests failed'}
Get-Content -LiteralPath (Join-Path $destination 'results.log')
