param([Parameter(Mandatory=$true)][string]$RunDirectory)
$workspace = & (Join-Path $PSScriptRoot '../../tests/Find-LocalWorkspace.ps1')
$ErrorActionPreference='Stop'
$run=(Resolve-Path -LiteralPath $RunDirectory).Path
$result=Get-Content -LiteralPath (Join-Path $run 'result.json') -Raw | ConvertFrom-Json
$timing=Get-Content -LiteralPath (Join-Path $run 'timing-metrics.json') -Raw | ConvertFrom-Json
$rows=@()
foreach($suite in @(@{name='播放与脚本时序';file='playback-core.log'},@{name='界面回归';file='shell.log'},@{name='暂停退出记录';file='exit-profile.log'})){
    $path=Join-Path $run $suite.file
    if(Test-Path -LiteralPath $path){
        $pass=@(Select-String -LiteralPath $path -Pattern '^PASS:').Count
        $fail=@(Select-String -LiteralPath $path -Pattern '^FAIL:','^ERROR:').Count
        $rows+="| $($suite.name) | $pass | $fail | [$($suite.file)]($($suite.file)) |"
    }
}
$known=@()
if($timing.scriptCallbacksDuringStall -eq 0 -and $timing.nativeAdvanceDuringStallMs -gt 200){$known+='SYNC-001: UI 阻塞期间 ScriptHandler 回调停止，原生视频继续播放。'}
@{knownLimitations=$known;timing=$timing} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $run 'observations.json') -Encoding UTF8
$status=if(@($result.suitesFailed).Count -eq 0){'现有行为回归通过；仍有下面列出的已知限制。'}else{'存在失败项，不能作为通过的回归基线。'}
$report=@"
# 播放器第一阶段回归基线

完成时间：$($result.completedAt)

$status

| 检查组 | 通过断言 | 失败/错误 | 原始日志 |
| --- | ---: | ---: | --- |
$($rows -join "`n")

## 实测同步限制

界面线程阻塞 $([math]::Round($timing.uiStallMs,1)) ms 时，原生 MPV 前进 $([math]::Round($timing.nativeAdvanceDuringStallMs,1)) ms；界面时间源前进 $($timing.sampledAdvanceDuringStallMs) ms，ScriptHandler 回调 $($timing.scriptCallbacksDuringStall) 次。恢复后等待约 $([math]::Round($timing.recoveryWaitMs,1)) ms，重新出现回调且采样追上。

这是 SYNC-001 的测量证据，不代表同步已经与界面线程解耦，也不把冻结行为设为未来必须保留的通过条件。

2 倍播放实测倍率：$([math]::Round($timing.speed2xMeasured,3))。稳定阶段验证脚本动作 $($timing.validatedStableActionEvents) 次，区间错误 $($timing.scriptIntervalErrors) 次。

## 验证边界

- 使用生成的 12 秒 / 16 秒视频及不同脚本；实际启动 libmpv。
- 验证脚本动作时间区间、正负延迟、倍速媒体时间、Seek 状态恢复、快速切换后最终媒体与脚本一致。
- 快速切换的过渡回调保留在轨迹中，但本轮未断言每一个过渡输出都没有旧数据；后续 Coordinator 必须建立会话隔离。
- UI 阻塞只测试一次约 650 ms 人为阻塞，不能代替长时间负载测试。
- 未连接硬件，也未验证物理设备时延、多轴输出、网络传输和音画同步。
- 未覆盖自然播完、损坏文件、并发窗口退出、强制终止以及不同 DPI/机器的性能分布。
- 源码快照与二进制哈希分别记录；本脚本复制已发布程序，不重新编译生产程序。

## 证据文件

- [源码、运行库和程序哈希](manifest.json)
- [脚本回调轨迹](script-trace.json)
- [时序测量](timing-metrics.json)
- [执行结果](result.json)
- [已知限制](observations.json)

断言通过表示所列场景符合当前约定，不表示播放器没有缺陷。
"@
Set-Content -LiteralPath (Join-Path $run 'REPORT.md') -Value $report -Encoding UTF8
Write-Output (Join-Path $run 'REPORT.md')
