# 长期维护计划

## 固定源码目录（2026-10-16）

当前唯一开发目录为 GitHub Desktop 已关联的 `github-upload/ScriptPlayer-CN-20261012`，
已同步v1.0.12.4源码及文档；旧ScriptPlayer-merged只作历史参考和缓存。
维护构建/测试实现已进入当前仓库tools/workspace，参见[工作目录约定](WORKSPACE.md)。
新编译成功；完整Stage4本次612 PASS、2项鼠标API异常中断，不能标记全绿；
凭据隔离33/0。详见[测试记录](TESTING.md)。未自动提交或推送，不关闭原架构风险。

日期：2026-10-10。本文件是 R01–R11 风险状态与顺序的唯一总表；具体实施使用现有本地任务票，不另建远程跟踪系统。接管任务见 [spec](../.scratch/maintenance-takeover/spec.md)。

## v1.0.12.4 运行组件维护（2026-10-16）

本地集成已完成：官网列出的MSYS2 mpv 0.41.0-8共享库与Gyan FFmpeg 9.0.2，保留原WPF/libmpv/实时输出链。旧20260814 mpv下载地址和旧release tag实测404，FFmpeg8.1地址200。官方mpv稳定Windows两种包均不含共享DLL，未把mpv.exe冒充DLL，未用每日开发版冒充稳定版。

最终Release编译成功，完整Stage4 766/0、FFmpeg专项7/0及异常清理故障探针通过、隐私33/0、六轴回环20/0。版本/清单/许可/导出校验已更新。旧成品和GitHub内容不改，文件字节快照：工作区 `ScriptPlayer-maintenance-snapshots/runtime-upstream-20261016-125708`。实施票：[runtime-upstream](../.scratch/runtime-upstream-20261016/issues/01-integrate-and-verify.md)。本轮不关闭R01–R11、原hover偶发超时或R05。

后续：另一台干净Windows电脑验证完整便携候选；逐项核验新版第三方对应源码和构建材料；核验最终包后再执行用户授权的公开发布。GitHub现有在线安装器仍固定旧版本，本轮没有修改它；本地新包不需要该安装器。

## 接管与既有风险状态

第一轮 B01 测试隔离与 R03 常规改名/回滚/恢复已完成；第二轮界面维护已完成。第三轮 R02 的完整性保护、按读取版本提交和成功后投影已验收，按用户追加要求发布 v1.0.12.1。R10、R01 整体和其他生产修复仍未实施。本轮时间轴整合与底边唤醒已验收，v1.0.12.2 完整 Stage4 761/0，另定位并修复重复展示通知取消当前按需预览；[时间轴报告](../.scratch/round4-timeline/REPORT.md)保留故障证据与局限。完整记录见[第一轮报告](../.scratch/round1-r03-b01/REPORT.md)、[第二轮报告](../.scratch/round2-ui/REPORT.md)与[第三轮报告](../.scratch/round3-r02/REPORT.md)。测试结果以 [TESTING](TESTING.md) 为准。

原始审查：[2026-10-10 HTML](reviews/2026-10-10/architecture-review.html)；保留原文，不以新计划覆盖审查历史。补充核实：TCodeWifiTests 已有六轴测试；NFO 共享回退只有 `-`；最近卡片可能在当次因共享 Entry 跟随改名，不能断言每次立即丢失最近项。HTML 的 High 表示潜在影响，不是复现概率。

R02–R11证据行号沿用接管时快照，本轮插入改名adapter导致MainViewModel后续行号漂移，定位时以符号名和原快照核对。第一轮R03当前源码与哈希见报告。APPDATA防护目前检查非空/rooted，测试EXE检查profile标记；实际runner已验证私有，不等于Windows完全限定路径或文件身份认证。

## 执行顺序与状态

