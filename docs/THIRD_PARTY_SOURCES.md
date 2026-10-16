# FFmpeg/libmpv 对应材料

历史核实日期：2026-10-12，对应 v1.0.12.3；2026-10-16 新增 v1.0.12.4 本地集成记录。两轮材料身份分开，已有 ZIP 均不改写。

## v1.0.12.4 本地集成候选（2026-10-16）

此节描述新版；下文旧身份/缺口属于历史v1.0.12.3，不可混用。新版mpv0.41.0采用官网列出的MSYS2 UCRT64 0.41.0-8共享库，独立FFmpeg采用用户确认的Gyan9.0.2 full build。

- 官方mpv v0.41.0 x64 MinGW/MSVC原包均下载并核对GitHub资产SHA256，检查实际附件后确认未附libmpv共享DLL。
- Gyan原包 `ffmpeg-9.0.2-full_build.7z` 168971636字节，SHA256 `F0E46253C70DFE902BAC915DFB4224F0CF1B7C6EEAB9DA2CCC9A5581F9A71B13`；新EXE与131DLL的精确大小/哈希及MSYS包来源、许可证字段保存在程序旁 `runtime-versions.json`。
- MSYS2 mpv包 `mingw-w64-ucrt-x86_64-mpv-0.41.0-8-any.pkg.tar.zst` SHA256 `ACA9CE7359C63CAA33612D160EDAD059FD931E1C434A87D6CA01F5AF015CAC76`；完整包依赖索引及146原包已锁定、校验，只将实际导入闭包131DLL用于候选。记录的普通winpty依赖为CLI用途，不是本轮libmpv导入。
- 已保存上游mpv v0.41.0、FFmpeg9.0.2核心源码，以及MSYS2 mpv精确source package（含原始tar和PKGBUILD），后者SHA256 `FA594A8E13FA22300435925EBE57ECA1C2A4015B68F00BF506C99FD6A6AFF1A4`。保留各包已有许可证、mpv版本化Copyright/GPL/LGPL、GyanREADME与配置。保存源码不表示所有依赖材料已逐项审查。
- 待完成：核验Gyan新版全部关联依赖、MSYS2所选DLL对应source package/构建输入与逐项许可材料，形成可公开提供的完整索引。当前清单明确 `CompleteCorrespondingSourceMaterials=false`。本轮只交付本地候选，未上传或修改GitHub；旧PARTIAL附件不作为新版对应材料。

## 历史 v1.0.12.3 已确认身份

|分发文件|原始发行|核心源码完整提交|当前文件 SHA256|
|---|---|---|---|
|ffmpeg.exe|Gyan 8.1 full build|9047fa1b084f76b1b4d065af2d743df1b40dfb56|D1E2A156261ECC675081943197A85F08F2868784A0AF499171EDE89353EDAD31|
|libmpv-2.dll|shinchiro 20260814 x86_64 dev|mpv 7b8915bc1d04c7e1b61184e00c7fbfaab1911e75|F709C7CA8B183BEC76B8158BF0C45C53018C63366750729352612F228FF7BDEA|
|DLL 内部 FFmpeg|同一次 mpv 构建|1d7b14f61d66fdf18f15204c613df9d65396c319|随 DLL 分发，没有独立文件哈希|

原 FFmpeg 7z SHA256：4dcbe10fa72bffa3e976d09cf3335f53a2716a8041af7a047609898fe6019069。
原 mpv dev 7z SHA256：0af22b28e920620036d3ae08fd9283156dc9af0420bf4df84b0e02282094599c。

重新下载的两个原包均通过 GitHub release asset digest；以 7-Zip 流式读取单个 EXE/DLL，计算结果与当前候选逐字匹配。内嵌 FFmpeg 的短版本字符串在实际 DLL 中存在，原 x86_64 构建日志也记录该提交。不用独立 FFmpeg 的 9047fa1 源码替代 DLL 内部的 1d7b14f 源码。

## 已取得材料与缺口

- 三份核心源码 tar.gz：独立 FFmpeg、mpv、DLL 内部 FFmpeg，全部按完整提交获取并记录 SHA256。
- mpv-winbuild-cmake 配方快照：cd1edc11dc6887a50f705717619d879f5a93a488，包括 CMake、补丁、workflow、安装环境说明。它是构建配方，不是包含所有依赖的源码包。
- 原 Gyan README：配置与依赖版本清单；当前 ffmpeg.exe 的 `-version`、GCC 15.2.0/Rev11、完整 configure 参数。
- 相应核心许可证及 mpv Copyright，依赖清单与独立研究报告。
- 原 mpv x86_64 job 94631245419 的认证读取日志。日志保留容器 digest、CMake 参数、部分依赖更新线索；匿名 403 不代表日志不存在。

