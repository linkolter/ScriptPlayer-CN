param([string]$PrivateBin,[string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
$workspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bin=[IO.Path]::GetFullPath((Join-Path $workspace $PrivateBin))
$evidence=[IO.Path]::GetFullPath((Join-Path $workspace $EvidenceDirectory))
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$ref=Join-Path $workspace 'net48-reference/build/.NETFramework/v4.8'
$refs=@('mscorlib','System','System.Core','System.Xml','System.Xml.Linq','WindowsBase','PresentationCore','PresentationFramework','System.Xaml') | ForEach-Object { '/r:'+(Join-Path $ref ($_+'.dll')) }
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /debug:full /optimize- /target:exe /nostdlib+ @refs "/r:$bin/ScriptPlayer.exe" "/r:$bin/ScriptPlayer.Shared.dll" "/out:$bin/PlaybackChromeIsolationTests.exe" (Join-Path $PSScriptRoot 'PlaybackChromeIsolationTests.cs')
if($LASTEXITCODE -ne 0){throw 'Compile failed'}
$previous=$env:APPDATA
$results=@()
function Invoke-Case($name,$order,$pointer) {
    $arguments=@((Join-Path $workspace 'ui-test-video.mp4'),(Join-Path $evidence "$name.log"),(Join-Path $workspace 'ScriptPlayer-merged/ScriptPlayer/ScriptPlayer/App.xaml'),$order,$pointer)
    $quoted=$arguments | ForEach-Object { '"'+$_+'"' }
    $process=Start-Process -FilePath "$bin/PlaybackChromeIsolationTests.exe" -ArgumentList $quoted -PassThru -WindowStyle Hidden
    if(!$process.WaitForExit(30000)) { $process.Kill(); throw "Diagnostic timeout: $name PID $($process.Id)" }
    $process.Refresh()
    return $process.ExitCode
}
try {
    foreach($repeat in 1..2){
        foreach($case in @('right','edge','heat')){
            foreach($pointer in @('transport','center')){
                $name="b01-$case-$pointer-$repeat"
                $env:APPDATA=Join-Path $evidence "$name-profile"
                $exitCode=Invoke-Case $name $case $pointer
                $results+=@{name=$name;exitCode=$exitCode;expectedFailure=($pointer -eq 'transport')}
                Write-Output "$name exit=$exitCode expectedFailure=$($pointer -eq 'transport')"
            }
        }
    }
    foreach($order in @('right,edge,heat','heat,right,edge','edge,heat,right')){
        $name='b01-order-'+$order.Replace(',','-')
        $env:APPDATA=Join-Path $evidence "$name-profile"
        $exitCode=Invoke-Case $name $order 'center'
        $results+=@{name=$name;exitCode=$exitCode;expectedFailure=$false}
        Write-Output "$name exit=$exitCode"
    }
} finally { $env:APPDATA=$previous }
$results | ConvertTo-Json | Set-Content (Join-Path $evidence 'b01-isolation-results.json') -Encoding UTF8
if(@($results | Where-Object { ($_.exitCode -eq 0) -eq $_.expectedFailure }).Count -gt 0){throw 'Unexpected diagnostic outcome; inspect logs'}
