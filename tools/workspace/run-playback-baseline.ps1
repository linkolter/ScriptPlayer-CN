param([string]$PublishName='',[switch]$CoreOnly,[switch]$Stage3,[switch]$Stage4)
$workspace = & (Join-Path $PSScriptRoot '../../tests/Find-LocalWorkspace.ps1')
if ([string]::IsNullOrWhiteSpace($PublishName)) { throw 'Specify -PublishName for the build being tested.' }
$Stage3=$Stage3 -or $Stage4
$ErrorActionPreference='Stop'
$root=$workspace
$runName='playback-baseline/runs/'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,6)
$run=Join-Path $root $runName
$bin=Join-Path $run 'app'
$publish=Join-Path $root $PublishName
New-Item -ItemType Directory -Path $bin -Force | Out-Null
# Work against a private copy, including a private APPDATA; leave released apps/configuration alone.
Get-ChildItem -LiteralPath $publish | Where-Object {$_.Name -notmatch 'Tests\.exe$'} | Copy-Item -Destination $bin -Recurse
$sourceFiles=& rg --files (Join-Path $root 'github-upload/ScriptPlayer-CN-20261012/ScriptPlayer') -g '*.cs' -g '*.xaml' -g '*.csproj' -g '!bin' -g '!obj' -g '!packages'
$manifest=[ordered]@{
    capturedAt=(Get-Date).ToString('o'); publishedFrom=$publish; stage3=[bool]$Stage3; stage4=[bool]$Stage4; operatingSystem=[Environment]::OSVersion.ToString(); runtime=[Environment]::Version.ToString()
    binaries=@(Get-ChildItem -LiteralPath $bin -File | Where-Object {$_.Extension -in '.exe','.dll'} | ForEach-Object {[ordered]@{name=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
    sourceSnapshot=@($sourceFiles | ForEach-Object {[ordered]@{path=$_.Substring($root.Length+1);sha256=(Get-FileHash -LiteralPath $_).Hash}})
    workflow=@(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1' -File | ForEach-Object {[ordered]@{path=$_.FullName.Substring($root.Length+1);sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
    workspaceLocator=[ordered]@{path='github-upload/ScriptPlayer-CN-20261012/tests/Find-LocalWorkspace.ps1';sha256=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot '../../tests/Find-LocalWorkspace.ps1')).Hash}
    harness=@(@('run-playback-baseline.ps1','summarize-playback-baseline.ps1','test-media-player.ps1','test-playback-exit.ps1','github-upload/ScriptPlayer-CN-20261012/tests/PlaybackBaselineTests.cs','github-upload/ScriptPlayer-CN-20261012/tests/MediaShellSmokeTests.cs','github-upload/ScriptPlayer-CN-20261012/tests/PlaybackExitTests.cs','ui-test-video.mp4') | ForEach-Object {@{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $root $_)).Hash}})
}
if($Stage3){$manifest.harness+=@(@('test-playback-coordinator.ps1','test-playback-coordinator-ui.ps1','test-playback-timeline.ps1','github-upload/ScriptPlayer-CN-20261012/tests/PlaybackCoordinatorTests.cs','github-upload/ScriptPlayer-CN-20261012/tests/PlaybackCoordinatorUiTests.cs','github-upload/ScriptPlayer-CN-20261012/tests/TimelineUiTests.cs') | ForEach-Object {@{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $root $_)).Hash}})}
if($Stage4){$manifest.harness+=@(@('test-playback-realtime.ps1','github-upload/ScriptPlayer-CN-20261012/tests/RealtimePlaybackTests.cs','github-upload/ScriptPlayer-CN-20261012/tests/RealtimePlaybackUiTests.cs') | ForEach-Object {@{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $root $_)).Hash}})}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $run 'manifest.json') -Encoding UTF8
$fixtures=Join-Path $run 'fixtures'
New-Item -ItemType Directory -Path $fixtures | Out-Null
foreach($fixture in @(@{name='A';duration=12;first=10;second=90},@{name='B';duration=16;first=80;second=20})){
    $video=Join-Path $fixtures ($fixture.name+'.mp4')
    & (Join-Path $bin 'ffmpeg.exe') -hide_banner -loglevel error -f lavfi -i 'testsrc2=size=320x180:rate=30' -t $fixture.duration -an -c:v libx264 -preset ultrafast -pix_fmt yuv420p $video
    if($LASTEXITCODE -ne 0){throw 'Fixture generation failed'}
    $actions=@(0..$fixture.duration | ForEach-Object { @{at=$_*1000;pos=$(if($_%2 -eq 0){$fixture.first}else{$fixture.second})} })
    @{version='1.0';inverted=$false;range=90;actions=$actions} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $fixtures ($fixture.name+'.funscript')) -Encoding UTF8
}
$ref=Join-Path $root 'net48-reference/build/.NETFramework/v4.8'
$refs=@('mscorlib','System','System.Core','System.Xml','System.Xml.Linq','WindowsBase','PresentationCore','PresentationFramework','System.Xaml') | ForEach-Object { '/r:'+(Join-Path $ref ($_+'.dll')) }
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /nostdlib+ @refs "/r:$bin/ScriptPlayer.exe" "/r:$bin/ScriptPlayer.Shared.dll" "/r:$bin/Newtonsoft.Json.dll" "/out:$bin/PlaybackBaselineTests.exe" (Join-Path $root 'github-upload/ScriptPlayer-CN-20261012/tests/PlaybackBaselineTests.cs')
if($LASTEXITCODE -ne 0){throw 'Core test compilation failed'}
$originalAppData=$env:APPDATA
$suiteErrors=@()
try{
    $env:APPDATA=Join-Path $run 'core-profile'
    & (Join-Path $bin 'PlaybackBaselineTests.exe') (Join-Path $fixtures 'A.mp4') (Join-Path $fixtures 'B.mp4') $run (Join-Path $root 'github-upload/ScriptPlayer-CN-20261012/ScriptPlayer/ScriptPlayer/App.xaml')
    if($LASTEXITCODE -ne 0 -or !(Select-String -LiteralPath (Join-Path $run 'playback-core.log') -Pattern '^Failures: 0$')){$suiteErrors+='playback-core'}
    if(!$CoreOnly){
        try { & (Join-Path $root 'test-media-player.ps1') -PublishName ($runName+'/app') -Profile ($runName+'/shell-profile') -Log ($runName+'/shell.log') }
        catch {$suiteErrors+='shell-process'; $_ | Out-String | Add-Content -LiteralPath (Join-Path $run 'runner-errors.log')}
        if(!(Test-Path (Join-Path $run 'shell.log')) -or !(Select-String -LiteralPath (Join-Path $run 'shell.log') -Pattern '^Failures: 0$')){$suiteErrors+='shell'}
        try { & (Join-Path $root 'test-playback-exit.ps1') -PublishName ($runName+'/app') -Profile ($runName+'/exit-profile') }
        catch {$suiteErrors+='exit-process'; $_ | Out-String | Add-Content -LiteralPath (Join-Path $run 'runner-errors.log')}
        $exitLog=Join-Path $run 'exit-profile.log'
        if(!(Test-Path $exitLog) -or (Select-String -LiteralPath $exitLog -Pattern '^Phase failures: 0$').Count -ne 6){$suiteErrors+='exit'}
        if($Stage3){
            try { & (Join-Path $root 'test-playback-coordinator.ps1') -Output ($runName+'/coordinator-core') } catch {$suiteErrors+='coordinator-core'; $_ | Out-String | Add-Content -LiteralPath (Join-Path $run 'runner-errors.log')}
            try { & (Join-Path $root 'test-playback-coordinator-ui.ps1') -RunDirectory $run } catch {$suiteErrors+='coordinator-ui'; $_ | Out-String | Add-Content -LiteralPath (Join-Path $run 'runner-errors.log')}
            try { & (Join-Path $root 'test-playback-timeline.ps1') -RunDirectory $run } catch {$suiteErrors+='timeline-ui'; $_ | Out-String | Add-Content -LiteralPath (Join-Path $run 'runner-errors.log')}
            if($Stage4){
                try { & (Join-Path $root 'test-playback-realtime.ps1') -RunDirectory $run } catch {$suiteErrors+='realtime'; $_ | Out-String | Add-Content -LiteralPath (Join-Path $run 'runner-errors.log')}
            }
        }
    }
}
finally{$env:APPDATA=$originalAppData}
@{run=$run;suitesFailed=$suiteErrors;coreOnly=[bool]$CoreOnly;stage3=[bool]$Stage3;stage4=[bool]$Stage4;completedAt=(Get-Date).ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding UTF8
if(Test-Path -LiteralPath (Join-Path $run 'timing-metrics.json')){& (Join-Path $root 'summarize-playback-baseline.ps1') -RunDirectory $run}
Write-Output "Baseline artifacts: $run"
if($suiteErrors.Count -gt 0){throw ('Baseline failures: '+($suiteErrors -join ', '))}