2026-10-11 第二轮界面维护完成：窄窗口时间轴、可关闭动画、中英文、深浅主题和五种强调色。成品 playback-baseline/round2-20261011/ready，完整 Stage4 739/0，独立外观/绘制36/0，审查无阻断/High。只防护新增四项外观设置，R01整体仍待实施；R02/R10等未实施。详见[报告](../.scratch/round2-ui/REPORT.md)。

本轮新回归观察：

- 主页移除断言：112604-ceb5cc 固定50ms等待失败。独立同模型探针证明计时到期时后台操作仍Pending；测试改等Background队列屏障，保留原断言和1500ms截止，生产HomeMediaViewModel不改。最终日志Completed/sourceContains=false/shelfContains=false。
- **O02开放**：112305-7454d4 的 Fast uncached hover settles on the latest requested preview 超时，一个断言FAIL加捕获ERROR（Failures:2）。随后112604、最终112846和独立时间轴134/0未复现；原因未确定，预览生产实现不改，不能用复跑通过宣称修复。与原O01旧会话预览观察分开；增加请求/媒体/取消状态诊断。
- 首个候选新增外观页后旧30分类断言失败；更新为严格31且包含Appearance，保留全部旧页面访问检查，旧运行原样保留。

| 顺序 | 风险 | 等级 | 状态 | 前置/关键条件 |
| --- | --- | --- | --- | --- |
| 1 | R03 关联重命名与恢复 | High | 常规异常修复与验收完成；崩溃窗口保留 | 80 核心 + 38 caller；完整 Stage4 617/0 |
| 2 | R02 NFO 完整性与安全提交 | High | 本轮保护性修复验收完成；外部竞态/崩溃边界保留 | NFO 50 + caller 10 + 小窗口 1；完整 Stage4 739/0 |
| 3 | R10 路径历史迁移 | Medium | 已核实；待实施 | 消费 R03 完整路径映射 |
| 4 | R01 展示与实时输出解耦 | High | 已核实；待实施 | 设置影响表与负向回归 |
| 5 | R04 输出生命周期 / client 隔离 | High | 机制确认；故障待复现 | 先确认 SDK 并发语义 |
| 6 | R05 FFmpeg 退出与产物发布 | High | 机制确认；故障待复现 | 约定截止与子进程所有权 |
| 7 | R06 对话框异常恢复 | High | 机制确认；故障待复现 | 分阶段故障注入 |
| 8 | R08 native 终止事件归属 | Medium | 身份缺口确认；可达性待复现 | 控制回调顺序 |
| 9 | R09 扫描结果归属 | Medium | 修订缺口确认；竞争待复现 | 捕获身份与修订 |
| 10 | R07 会话准备 / 多轴策略 | Medium | 策略差异确认；方案待决策 | 保留既有轴语义 |
| 11 | R11 媒体库成本与索引 | Low | 待测量；探索项 | 不预定 SQLite |

顺序遵循用户决定 R03 → R02 → R10 → R01。后续每次只处理一组关联问题；短期保护性修复可先于完整深化，但必须记录尚未收回的协议。R03 与本轮 R02 已验收；下一项建议为 R10，等待用户后续授权。

### B01：接管 UI 基线失败（已定位并修复测试隔离）

20261010-211546-1a8010 的完整Stage4：611通过、3失败，全部失败在Shell。相同成品+新配置复跑Shell仍328通过、相同3失败。分别为右侧边缘只唤出快捷调整、边缘调整不钉住无关播放栏、悬停常驻热力图不使其移位。其余7套件通过；额外六轴20项与表单9项通过。

旧测试断言原位置为MediaShellSmokeTests.cs:185/:189/:280；生产保持判断在MainWindow.PlaybackChrome.cs:135之后使用真实IsMouseOver，而测试传入模拟坐标却未移开真实鼠标。第一轮在旧成品上三项各重复两次：真实 transportHover=True 时全部失败，视频中央 False 时全部通过；三种顺序全部通过。播放/Seek/捕获/内嵌/键盘状态无冲突，原顺序强制悬停精确复现3项及完整调用栈。见[定位票](../.scratch/round1-r03-b01/issues/01-b01.md)与[失败栈](../.scratch/round1-r03-b01/evidence/B01-FAILURE-STACKS.md)。

