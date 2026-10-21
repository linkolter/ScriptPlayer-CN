# 构建与验证

## 2026-10-20 v1.0.12.6 最终验收

- 本轮恢复点：当前仓库忽略目录 `.scratch/visual-motion-20261020/before/` 的真实文件字节；包含开工前v5未提交工作。完整差异/文件SHA256/审查记录/失败日志保留在同任务目录。
- 构建：工作区 `build-media-player.ps1 -PublishName .scratch/visual-motion-20261020/build-accepted -PublicRelease`。VS2022 Release成功，原SubtitleHandler未使用字段两条CS0414警告保留。仍使用既有MK312预编译依赖，不等于干净机器完整方案构建。
- 完整Stage4：`run-playback-baseline.ps1 -PublishName .scratch/visual-motion-20261020/build-accepted -Stage4`；run `20261020-180737-243372`，退出0，`stage4=true / coreOnly=false / suitesFailed=[]`。播放核心41、暂停退出33、协调核心32/UI18、实时核心32/UI37、Shell452、Timeline156，共 **801 PASS / 0 FAIL**。源码及该次harness哈希与当前对应源码一致。
- `tests/Run-MotionTests.ps1 -PrivateBin .scratch/visual-motion-20261020/build-accepted -OutputDirectory .scratch/<新目录>`：68配置（旧XML迁移、模式优先级、全部预设/逐标志往返与Duplicate、通知隔离）、46真实WPF控件/取消/三态/系统/低图形能力/最小化/卸载/GIF/760窄窗中英文深浅主题、1真实libmpv物理hover，共115/0。最终证据 `tests-reviewed`。
- NativeHover先保存鼠标，失败立即ERROR/非零；25s独立截止恢复原位置并退出，开始时要求真实MpvTimeSource和Ready/playing/duration。Runner同时检查退出码、成功标记和错误行，不把窗口关闭重置的进程码当通过。
- 原外观`Run-InterfacePreferenceTests`：30/0；SeekBoundsProbe：6/0（宽120/502、两端以及中间位置）。设备UI：48/0；ReleasePrivacy：33/0；TCodeWifiTests六轴/UDP回环/失败COM：20/0；TCodeWifiDialogTests：9/0。所有测试串行，私有APPDATA/临时夹具；无实物连接。
- 修改前v5完整基线run `20261020-170652-9afc0e`：536 PASS/2 ERROR（SetCursorPos、Cannot preserve cursor）；原v5 Shell独立重跑最终0失败。v6早期run `20261020-173755-b4c9aa` 和 `20261020-175042-0b1a33` 在真实悬停超时，不能认定历史问题。诊断增加等待曾偶然通过，但已移除临时等待/日志，原断言未降低。
- 最小探针在修复前构建连续捕获约0.574不透明度（阈值>=.8），确认返回1；可取消显示从0改为.86后，原生hover及未放宽的完整Stage4通过。保留red/green和早期失败日志，不覆盖旧结果。
- 独立审查分阶段核对XML、时钟归属、纯UI白名单、GIF不重复重启及指针保存/截止，修正建议后复验。实时generation和pendingPlaybackSettings不变；播放、AB、Seek、暂停退出、650ms界面繁忙时后台输出见完整Stage4。
- 边界：未验证实物Handy/OSR6/蓝牙运动、不同DPI/驱动/低端实机或另一台干净Windows。软件渲染/系统关闭/最小化为可注入策略检查；自然卡顿自动简化未作长期性能统计。第三方完整对应材料未在本轮补齐。


## v1.0.12.5 设备交互（2026-10-20）

- 修改前固定仓库干净，v4 Stage4 `20261020-113150-821452`：512 PASS、2 ERROR。旧文档曾将相同分项误加为612，本轮按原始日志更正为512，不改变原失败结论。
- v5最终Release编译成功；已有SubtitleHandler._currentIndex的两条CS0414警告保留。
- `tests/Run-DeviceSettingsUiTests.ps1 -PrivateBin .scratch/device-ui-20261020/build-accepted -OutputDirectory .scratch/<新目录>`：48 PASS / 0 FAIL。真实WPF控件事件与假连接回调，覆盖主页到子页、缺密钥、密码框宽度、忙碌导航/断开/退出/重复打开、COM选择完成、连接失败与恢复、760宽/中英文/深浅主题。图为测试窗口渲染，无真实硬件连接。
- 最终完整Stage4 `20261020-121432-0c8228`：536 PASS、2 ERROR，退出码1。Shell323项通过后SetCursorPos failed；时间轴20项后Cannot preserve cursor for capture test。与改前同类环境鼠标API异常，未执行后续断言不计通过；未放宽测试。
- 播放核心41、暂停退出33、协调核心32/UI18、实时核心32/UI37均0失败；新增导航页面使Shell已执行通过数比改前多24项。
- 隐私33/0；TCodeWifiTests20/0（六轴与UDP loopback、坏COM）；TCodeWifiDialogTests9/0（含忙碌、坏端点、连接失败和地址保留）。仅专属临时目录，未操作用户媒体/真实设备。
- 独立审查修正Wi-Fi连接期间争用控制器、拒绝导航选中项不一致及Handy说明问题，最终未发现遗留High。Shared/HandyApi/ViewModels/Playback差异为零。
- 证据：本仓库忽略目录 `.scratch/device-ui-20261020`，含before字节快照、计划、专项日志、渲染图与报告；完整runner证据在上述工作区run。第一次专项测试遗漏InitializeComponent导致测试进程未退出，已按专属exe完整路径收尾；修复测试初始化后重跑，不作为产品失败。
- 未验证：Handy实际网络连接、OSR6运动、蓝牙SPP实机、BLE/GATT、另一台干净电脑；第三方材料完整性仍是独立待办。未自动提交/推送或发布Release。


