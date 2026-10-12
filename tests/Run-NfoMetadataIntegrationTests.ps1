param([Parameter(Mandatory=$true)][string]$PrivateBin, [string]$OutputDirectory='.scratch/round3-r02/evidence', [switch]$ExpectRed, [switch]$ExpectFailure, [switch]$LayoutProbeOnly)
$ErrorActionPreference='Stop'
if($ExpectRed -and $LayoutProbeOnly){throw 'Layout probe targets CURRENT and uses ExpectFailure, not old API ExpectRed.'}
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workspace=Split-Path $repository -Parent
$bin=[IO.Path]::GetFullPath((Join-Path $workspace $PrivateBin))
$evidence=[IO.Path]::GetFullPath((Join-Path $repository $OutputDirectory))
if(!$evidence.StartsWith($repository+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'OutputDirectory must remain in repository scratch.'}
$run=Join-Path $evidence ('nfo-caller-'+(Get-Date -Format yyyyMMdd-HHmmss)+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
[IO.Directory]::CreateDirectory($run)|Out-Null
$app=Join-Path $run 'app';[IO.Directory]::CreateDirectory($app)|Out-Null
# Copy managed assemblies only. Never write test binaries into a baseline/production bin.
Get-ChildItem -LiteralPath $bin -File | Where-Object {$_.Extension -in @('.dll','.exe','.config')} | ForEach-Object {
    $managed=$_.Extension -eq '.config'
    if(!$managed){try{[Reflection.AssemblyName]::GetAssemblyName($_.FullName)|Out-Null;$managed=$true}catch{}}
    if($managed){Copy-Item -LiteralPath $_.FullName -Destination $app}
}
$fixtures=Join-Path $app 'fixtures';[IO.Directory]::CreateDirectory($fixtures)|Out-Null
[IO.File]::WriteAllText((Join-Path $fixtures '.scriptplayer-r02-private-fixture'),'R02 private test fixtures')
$ref=Join-Path $workspace 'net48-reference/build/.NETFramework/v4.8'
$refs=@('mscorlib','System','System.Core','WindowsBase','PresentationCore','PresentationFramework','System.Xaml')|ForEach-Object {'/r:'+(Join-Path $ref ($_+'.dll'))}
$exe=Join-Path $app 'NfoMetadataIntegrationTests.exe'
$source=Join-Path $PSScriptRoot 'NfoMetadataIntegrationTests.cs'
$capturedSource=Join-Path $run 'NfoMetadataIntegrationTests.cs'
$capturedRunner=Join-Path $run 'Run-NfoMetadataIntegrationTests.ps1'
Copy-Item -LiteralPath $source -Destination $capturedSource
Copy-Item -LiteralPath $PSCommandPath -Destination $capturedRunner
$compiler='C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$args=@('/nologo','/debug:full','/target:exe','/nostdlib+')+$refs+@("/r:$app/ScriptPlayer.exe","/r:$app/ScriptPlayer.Shared.dll","/r:$app/Newtonsoft.Json.dll","/out:$exe",$capturedSource)
if(!$ExpectRed){$args+='/define:NFO_CALLER_CURRENT'}
& $compiler @args 2>&1|Tee-Object -FilePath (Join-Path $run 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Caller tests compilation failed; host failures are not red assertions.'}
$previous=$env:APPDATA
$log=Join-Path $run $(if($LayoutProbeOnly){if($ExpectFailure){'nfo-layout.red.log'}else{'nfo-layout.log'}}elseif($ExpectRed){'nfo-caller.red.log'}else{'nfo-caller.log'})
try{
    $env:APPDATA=Join-Path $run ('nfo-caller-profile-'+[guid]::NewGuid().ToString('N'))
    $start=@{FilePath=$exe;PassThru=$true;WindowStyle='Hidden';RedirectStandardOutput=$log;RedirectStandardError=(Join-Path $run 'stderr.log')}
    if($LayoutProbeOnly){$start.ArgumentList='--layout-probe-only'}
    $process=Start-Process @start
    if(!$process.WaitForExit(45000)){$process.Kill();throw "Owned caller process watchdog timeout PID $($process.Id); no functional red claim."}
    $process.Refresh();$exit=$process.ExitCode
}finally{$env:APPDATA=$previous}
$manifest=[ordered]@{Scope='Actual WPF favorite/editor callers; private marked fixtures and APPDATA; no media playback/devices';ExpectedRed=[bool]$ExpectRed;ExpectedFailure=[bool]$ExpectFailure;LayoutProbeOnly=[bool]$LayoutProbeOnly;PrivateBin=$bin;ExitCode=$exit;Passed=@(Get-Content -LiteralPath $log|Where-Object{$_ -like 'PASS:*'}).Count;Failed=@(Get-Content -LiteralPath $log|Where-Object{$_ -like 'FAIL:*'}).Count;LogSHA256=(Get-FileHash -LiteralPath $log).Hash;SourcePath=$capturedSource;SourceSHA256=(Get-FileHash -LiteralPath $capturedSource).Hash;RunnerPath=$capturedRunner;RunnerSHA256=(Get-FileHash -LiteralPath $capturedRunner).Hash;BinarySHA256=(Get-FileHash -LiteralPath (Join-Path $app 'ScriptPlayer.exe')).Hash}
[IO.File]::WriteAllText((Join-Path $run 'verification.json'),($manifest|ConvertTo-Json -Depth 5))
Write-Output "Evidence: $run"
if($ExpectRed -or $ExpectFailure){if($exit -ne 1 -or $manifest.Failed -eq 0 -or (Select-String -LiteralPath $log -Pattern '^ERROR:' -Quiet)){throw 'Expected failure did not produce clean functional red assertions.'}}elseif($exit -ne 0){throw 'Caller regression failed.'}