**缺口仍未关闭：**

1. Gyan README 中 openal-soft 标为 latest，部分库只有版本/短提交；压缩、加密、字体等传递依赖的精确版本、补丁及构建步骤没有全部给出。需要该发行完整源码/配方快照，不能用当前 MSYS2 包替代历史输入。
2. mpv 构建使用源码/工具链缓存、浮动 Git 依赖及容器 latest。实际容器 digest 已从日志取得，但多数缓存依赖没有 HEAD 清单。某些配方有 GIT_RESET，应按 reset 核实，不取日志里其他分支的 tip。
3. mpv 配置细节附件 mpv-x86_64-logs，artifact ID 9203485392，原 ZIP 217726 字节、SHA256 c404359920233fd8f30d64804f45f6a1f432696fb1dcbfb842bc42682e915283。配方保留 1 天；本轮认证下载返回 404，不能用新版日志补上。
4. libass、libplacebo 等精确历史输入仍未获证实；依赖源码、传递依赖、实际补丁和完整配置尚未组成对应闭包。部分更新短提交与版本清单不是完整锁文件。
5. 独立审查在该构建配方快照中未找到单独 LICENSE/COPYING 文件。公开转载配方前需核实其授权；核心 FFmpeg/mpv 许可证不能自动覆盖构建者脚本。当前材料仅作为本地部分附件，逐项许可仍待核实。

## 可执行补齐路线

优先保持当前二进制：向两位构建提供者索取对应原包的 source snapshot、完整依赖清单、补丁及实际构建配置。项目内已准备不含个人路径/密钥的英文请求草稿，尚未发送。取得后核实完整提交、子模块、压缩包哈希、许可证、配方与当前发行身份，生成独立材料附件。

若历史快照无法取得，采用受控构建：保留 WPF + libmpv 接入，固定 mpv/FFmpeg 与依赖提交、源码压缩包哈希、子模块、补丁、容器镜像 digest 和工具链版本；完整保存输入，再编译新的运行组件。不按日期猜旧 HEAD，不将随手下载新版库称为旧二进制对应源码。

构建替代件首先放独立候选；检查导出 API、加载、视频/音频、硬件解码回退、Seek/速度/AB、脚本同步和停止生命周期，FFmpeg 另验缩略图、预览、媒体信息和取消/退出；运行完整 Stage4 与相关专项后再决定纳入新版本。受控构建不要求与旧二进制逐字一致，但必须对应自己的源码、配置和最终哈希。生产替换属于下一次明确实施任务。

公开发行组织：代码仓库保存来源清单、许可证、构建说明和材料索引；Releases 同版本提供程序、程序源码及第三方源码/构建材料附件，建立可用的下载入口，不只写提供者主页。若依赖有许可证义务差异，逐项记录；不把所有依赖统一假设为 GPL。

## 当前结论与参考

已完成原包/二进制/核心提交匹配，取得部分实物材料；**未完成全部依赖对应材料，现有便携候选的公开发行待补项保持开放。** 本轮没有运行下载的构建脚本或用 Git 更改本地项目，没有上传/发送请求。播放器测试结果沿用该候选既有记录，本轮只做材料读取、下载、哈希和压缩包检查。

- [Gyan 8.1 发行记录](https://github.com/GyanD/codexffmpeg/releases/tag/8.1)
- [mpv 20260814 发行记录](https://github.com/shinchiro/mpv-winbuild-cmake/releases/tag/20260814)
- [原 mpv 构建工作流](https://github.com/shinchiro/mpv-winbuild-cmake/actions/runs/31755832255)
- [固定构建配方](https://github.com/shinchiro/mpv-winbuild-cmake/tree/cd1edc11dc6887a50f705717619d879f5a93a488)
- [FFmpeg 分发说明](https://ffmpeg.org/legal.html)

证据与材料清单位于 `.scratch/runtime-source-materials/`；其中原始二进制包用于核验，不放入第三方源码材料 ZIP。材料 ZIP 明确标记 PARTIAL，不能作为已经合规完备的声明。