## 2026-10-16 固定仓库验证

从当前 GitHub Desktop 仓库重新编译 v1.0.12.4 成功。完整 Stage4
`20261016-150627-f4f19b` 为512 PASS、2 ERROR，退出码1：Shell在299项通过后
`SetCursorPos failed`；时间轴在20项通过后 `Cannot preserve cursor for capture test`。
其余播放核心41、退出33、协调核心32/UI18、实时核心32/UI37均0失败。
未执行的后续UI断言不计通过；不把此次结果写成完整通过。凭据隔离另33/0。
工作流脚本解析通过。日志保留在上层工作区对应run；本次修正目录和构建入口，
没有为环境鼠标异常降低测试断言或修改播放器逻辑。

2026-10-16：源码固定在当前 GitHub Desktop 仓库；本机命令工作目录为上层 `<workspace>`，入口转发到当前仓库 `tools/workspace`。参见 [固定开发目录](WORKSPACE.md)。不要在真实媒体目录运行夹具/故障工具。

## v1.0.12.4 稳定运行库集成（2026-10-16）

- 组件：MSYS2 UCRT64 mpv 0.41.0-8（自报 mpv v0.41.0 / API 2.5 / 内部FFmpeg 9.0.2），独立Gyan FFmpeg 9.0.2 full build。132项组件身份见 Runtime/runtime-versions.json；构建导出时逐项校验，不只检查两个主文件。
- 升级前Release构建成功。首次Stage4 `20261016-124539-55d30f` 在Shell物理hover步骤超时（299 PASS、1 ERROR），其余套件0失败；相同成品单独Shell复跑417/0。此物理悬停现象未定位根因，不宣称已修复。
- 升级候选、仍用v1.0.12.3应用的Stage4 `20261016-125239-abaf3f` 完整通过。首次v1.0.12.4 run `20261016-130537-6720b2` 有3项固定检查旧版本文本的Shell断言失败；同步期望为新版本，保持显示/可见/内嵌/滚动断言。
- 最终公开模式Release构建与完整Stage4 `20261016-131211-70c1f5`：**766 PASS、0 FAIL/ERROR**；stage4=true、coreOnly=false、suitesFailed=[]。41播放+417界面+33退出+32协调核心+18协调UI+156时间轴+32实时核心+37实时UI。构建保留既有SubtitleHandler._currentIndex的CS0414警告（临时工程与主Shared各一条），无编译错误。
- FFmpeg真实wrapper专项7/0：中文/空格路径的音视频信息、尺寸/帧率/时长、JPEG序列、libx264/hqdn3d/stereo3d/scale片段及重读、坏媒体拒绝、真实q取消截止。另以 `--inject-cancel-error` 实际注入异常，预期非零且拥有的PID已退出。该测试不代表R05原ConsoleWrapper生命周期已修复。
- 凭据隔离33/0；TCodeWifiTests六轴与回环20/0。没有连接实物设备。
- 入口：`tests/Run-RuntimeCompatibilityTests.ps1 -PrivateBin <工作区相对候选目录> -OutputDirectory .scratch/<新的夹具目录>`；仍用原完整Stage4入口。证据在工作区 `.scratch/runtime-upstream-release-20261016/`，最终运行在上述独立run目录。
- 静态/延迟PE导入闭包131DLL、无未解析非系统导入，隔离加载与实际播放通过；这不覆盖任意插件、Python/VapourSynth脚本环境、字幕字体配置、全部硬件解码/显卡组合或另一台干净Windows机器。第三方完整对应材料审查与公开发布另行验收。

## 预置条件与构建

- Windows / .NET Framework 4.8 与可操作的WPF桌面；UI测试会显示窗口、移动指针，需要串行且避免人工同时操作。
- 本机VS2022 Community：`C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe` 和其 `Roslyn\csc.exe`；rg；工作区 `net48-reference/build/.NETFramework/v4.8`。
- 既有 `ScriptPlayer-merged/ScriptPlayer/packages`、Runtime/ffmpeg.exe、Runtime/libmpv-2.dll、MK312预编译DLL与本机测试视频。记录依赖身份，未经任务授权不下载/升级。
- 主应用和Shared等为旧式csproj；MK312为SDK net48。本机脚本通过 `UsePrebuiltProjectReferences=true` 使用已有MK312，**不等于整个解决方案的干净构建**。两处旧绝对WinRT HintPath已不存在；本机成功不能推广到新机器。