仅测试隔离修复：真实鼠标移开、等待前置就绪、明确 hidden 前置、finally 恢复指针；保留原3断言并增加真实悬停超过隐藏延迟仍保持显示。未改生产显隐。旧成品修复后Shell334/0，最终完整Stage4 617/0。旧失败日志保持原样，不能把对照故意失败计入交付回归。

### O01：中间预览单次失败（开放）

中间run20261010-215917-43f686有1项 `Old on-demand preview cannot appear in a replacement session` 失败，与B01分列。后续完整回归未复现，旧成品2次中性指针90/0与主动悬停诊断92/0亦未复现。根因未确认，不认定为历史问题、不宣称已修复；不修改预览生产实现或原测试断言，见[独立定位票](../.scratch/round1-r03-b01/issues/03-preview-observation.md)。

## R03：关联文件变更 module

- **证据/文件**：MainViewModel.cs:6378 发现共同名、`-*`、`_*`；PlaylistViewModel.cs:822 建映射，:824 仅查磁盘目标，:846 顺序 Move，:863 仅留新共同名+最后扩展名。路径均在 `ScriptPlayer/ScriptPlayer/ViewModels`。
- **问题**：Movie.funscript、Movie-soft.funscript、Movie.twist.funscript 全映射 NewName.funscript。预检漏重复目标；执行中失败可能留下已移动文件，Entry 仍旧路径。静态映射确定，未改真实文件复现。
- **方案**：同一 module 拥有关联发现、保留作者/轴/侧车 suffix 的完整规划、唯一目标与路径冲突检查、可恢复执行及旧→新结果。先定义大小写等价、无改名和跨目录约束；禁止薄 MoveFile 包装代替协议。
- **测试**：临时树含视频、主脚本、-soft、_Handy、twist/pitch、NFO和图片；已有目标冲突零修改；注入第N次 Move 失败及恢复失败；Movie2 不误关联。
- **验收**：目标一一对应；内容哈希不变；成功后 Entry/重扫/磁盘一致；预检失败零副作用；执行失败恢复，恢复也失败时报告实际位置与可执行恢复步骤，绝不宣称成功。interface 是完整规划/执行协议；locality 集中命名和恢复，leverage 保护所有关联 caller。

**第一轮完成记录**：`Library/RelatedFileRename.cs` 拥有完整路径发现、完整suffix映射、Windows名称校验、全量预检、case-only临时路径、不覆盖Move、逆序回滚继续和可信result恢复。`PlaylistViewModel.RenameEntry` 为命令/测试共同入口，只在Success后发布路径；脚本选择在物理动作前捕获候选，避免同名外目录误关联。`MainViewModel` 提供同次目录与已加载/companion候选保护；不改加载和输出。完整result尝试CreateNew存入显式绝对APPDATA/ScriptPlayer/RenameOperations，保存失败明确保留内存结果。

核心80/0、真实caller38/0、Release构建、最终Stage4 617/0、额外WiFi20+9/0，独立审查已完成；不等于实物验收。完整快照1915项/1121336158字节全部SHA256一致，位于工作区 `ScriptPlayer-maintenance-snapshots/round1-20261010-213825`，恢复到新目录。生产范围只有csproj、MainViewModel、PlaylistViewModel和新增Library模块。

**残余边界**：result JSON不是逐Move持久事务；进程强制结束/断电窗口不保证自动恢复。Recover要求可信清单、当前文件未被外部替换，且无一键恢复UI。当前保护是配置候选，非实际轴来源持久身份。NFO内部引用与跨重启历史保持原行为，交由R02/R10；扫描竞争R09未顺手修复。测试曾误路由17份临时夹具报告到真实配置目录，已按内容归档并校验、修正显式APPDATA和测试守卫后重验；没有修改真实媒体。

## R02：元数据提交 module

