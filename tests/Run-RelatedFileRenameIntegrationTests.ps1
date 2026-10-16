param([string]$PrivateBin='playback-baseline/round1-20261010/build', [string]$EvidenceDirectory='.scratch/round1-r03-b01/evidence')
$ErrorActionPreference='Stop'
$workspace=& (Join-Path $PSScriptRoot 'Find-LocalWorkspace.ps1')
$bin=[IO.Path]::GetFullPath((Join-Path $workspace $PrivateBin))
$repository=Split-Path $PSScriptRoot -Parent
$evidence=[IO.Path]::GetFullPath((Join-Path $repository $EvidenceDirectory))
if (!$evidence.StartsWith($repository+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence directory must stay in project' }
[IO.Directory]::CreateDirectory($evidence) | Out-Null
$ref=Join-Path $workspace 'net48-reference/build/.NETFramework/v4.8'
$refs=@('mscorlib','System','System.Core','WindowsBase','PresentationCore','PresentationFramework','System.Xaml') | ForEach-Object { '/r:'+(Join-Path $ref ($_+'.dll')) }
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /debug:full /target:exe /nostdlib+ @refs "/r:$bin/ScriptPlayer.exe" "/r:$bin/ScriptPlayer.Shared.dll" "/r:$bin/Newtonsoft.Json.dll" "/out:$bin/RelatedFileRenameIntegrationTests.exe" (Join-Path $PSScriptRoot 'RelatedFileRenameIntegrationTests.cs')
if($LASTEXITCODE -ne 0){throw 'Integration compilation failed'}
$previous=$env:APPDATA
try {
    $env:APPDATA=Join-Path $evidence ('caller-profile-'+[guid]::NewGuid().ToString('N'))
    $process=Start-Process -FilePath "$bin/RelatedFileRenameIntegrationTests.exe" -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $evidence 'r03-caller.log') -RedirectStandardError (Join-Path $evidence 'r03-caller-stderr.log')
    if(!$process.WaitForExit(30000)){ $process.Kill(); throw "Caller tests timed out PID $($process.Id)" }
    $process.Refresh()
    if($process.ExitCode -ne 0){throw 'Caller tests failed; see r03-caller.log'}
} finally { $env:APPDATA=$previous }