```powershell
# 选择此前不存在的目录，避免增量Copy混入旧发布文件
$taskPublish = 'playback-baseline/builds/' + (Get-Date -Format 'yyyyMMdd-HHmmss')
.\build-media-player.ps1 -PublishName $taskPublish
.\run-playback-baseline.ps1 -PublishName $taskPublish -Stage4
```

build-media-player 使用Release、net48引用与主csproj；会更新各项目bin/obj并复制成品，不修改源文件。调用者捕获完整stdout/stderr到独立build.log，检查退出码和警告。不要覆盖v11或已有验证目录。本轮实际build目录为 `playback-baseline/maintenance-20261010/build`。

## 完整 Stage4 基线

Runner 创建 `playback-baseline/runs/<时间-随机标识>`，私有app、fixtures、APPDATA和实例ID；真实MPV/FFmpeg、RecordingDevice及loopback随机UDP。无真实硬件连接。

| 日志 / 入口 | 主要协议 |
| --- | --- |
| playback-core.log / PlaybackBaselineTests | 开关片、Seek、脚本延迟/速度、时序 |
| shell.log / MediaShellSmokeTests | 设置入口、绑定/保存、内嵌UI、主页/收藏、独立显隐、窄布局 |
| exit-profile.log / PlaybackExitTests | 6次进程，严格暂停退出资格及跨启动 |
| coordinator-core/results.log / PlaybackCoordinatorTests | 无WPF编译的命令、意图、旧Task |
| coordinator-ui.log / PlaybackCoordinatorUiTests | 真实mpv、失败恢复、拖动与状态 |
| timeline-ui.log / TimelineUiTests | 热力图/书签/AB/退出标记、缓存及按需预览隔离 |
| realtime-core.log / RealtimePlaybackTests | 计算、单槽、代际、stop、可释放慢Sink |
| realtime-ui.log / RealtimePlaybackUiTests | 100/300/650ms UI stall、MPV同步、L0/R0 UDP、Seek/AB/切换 |

所有八组日志结束、无FAIL/ERROR；退出有六个 `Phase failures: 0`，其余 `Failures: 0`；`result.json` 要求stage4=true、coreOnly=false、suitesFailed=[]。运行前后已列源码/测试/成品SHA256匹配，才认定完整通过。PASS数量记录观测结果，不硬编码为永久阈值；新增测试应增加总数。

禁止 `-CoreOnly -Stage4`：标志可能写成stage4，但实际跳过扩展测试。CoreOnly仅为局部测量。原核心/实时UI有进程内watchdog，旧Shell/退出/时间轴不都有截止，父级需监督；超时记失败并只收尾属于本轮的PID，不能批量kill用户播放器/FFmpeg。强制结束可能未恢复指针/资源，须报告。

完整runner的finally恢复APPDATA；单独test-playback-exit.ps1不恢复调用进程APPDATA，须另开PowerShell或外包try/finally。测试不可并发共享同一配置或桌面。

## 局部与额外六轴测试

```powershell
.\test-playback-coordinator.ps1 -Output 'playback-baseline/<新目录>/coordinator-core'
.\test-playback-coordinator-ui.ps1 -RunDirectory '<具备私有app的run>'
.\test-playback-timeline.ps1 -RunDirectory '<具备app及A/B夹具的run>'
.\test-playback-realtime.ps1 -RunDirectory '<具备app及A/B夹具的run>'
# 原Wi-Fi脚本会写主项目bin/Release；需要私有副本时按下述csc方式运行
.\github-upload\ScriptPlayer-CN-20261012\tests\Run-TCodeWifiTests.ps1
```

Stage4不含独立六轴测试。TCodeWifiTests覆盖六轴精确插值、25ms、延迟、I1000/I50、移除轴回中、断开/重连、COM99999失败；实际UDP只有loopback。TCodeWifiDialogTests用回调fake，不连真机，但临时写程序旁OSR6-WiFi.txt并finally恢复。最好编译到本次私有app：

```powershell
$taskRun = '<完整运行目录>'
$taskBin = Join-Path $taskRun 'app'
$taskCsc = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe'
& $taskCsc /nologo /target:exe "/out:$taskBin/TCodeWifiTests.exe" "/r:$taskBin/ScriptPlayer.Shared.dll" 'github-upload/ScriptPlayer-CN-20261012/tests/TCodeWifiTests.cs'
# 确认csc退出码为0再执行
& "$taskBin/TCodeWifiTests.exe"
$taskWpf = "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/WPF"
& $taskCsc /nologo /target:exe "/out:$taskBin/TCodeWifiDialogTests.exe" "/r:$taskBin/ScriptPlayer.exe" "/r:$taskWpf/PresentationFramework.dll" "/r:$taskWpf/PresentationCore.dll" "/r:$taskWpf/WindowsBase.dll" 'github-upload/ScriptPlayer-CN-20261012/tests/TCodeWifiDialogTests.cs'
& "$taskBin/TCodeWifiDialogTests.exe" "$taskRun/wifi-dialog.png"
```