- **证据/文件**：Controls/PlaylistControl.xaml.cs:165（:178 先改IsFavorite，:180 Save）；Metadata/NfoMetadata.cs:65 只读已知字段，:175 直接写目标；Dialogs/NfoEditorDialog.xaml.cs:68 共享 Save。
- **问题**：未知 XML 无法往返；收藏这种局部改动重写整个文档；写盘失败会留下内存/磁盘差异或截断风险。编辑器有异常提示，不代表提交安全。
- **方案**：读取原文档、局部修改、保留未知元素/属性/嵌套内容、临时文件提交和失败恢复由同一 module 拥有。明确并发外部编辑冲突，避免拿旧快照静默覆盖；提交成功后统一发布并刷新共享 NFO 条目。
- **测试**：未知 XML 语义保留；收藏仅改变收藏；只读目标、写入/替换失败、外部内容变化、损坏 NFO；共享 Movie-soft 回退；编码和重启解析。
- **验收**：非编辑字段无静默丢失；失败保留原数据且不发布成功；冲突明确反馈；相关投影一致。现有主页直接赋 IsFavorite 只测投影。真实临时目录 adapter 是 test surface，收藏和编辑共用 seam。

### R02 本轮完成状态（2026-10-11）

保留完整 DOM、原字节修订和实际 SidecarPath；草稿只改拥有字段，标签按原值关联扩展。收藏和编辑共用安全提交，确认成功后发布同 NfoPath 的所有 Entry，保留对象、封面、时长、播放状态和脚本选择。未知 XML 不静默丢失；损坏/DTD/旧文本写回、复杂字段无法无损编辑、外部修订或路径消失均明确拒绝。提交异常检查磁盘实际位置，保留恢复路径；已提交但清理失败显示警告。小窗口表单与错误提示分别滚动，保存/取消固定在可见底部。

旧模块红测 13/15、真实 caller 红测 0/5，最终 NFO 50/0、caller 10/0、小窗口 1/0；最终成品完整 Stage4 739/0、外观36/0、R03 caller38/0，未变 R03 核心80/0，合计954/0。设计、数据安全和最终独立审查均有源码与日志证据。630文件原字节快照、变更差异及身份核对见本轮报告。

**仍保留边界**：非协作外部写者与检查/替换/清理之间不是严格 CAS；不保证断电/强杀后的自动恢复。旧文本不自动迁移，复杂扩展拒绝修改而非删除；巨型 NFO 输入预算与 R09 慢扫描发布竞争仍待单独处理。无实物设备或多 DPI 验收。以上边界不能随本轮测试全绿关闭。

## R10：路径与运行历史 module

- **证据/文件**：MainWindow.PlaybackList.cs:15/:59/:71 按路径读写退出点；MainWindow.MediaShell.cs:35/:52 保存最近路径并过滤缺失文件；PlaylistViewModel.cs:785/:852 只改 Entry、Reset、排队；PlaylistEntry.cs:247 只通知名称。
- **问题**：程序内移动未发布路径迁移，退出点仍旧键；磁盘最近列表可能重启丢项。当次共享 Entry 可能跟随新路径，不将其误报为必然立即丢失。
- **方案**：消费 R03 成功/恢复后的真实完整映射，统一迁移最近、退出点并支持重复应用、提交失败与冲突。保留 Coordinator 的用户暂停资格；不顺手加入内容指纹、外部移动识别或数据库。
- **测试**：记录后改名/移动与跨启动恢复；已有目标历史冲突、重复映射、写入失败、文件恢复后历史恢复。联动列表/时间轴。
- **验收**：同一点位和最近顺序保留，无旧路径幽灵重复；失败不丢记录、状态与磁盘实际位置一致；迁移不新建退出时刻。现有 PlaybackExitTests 和 TimelineUiTests 资格断言全保留。多个实际 caller 证明 leverage。

## R01：实时配置失效 module

- **证据/文件**：MainViewModel.cs:2038–2174 所有 settings switch 后无条件 RefreshRealtimePlayback；MainViewModel.Realtime.cs:35–42 重建动作；Playback/RealtimePlaybackService.cs:38–42 每次版本更新并 Invalidate；PlaybackOutputPipeline.cs:51 安排 stop。
- **问题**：热力图、列表风格、字幕等纯展示变化影响设备停止路径。控制流确认，感知停顿和大脚本成本未测量。
- **方案**：先列设置→影响 module→失效范围表，区分 UI、脚本准备、运动参数和设备配置；复用不可变动作快照，有效变化才发布。不能取消必要暂停/Seek/会话切换屏障。
- **测试**：播放中反复改所有已登记展示设置，配置版本/重建/Stop计数不增加；delay/range/invert/fill 正向仍生效；连续批改一致；保存仍发生。
- **验收**：纯展示零输出 invalidation；相关运动变化正确且不重放旧帧；UI忙时实时回归通过。interface 收回失效协议，提高 locality，不让每个设置入口独自判断。

## R04：输出生命周期与共享 client

- **证据/文件**：PlaybackOutputPipeline.cs:67/:79/:83 同步等Task，:101 无界Join；MainViewModel.cs:3761 UI移除先Dispose实时；Shared/Devices/ButtplugAdapter.cs:20、:260–355 共享锁覆盖发送/停止。
- **问题**：未完成I/O可阻塞移除/退出；一个client下慢设备影响其他输出。独立Sink隔离不能证明真实共享client隔离。
- **方案**：分别计划draining/关闭预算/故障隔离/安全释放，以及SDK允许的每设备与全client并发；不得直接去锁或超时后释放仍被调用的资源。保留现有单槽seam与真实adapters。
- **测试**：隔离子进程+父级watchdog覆盖永不完成Send/Stop、拒绝取消、断连、Stop抛错、共享client两设备竞争。已有可手工释放Sink及正常移除仅为成功基线。
- **验收**：UI无无限等待；在途未结束不释放；故障不补发旧帧；并发/公平/停止优先级有SDK证据；关闭结果可诊断。期限应在实施设计中明确，不编造统一数字。

## R05：FFmpeg 执行与任务 module

- **证据/文件**：Generators/GeneratorWorkQueue.cs:148、:183–192；Shared/Classes/Ffmpeg/Wrappers/ConsoleWrapper.cs:69/:76 无界Wait/Join；FfmpegConsoleWrapper.cs:20 仅写q；Playback/TimelinePreviewService.cs:47–67 已有4秒超时。
- **问题**：队列已存在；legacy取消与退出无统一保证，潜在影响进程收尾，维持High影响评级；忽略q情形未复现，不等于频繁故障。
- **方案**：集中进程所有权、取消升级、deadline、读流诊断、晚结果失效和安全产物提交；减少generator反向读VM；保留hover/batch优先级、SkipExisting与格式差异。
- **测试**：忽略q、流量洪峰、启动失败、非零退出、坏PNG、磁盘失败、取消晚结果、退出与queue同时关闭；真实FFmpeg正常预览保留。
- **验收**：归属子进程在约定截止内结束；取消不发布成功；部分文件不被认作缓存；状态/进度/跳过规则不退化。用失败进程adapter检验已有seam，不再加重复队列。

## R06：内嵌对话框生命周期 module

- **证据/文件**：InlineDialogService.cs:28 起注册session、禁用快捷键/父层，:48移交Content，:68才try/finally。MediaShellSmokeTests.cs:485后覆盖正常/取消/嵌套。
- **问题**：初始化异常可能绕过恢复，残留IsOpen、快捷键或父层禁用；尚未注入失败复现。
- **方案**：从首项副作用起拥有获取、附着、完成和部分初始化恢复；按已获取资源恢复，旧Window与宿主布局为内部adapters。保留内嵌UX。
- **测试**：归一化、资源注入、事件订阅、附着分别失败；嵌套失败、关闭中失败；正常焦点/键盘/布局。
- **验收**：session清除、内容所有权正确、父层/全局命令/焦点恢复；下层对话框不受破坏。此module通过deletion test，深化比替换成新窗口有leverage。