每个编译/执行立即检查退出码；把输出分别写wifi-core.log/wifi-dialog.log，不能只看最后一条命令。检查OSR6-WiFi.txt前后实际字节或原来不存在的状态一致。不要与Stage4桌面测试同时运行。

## 本次接管实测与历史

历史：`runs/20261010-150440-1ac59e` 的v11记录614项、0失败。[V11-SUMMARY](V11-SUMMARY.md)是原交付证据；第一阶段REPORT/README的UI冻结属于兼容链，不代表当前实时链。

本次run：`playback-baseline/runs/20261010-211546-1a8010`。主项目Release编译成功，build.log未出现警告/错误；本机构建不是干净机器验证。完整Stage4**未通过**：611 PASS、3 FAIL。七组非Shell日志全部0失败，Shell为328 PASS、3 FAIL，result.suitesFailed为shell-process/shell。退出6阶段完整。

| 套件 | 本次通过 | 本次失败 |
| --- | ---: | ---: |
| playback-core | 41 | 0 |
| shell | 328 | 3 |
| exit-profile | 33 | 0 |
| coordinator-core | 32 | 0 |
| coordinator-ui | 18 | 0 |
| timeline-ui | 90 | 0 |
| realtime-core | 32 | 0 |
| realtime-ui | 37 | 0 |
| 额外wifi-core | 20 | 0 |
| 额外wifi-dialog | 9 | 0 |

首次Stage4三个失败原文：`Right edge reveals only quick settings`、`Edge settings do not pin an unrelated transport open`、`Hovering persistent heatmap does not move it away from the pointer`。相同成品、新shell-rerun-profile复跑，仍同样3失败（328通过）。原日志和result保持不改，复跑日志另存；不将两次PASS重复计入一轮总数。包含额外Wi-Fi但不重复复跑的一轮共640通过、3失败。

Wi-Fi配置运行前后均不存在，临时写入已恢复。实物未连接。根因未确定，见[MAINTENANCE B01](MAINTENANCE.md)与[定位票](../.scratch/maintenance-takeover/issues/02-ui-baseline.md)。本轮不改断言或生产实现。完整运行与构建证据保存于[接管证据](../.scratch/maintenance-takeover/evidence/)，明细以baseline-results.json为准。

不要用playback-baseline/verify-v11.ps1认证新维护结果：它会覆盖历史摘要/源码文档且固定写ECC APPROVE。本轮独立检查日志、哈希和审查事实，不改历史运行目录。

标准manifest并未覆盖所有config、native子目录、包缓存、工具和预编译依赖；本地接管另存611项文件清单。哈希只验证已列身份，不证明未测试行为正确，也不是恢复快照。

## 第一轮正式维护实测（R03 / B01，2026-10-10）

最终成品：`playback-baseline/round1-20261010/release/ScriptPlayer.exe`。最终完整run：`playback-baseline/runs/20261010-221259-ca4701`，stage4=true、coreOnly=false、suitesFailed=[]；617 PASS/0 FAIL，退出6阶段完整。主项目Release成功，无build日志警告/错误；仍是本机预编译MK312条件，不宣称整解决方案干净构建。

| 套件 | 通过 | 失败 |
| --- | ---: | ---: |
| playback-core | 41 | 0 |
| shell | 334 | 0 |
| exit-profile | 33 | 0 |
| coordinator-core | 32 | 0 |
| coordinator-ui | 18 | 0 |
| timeline-ui | 90 | 0 |
| realtime-core | 32 | 0 |
| realtime-ui | 37 | 0 |
| 新R03 module | 80 | 0 |
| 新R03真实caller | 38 | 0 |
| 额外wifi-core | 20 | 0 |
| 额外wifi-dialog | 9 | 0 |

一次最终验收合计764 PASS/0 FAIL，不重复计入诊断和候选版本。证据位于 `ScriptPlayer-merged/.scratch/round1-r03-b01/evidence`，校验明细为final-verification.json。

```powershell
# 工作目录仍为workspace，发布目录必须新建且不要覆盖旧交付
.\build-media-player.ps1 -PublishName 'playback-baseline/<新目录>/release'
.\github-upload\ScriptPlayer-CN-20261012\tests\Run-RelatedFileRenameTests.ps1
.\github-upload\ScriptPlayer-CN-20261012\tests\Run-RelatedFileRenameIntegrationTests.ps1 -PrivateBin 'playback-baseline/<新目录>/release'
.\github-upload\ScriptPlayer-CN-20261012\tests\Run-MaintenanceRegression.ps1 -PublishName 'playback-baseline/<新目录>/release'
```

最后一个wrapper保留真实指针、先放到中性位置，finally恢复；原Stage4 runner不改。Shell测试自己仍建立真实鼠标/隐藏前置和完整恢复，断言不降低；不要与其他桌面操作并发。caller runner有30秒子进程监督，core使用私有TEMP GUID树，清理验证绝对边界；不要直接运行caller EXE，它会拒绝未标识的真实APPDATA。Report失败夹具也仅操作本次private profile。

B01：三项原断言保留；旧成品真实鼠标在transport上各2次稳定失败，视频中央各2次通过，三个执行顺序通过。原完整顺序强制悬停3 FAIL及完整栈留证；修复隔离后同旧成品Shell334/0。生产MainWindow.PlaybackChrome.cs哈希不变。旧接管328/3不更改，不混入新增问题。

R03：旧suffix算法红测试3 FAIL（预期复现），新80/0。caller38/0覆盖真实命令、移动过程中保持原Entry、选中变体/共享与外目录同名、部分执行/回滚失败不发布、Newtonsoft JSON恢复、报告I/O错误、实际MainVM附加目录/加载候选/坏目录保护。视频脚本NFO原字节SHA不变；不验证NFO内部引用或历史迁移。

异常必须保留：早期B01诊断夹具缺InitializeComponent/就绪判定导致异常与挂起，已仅结束确认属于本轮的PID30816；随后补初始化/超时/活动窗口，原日志未删。R03首次fixture缺文件异常已修正并留日志。两次caller报告因GetFolderPath忽略APPDATA误写真实profile，17份仅含本轮TEMP路径的JSON全量预检后归档misrouted-test-reports，哈希一致；改用显式绝对APPDATA并增加EXE守卫，最终9份清单实测位于私有profile，无真实媒体改动。

O01：中间Stage4 `20261010-215917-43f686` 时间轴1失败，后续未复现，仍开放。旧成品两次中性指针90/0及主动悬停诊断92/0不证明根因；断言和预览生产源码不改，不归为历史问题。详见[报告](../.scratch/round1-r03-b01/REPORT.md)。

完整快照及Restore说明：工作区 `ScriptPlayer-maintenance-snapshots/round1-20261010-213825`，1915项/1121336158字节逐文件SHA一致。文件差异及来源身份在changes.patch/changes.json/final-verification.json。实物设备/DPI/崩溃恢复仍未测试，不把loopback成功当运动验证。

## 按影响选择回归（策略）

| 维护范围 | 必需验证 |
| --- | --- |
| R03/R02/R10 文件写入 | 临时目录正常/冲突/失败/恢复；内容哈希和NFO未知内容；退出/主页/时间轴；编译与完整Stage4 |
| R01/R07 脚本与配置 | 计算核心、展示负向/运动正向、轴策略、完整Stage4；TCode另加六轴 |
| R04 设备生命周期 | 不完成I/O/断连/Stop异常隔离子进程；共享client；完整Stage4；实物验证待授权 |
| R05 FFmpeg | 失败子进程/流/磁盘/取消晚结果/退出；真实预览；编译与完整Stage4 |
| R06/R08/R09 异步生命周期 | 初始化/旧回调/扫描阶段注入；对应UI或native集成；编译与完整Stage4 |
| R11 性能 | 1,000/5,000临时库冷/热分阶段指标；成员/投影正确性与适用回归 |
| 仅文档 | 源码核实、内部链接/命令/证据审查、生产文件哈希；本次接管另外建立完整实测基线 |

既有测试使用反射检验部分UI装配，暂时保留；新module尽量让caller与测试穿同一interface，不能只测抽出的纯函数却漏调用协议。涉及写入、设备和并发必须有失败路径；死锁测试用可控制阶段与父watchdog，不用任意Sleep证明完成。

## 手工和设备验收

自动测试不能替代：实物每轴方向/范围/限流/柔和起步/暂停停止/断连和关闭；共享Buttplug client慢设备；长时间负载；不同显卡/高码率；920/1280/1920、不同DPI/多屏；键盘/屏幕阅读器；设置入口、齿轮命中、拖动失捕、独立显隐、预览切换、明确暂停退出与换片不记录。

真实设备仅在用户明确授权及可安全控制的条件下执行；记录设备/固件/连接/轴、步骤、输出观测和结果。未连接或未观察运动必须标未验证，UDP发送成功不代表设备接收。不能据模拟测试承诺物理同步时延。

## 标题栏版本入口验收（2026-10-10）

第二轮最新验收见下节。
成品 playback-baseline/version-1.0.12.0/ready；完整 Stage4 runs/20261010-224652-4f049d：625 PASS/0 FAIL，新增8项 About UI检查，shell342/0。主项目Release编译通过；发布与测试EXE哈希一致。不同DPI、多屏和系统Snap悬停未实机验证。详见 ../.scratch/version-button/REPORT.md。

## 第二轮界面验收（2026-10-11）

成品 playback-baseline/round2-20261011/ready/ScriptPlayer.exe，FileVersion 1.0.12.0 / ProductVersion v1.0.12.0。ready-build.log 主项目Release成功；沿用本机构建依赖，不宣称全解决方案干净环境验证。

最终完整run playback-baseline/runs/20261011-112846-088795：coreOnly=false、Stage3/4=true、suitesFailed=[]，**739 PASS/0 FAIL**。