## R08：native 加载生命周期 module

- **证据/文件**：Shared/Controls/MpvController.cs:179 loaded检查路径，:191终止无身份；MpvVideoPlayer.cs:81经Dispatcher影响当前completion；PlaybackCoordinator.cs:35已有旧Task保护。
- **问题**：晚到的旧终止事件可能错归新请求；静态缺口确认，可达性待验证，推荐强度Worth exploring。
- **方案**：先控制事件顺序复现，再让native adapter拥有单次加载归属；caller不需同时理解多个编号。保留Coordinator。
- **测试**：A错误/EOF先排队，open B后释放；相同路径重开；超时晚成功；dispose后回调；失败后恢复。
- **验收**：旧回调不完成新Task、不改变新state、不触发新媒体ended；当前错误仍可见并可恢复。避免只对FakeEngine旧Task重复测而漏真实seam。

## R09：媒体扫描 module

- **证据/文件**：PlaylistViewModel.cs:1028仅开始检查Removed，后续读可变Fullname并写Entry；:1409 Reset后再排队；PlaylistEntry.cs:364清状态。MainViewModel.cs:2540播放thumbs已有版本保护。
- **问题**：改名、移除、Reload与慢扫描交错可能发布过期数据；未复现串图。
- **方案**：捕获路径+修订；计算完整结果；合并请求/取消/过期检查由module拥有，UI Dispatcher一次发布投影。
- **测试**：阶段信号控制扫描阻塞时改名、移除、多次Reload、Dispose；旧结果最后完成；解析失败不得混合新旧字段。
- **验收**：过期零发布；当前一次完整发布；无跨媒体数据混合。复用现有身份经验，避免七个浅interface；时间轴请求测试不是本项覆盖。

## R07：会话准备与轴策略 module

- **证据/文件**：MainViewModel.cs:3996加载/:5428更新协议/:2662轴装配；Realtime.cs:35普通变换；PlaybackDeviceOutput.cs:58 TCode只收时刻；Shared/Devices/TCodeSerialController.cs:155独立插值。
- **问题**：共同时间源、不同填充/变换语义；可能是有意策略。大型准备仍UI编排，UI stall运行通过不代表准备成本通过。
- **方案**：先明确轴优先级、变换作用范围、缺失轴与边界；再集中原始tracks和一致配置准备，transport adapter保留格式、节流与发送；未经确认不统一global transforms。
- **测试**：六轴精确样例/扩展轴、内嵌/后缀优先级、坏/空companion、重复时间、轴减少、快速换片改参数、准备预算、传输失败；比较普通与TCode策略。
- **验收**：file+generation一致结果一次发布；轴差异有书面策略；原TCodeWifiTests六轴、25ms、delay、移除回中全保留；Stage4实时L0/R0及AB/Seek通过。提升locality而非叠加透传manager。

## R11：媒体库规模测量

- **证据/文件**：MainViewModel.cs:5193同步递归；PlaylistViewModel.cs:1502线性去重/:1420全库NFO刷新；HomeMediaViewModel.cs:61已有合并。
- **问题**：批量比较有二次方结构，当前主要瓶颈未测，Worth exploring。
- **方案/测试**：隔离1,000/5,000条；冷/热首屏、枚举、去重、关联、NFO、图片分别计时/计数；重复、大小写、坏NFO、额外目录；再决定成员索引/增量/SQLite adapter。
- **验收**：分阶段证据和机器条件齐全，确定主要成本后制定门槛；不承诺一秒、不预定数据库、不破坏已有主页批量投影。一个adapter不足以证明新增seam有用。

## 状态更新规则

计划 → 用户确认 → 在本地票记录 claimed → 失败回归证明 → 小范围implementation → 相关测试+编译+适用回归 → 独立审查 → 验收满足后 resolved。受阻/失败保留日志，不能用文档中的“已完成”代替测试。此总表只记录风险总状态和下一项，操作讨论留在任务票 Comments。