| 套件 | 通过 | 失败 |
| --- | ---: | ---: |
| playback-core | 41 | 0 |
| shell | 412 | 0 |
| exit-profile | 33 | 0 |
| coordinator-core | 32 | 0 |
| coordinator-ui | 18 | 0 |
| timeline-ui | 134 | 0 |
| realtime-core | 32 | 0 |
| realtime-ui | 37 | 0 |
| 独立interface-core | 30 | 0 |
| 独立seek-bounds | 6 | 0 |

验收合计775项，不重复计入中间候选、独立时间轴134/0与调度探针2/0。503源码和17标准harness身份一致，额外入口另记final-verification.json。成品SHA256：CAC24A422CFAEACB2EEEF9696E1D81F46A772F5331466BD389E7738F9095AE4C。

从工作区顺序执行，每条核对退出码，新目录不得覆盖旧交付：

    .\build-media-player.ps1 -PublishName 'playback-baseline/<新目录>/ready'
    .\github-upload\ScriptPlayer-CN-20261012\tests\Run-MaintenanceRegression.ps1 -PublishName 'playback-baseline/<新目录>/ready'
    .\github-upload\ScriptPlayer-CN-20261012\tests\Run-InterfacePreferenceTests.ps1 -PrivateBin 'playback-baseline/<新目录>/ready' -OutputDirectory '.scratch/<新任务>/extra-tests'

独立入口复制托管依赖至全新测试目录，不写真实profile、不连接mpv或设备。旧成品绘制探针6 FAIL → 本轮6 PASS，验证端点2DIP裁切；完整UI覆盖760/920/1280/1920、展开左栏、实际native overlay内的时间轴/总时长/标记/设置按钮。语言检查31分类、Close、绑定/用户文本保护、12次切换、旧/非法XML；主题检查局部资源、播放文字与五色；动画检查快速切页与立即取消。四项新外观偏好保存后Realtime实例和version不变，混合旧播放设置仍应用。

保留中间失败：110845旧分类数量断言；112305快速预览超时O02；112604主页50ms等待。主页修为Background操作屏障，原断言不变，独立调度探针复现计时不等于完成。首次探针漏Dispose，仅结束确认专属路径PID25036，补释放后退出0；额外编译曾遇路径传参及旧csc/缺System.Xaml，最终使用项目Roslyn/net48引用通过。日志保留于scratch。

O02最终及单独复跑未复现但原因未确定。未直接统计native状态/Stop调用，未专门断言编辑中语言切换；实物设备、多DPI/多屏、Snap、长期负载及全部动态异常消息翻译未验收。详见 ../.scratch/round2-ui/REPORT.md 与review.md，不自动关闭R01–R11/O01。

## 第三轮 R02 基线与验证（2026-10-11）

修改前主项目 Release 编译成功，baseline 成品位于 playback-baseline/round3-20261011/baseline；完整 Stage4 runs/20261011-144020-6be44b：739 PASS / 0 FAIL、coreOnly=false、stage3/4=true、suitesFailed=[]。503 个标准生产源码与 before 快照一致，发布/测试 EXE SHA256 均为 CAC24A422CFAEACB2EEEF9696E1D81F46A772F5331466BD389E7738F9095AE4C。

本轮 630 个源码/文档/测试文件实际字节保存于 .scratch/round3-r02/before，清单 before-manifest.json；不复制已有 runtime/packages，不覆盖旧成品。NFO 故障测试只在 runner 带标记的私有夹具树运行。

旧 NfoMetadata 在独立红测 nfo-20261011-144414-d6b20d96 中 13 PASS / 15 FAIL，预期退出 1；包括未知 XML 丢失、外部修订/删除/目标出现、序列化异常截断和共享路径重绑定。真实 WPF caller 修正夹具隔离后，旧成品 nfo-caller-20261011-150950-90e2cec8 为0 PASS / 5 FAIL、无 ERROR。保留原日志，故意红测不能混入交付回归。

    .\github-upload\ScriptPlayer-CN-20261012\tests\Run-NfoMetadataTests.ps1 -Baseline -ExpectRed
    .\github-upload\ScriptPlayer-CN-20261012\tests\Run-NfoMetadataTests.ps1
    .\github-upload\ScriptPlayer-CN-20261012\tests\Run-RelatedFileRenameTests.ps1 -EvidenceDirectory '.scratch/<新任务>/evidence/r03-core'
    .\github-upload\ScriptPlayer-CN-20261012\tests\Run-RelatedFileRenameIntegrationTests.ps1 -PrivateBin 'playback-baseline/<新目录>/ready' -EvidenceDirectory '.scratch/<新任务>/evidence/r03-caller'

NFO runner 为每次运行创建新证据目录；R03 两入口新增可选 EvidenceDirectory（项目内绝对边界验证），测试断言、默认路径、退出判断和私有 profile 协议不变。本轮必须显式指定新目录，避免覆盖第一轮历史日志。

### v1.0.12.1 最终验收

成品 `playback-baseline/round3-20261011/v1.0.12.1/ScriptPlayer.exe`；FileVersion 1.0.12.1、ProductVersion v1.0.12.1，标题按钮、关于中英文和发布日期2026.10.11一致。主项目 Release 通过；仍使用本地已有 MK312 产物，不宣称全解决方案/新机器干净编译。

| 验证组 | PASS | FAIL | 证据 |
| --- | ---: | ---: | --- |
| 完整 Stage4（八组） | 739 | 0 | runs/20261011-210753-858862；非 CoreOnly，suitesFailed=[] |
| NFO 模块与注入失败 | 50 | 0 | nfo-20261011-151111-b4b559bf；源码身份未变 |
| 实际收藏/编辑入口 | 10 | 0 | nfo-caller-20261011-210718-91306af4 |
| 长提示、小窗口布局 | 1 | 0 | nfo-caller-20261011-210713-921961c8 |
| 外观与端点绘制 | 36 | 0 | evidence/interface-final |
| R03 核心回归 | 80 | 0 | evidence/r03-core；源码身份未变 |
| R03 实际入口回归 | 38 | 0 | evidence/r03-final；最终 EXE 的测试副本 |

合计 **954 PASS / 0 FAIL**，不重复计入基线、中间候选、红测或旧版R03 caller。NFO模组50与R03核心80以未变的生产/测试/runner SHA证明适用；实际UI、外观和R03 caller使用最终同版EXE。503标准生产源码、17标准harness与最终完整run一致，完整run、layout、caller、外观、R03 caller的应用EXE与发布成品均为 `63C0B3681416B48A5F64915F0C28221693B43BD01C29AB447946E09030F6BE36`。见本轮 final-verification.json / extra-verification.json。

    .\github-upload\ScriptPlayer-CN-20261012\tests\Run-NfoMetadataIntegrationTests.ps1 -PrivateBin 'playback-baseline/<新目录>/ready' -LayoutProbeOnly
    .\github-upload\ScriptPlayer-CN-20261012\tests\Run-NfoMetadataIntegrationTests.ps1 -PrivateBin 'playback-baseline/<新目录>/ready'

布局probe保留560×430、真实主题、3000字恢复提示；按 GetClientRect/ClientToScreen 与按钮 PointToScreen 检查真实客户区，且 ScrollToBottom 后要求正offset。64px-only修复在210552仍0/1，已保留；最终固定底部按钮、表单独立滚动后1/0，未降低断言。全套验证由root串行，不连接真实设备，不改真实媒体；测试profile与夹具私有。

原始异常：首个caller红测有重复全局命令注册和未Dispose worker，专属PID10928被watchdog结束；第二次夹具 RoutedEventArgs 无RoutedEvent，记ERROR而非功能红测。分别修夹具隔离、事件及finally释放后重跑，原日志保留。证据脚本曾用斜杠键查Windows反斜杠清单而失败，修键规范化后通过；此非播放器测试失败。B01无残留失败；O01/O02原因仍未确认，不因本轮预览通过关闭。实物设备、长期负载、多DPI/多屏及外部程序严格事务隔离未验证。

## 时间轴与底边交互 v1.0.12.2（2026-10-11）

主项目Release成功；成品 `playback-baseline/round4-20261011/ready-final/ScriptPlayer.exe`，FileVersion1.0.12.2、ProductVersionv1.0.12.2，SHA256 `B2E422E74662E4698E3AA696CE4E6340748DAC66B0CD41008853D7F6BB10D652`。沿用本机已有MK312产物，不代表新机/全解决方案干净编译。

冻结最终完整Stage4 `runs/20261011-220058-872e69`：**761 PASS /0 FAIL /0 ERROR**；八套件完成，stage3/4=true、coreOnly=false、suitesFailed=[]。时间轴156/0（原134+22），另七套件605/0。503生产源码/17标准harness与run manifest相符，发布EXE与测试副本相同；634before字节均有效。详见 `.scratch/round4-timeline/final-verification.json`。

新增覆盖：Heat/Progress邻接及共同左右区间（760/920/1280/1920），隐藏heat真实指针悬停仅预览、从视频接近不提前展开、真正底边独立展开、无heat/markers仍有12DIP入口与空Compact零高度、合成预览隔离真实指针；真实TryFind入口重复null元数据保持最新hover，实际新缓存发布并清除旧generated帧。原5秒截止、29/40最新时间、离开/换片失效、拖动/capture、AB/书签/暂停退出、设置按钮、UI繁忙下实时同步保留。

    .\build-media-player.ps1 -PublishName 'playback-baseline/<新目录>/ready'
    .\github-upload\ScriptPlayer-CN-20261012\tests\Run-MaintenanceRegression.ps1 -PublishName 'playback-baseline/<新目录>/ready'

所有桌面测试root串行、新APPDATA/合成视频/native libmpv/本地输出夹具。私有cache-replacement即便失败也删除本测试副本、恢复原.saved，不触及真实媒体。PlaybackChromeIsolationTests只同步新坐标，未单独执行；同一行为由完整Shell/Timeline覆盖。

保留故意红测：旧版布局139/7无ERROR；通知修复前真实caller148/2及1超时ERROR。保留完整四次中间失败：213448为746 PASS/1 FAIL/1 ERROR，213932/214344/215055各753/1/1；fast最新preview超时，不混入最终验收。两个单独预览153/0不足以定位；215055 Fast-HeatMap stack确定后台重复通知取消。215838候选761/0时fixture恢复修正晚于manifest，非最终身份依据；最终220058为冻结同源。