当前交付：公开构建/私人配置隔离 v1.0.12.3，完整 Stage4 766/0与专项33/0；R02保护性修复保留。下一项建议：R10 路径历史迁移；R03 的完整路径映射已保留，尚未开始迁移真实历史。

## 标题栏版本入口（2026-10-10）
按用户授权完成标题栏版本按钮及内嵌关于内容，统一v1.0.12.0；完整Stage4 625/0。保留R02/R10等待实施状态。见 ../.scratch/version-button/REPORT.md。

## 时间轴维护验收（2026-10-11，v1.0.12.2）

- 用户授权热图/进度贴合及分离底边唤醒；未扩大到R10/R01。634原字节快照、before/after差异、真实源码/测试/成品身份均见 `.scratch/round4-timeline`。
- 旧版邻接/热区红测139 PASS/7 FAIL/0 ERROR；重复无缓存真实caller红测148 PASS/2 FAIL/1超时ERROR。两者故意失败，不混入验收。
- run215055 fast-phase stack定位Dispatcher缩略图重查→SetPresentation→无条件通知→ClosePreview。现按引用/值变化通知，marker按Position/Kind/Label；真实变化及会话失效仍清理。完整中间失败四次保留，不能认定为FFmpeg慢。
- 冻结完整 `runs/20261011-220058-872e69` 八套件761 PASS/0 FAIL/0 ERROR，stage4=true、coreOnly=false、suitesFailed=[]；主项目Release通过，独立审查完成。原5秒、最新请求、cache替换、媒体归属和拖动断言未降低。
- **O02补充**：本轮同断言重复无缓存通知原因已确定性修复；历史112305未记录对应栈，不能保证历史全部症状都同根因。保留其历史观察与有序故障测试，不以复跑通过全面关闭。O01、实物设备、长期负载、多DPI/多屏及R10等仍未验收。

## 公开发布隐私维护（2026-10-11）

按用户授权处理FFmpeg展示路径、Handy私密存储、输入遮蔽和公开候选导出；未扩大到R10等架构风险。原761基线，634项目文件实际before字节与构建脚本额外快照。v1.0.12.3公开强制Rebuild后PE个人路径零命中；完整runs/20261011-222737-f89f04为766/0，凭据专项33/0（含审查发现XML损坏默认回退风险的确定性红/绿闭环）。独立审查无剩余生产阻断。见 .scratch/round5-release/REPORT.md 与 privacy-review.md。

真实旧profile仅读取用于内存候选比对、不修改，原密钥不显示/不落报告；候选目录与ZIP已知个人路径/密钥零命中，无拒读。发布前仍需第三方完整对应源码与构建材料和实际Fork地址；不因隐私检查通过宣称可无条件公开分发二进制。DPAPI不跨机器、双文件非事务、新机干净构建、实物设备与长期负载边界保留。R01/R10等按原顺序，不自动关闭。

## 上游组件下载式发行包（2026-10-12）

用户确认保留已测试FFmpeg/libmpv，由程序附带下载脚本从原作者取得，不将原组件二进制作为本轮附件。应用仍v1.0.12.3，生产代码和播放核心不变；完整历史依赖与构建材料继续PARTIAL，未关闭此前公开分发待补项。

新包包含程序与双击安装入口、应用源码、三份精确核心源码/公共构建索引及SHA256。安装器全部预检、两层哈希校验、绝不覆盖，部分失败逆序回滚，恢复记录写失败也保留夹具状态并报告。WindowsPowerShell5.1入口幂等、默认策略、UTF8解析和模块环境问题均有实际测试证据；CMD仅设置本进程执行策略，不修改持久策略。13故障+6入口+5官方组件检查通过；ZIP和字节身份通过，旧冻结候选不变。没有新播放器回归、实物设备测试或GitHubRelease发布。详见 .scratch/runtime-release-20261012/REPORT.md。