历史O02缺stack，不能断定与本轮全部同因；O01仍开放。实物设备、长期负载、多DPI/多屏未验证。不把上轮954与本轮761相加，不将故意红测或候选计入最终通过。独立审查及详细中间异常见 `.scratch/round4-timeline/REPORT.md` / `review.md`。

## 公开构建与私人设置 v1.0.12.3（2026-10-11）

主项目公开Release强制Rebuild成功，成品 `playback-baseline/round5-20261011/public-final/ScriptPlayer.exe`，FileVersion1.0.12.3/ProductVersionv1.0.12.3，SHA256 `887DD03928E4F6DE9DEB37E11FFE3F0B714FC656D3E330B5E519A30F508FD0FD`。禁用DebugType/DebugSymbols；已有MK312依赖边界不变，未新机干净编译。

完整冻结Stage4 `runs/20261011-222737-f89f04` 八套件 **766 PASS/0 FAIL/0 ERROR**；Shell417、Timeline156，其余193；stage3/4=true、coreOnly=false、suitesFailed=[]。504生产文件和17标准harness匹配manifest，发布与测试EXE同hash，before634字节有效。新增5条真实UI检查覆盖FFmpeg不泄露安装目录、统一设置及inline密钥遮蔽。

    .\build-media-player.ps1 -PublicRelease -PublishName 'playback-baseline/<新目录>/ready'
    .\github-upload\ScriptPlayer-CN-20261012\tests\Run-MaintenanceRegression.ps1 -PublishName 'playback-baseline/<新目录>/ready'
    .\github-upload\ScriptPlayer-CN-20261012\tests\Run-ReleasePrivacyTests.ps1 -PrivateBin 'playback-baseline/<新目录>/ready' -OutputDirectory '.scratch/<任务>/privacy'

独立凭据正常/故障33/0，日志 `.scratch/round5-release/privacy-frozen/results.log`，测试源同目录冻结，直接Settings interface +真实Windows DPAPI，无Handy网络连接。涵盖legacy、XML导出、clone、加密恢复/缺XML、清空、不可写、损坏cipher/保护位、损坏ordinary XML真实MainVM默认回退/保护位、序列化异常、共享冲突、默认bundled/移动定位/override。33项+766项合计799，不叠加候选或故意红测。

旧v1.0.12.2隐私红测12FAIL（部分私密文件断言因缺文件未执行）；最初control-char故障未真实触发序列化错误，改未注册派生类型，原断言不降低，旧日志保留。审查修复前候选broken-xml-red用同33项冻结测试，通过28、失败5；最终全绿。初次PublicRelease无Rebuild仍四个PE带旧路径，最终强制Rebuild扫描全70文件为零；本机构建开关的增量缓存影响已记录。

最终source/portable目录及ZIP解压内容采用已知用户路径和实际本机key内存比对，输出只布尔/计数/命中文件，无原key或fingerprint；SHA/ZIP integrity/实际源码身份见package-audit.json及最终verify。真实profile仅读不迁移；下一次运行新程序正常成功保存才迁移。双文件非事务、原备份可能含明文、不跨用户、依赖源码材料待补齐与未实物/多DPI/新机验收见RELEASING。

## 组件下载发行包测试（2026-10-12，v1.0.12.3）

任务记录：.scratch/runtime-release-20261012。WindowsPowerShell5.1.26100.4202最终故障测试13 PASS/0 FAIL，实际入口6/0（无解压器幂等、缺程序、缺工具、保留冲突、新安装官方缓存、CMD启动/退出）；官方组件5/0，mpv真实HTTPS下载、FFmpeg官方原包缓存及提取后身份、临时目录清理。不能把FFmpeg缓存测试说成再次真实网络下载。

初次网络沙箱拒绝socket；授权扩展后mpv下载成功。WindowsPS5.1最初拒绝默认脚本策略，随后发现UTF8无BOM中文解析及Get-FileHash模块环境问题；最终CMD透明使用本进程ExecutionPolicy Bypass，入口ASCII，SHA256采用.NET流，未改持久策略。这些中间失败不计入最终通过。

安装器测试入口：Test-Installer.ps1、test_entry.py、Test-OfficialInstaller.ps1；均仅访问任务私有夹具，不启动设备或读取个人profile。恢复记录写失败已有明确注入测试，保留staging并报告，不宣称自动恢复。测试证据installer-tests-ps51.log、installer-tests.json、entry-tests.json、official-installer-tests.json及独立review.md。后续复跑须创建新夹具，不能将结果数硬编码为永久门槛。

最终程序EXE/DLL/config与此前公开构建同字节，448个C#文件未变；本轮只构建安装/源码材料ZIP，不重新编译程序或运行Stage4，既有766/0/0和凭据33/0/0作为历史基线保留。新ZIP CRC/逐成员哈希、来源清单、已知路径与危险文件检查通过，完整第三方材料仍待补。
