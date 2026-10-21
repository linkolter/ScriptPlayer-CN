# ScriptPlayer

[English](README.md) | **简体中文**

*让播放回归简单，让体验更加自由。*

一款面向 Windows 的本地视频与 Funscript 脚本播放器，重新整理了界面、媒体库和围绕脚本播放的时间轴，让日常操作更加自然、顺手。

当前源码版本：**v1.0.12.6**。更新后的便携包目前为本地候选，上传发行附件是独立步骤。实际可下载文件请查看 [Releases 发布页](https://github.com/linkolter/ScriptPlayer-CN/releases)。

主页优先展示设备连接，Handy、Wi-Fi、USB 与蓝牙 SPP/COM 统一在设置子页配置。蓝牙需要 Windows 已配对并提供传出 COM 端口，本轮不增加 BLE/GATT。见[变更与验证边界](CHANGELOG.md)。

动画设置位于 **设置 → 操作与外观 → 动画与性能**，提供关闭、轻量、标准和自定义模式；默认轻量，可逐项调整并关闭动态预览。系统关闭动画或最小化时会停用相应效果。

## 播放与浏览

- libmpv 负责视频播放，FFmpeg 提供缩略图等媒体处理能力。
- 左侧大封面与底部单行标题，方便识别和切换视频。
- 左侧列表、底部控制栏与右侧调整面板独立显示；拖动或打开菜单时保持对应控件显示。
- 热力图与进度条整合展示书签、AB 点和暂停退出记录；悬停预览辅助定位片段。
- 热力图可以保持可见，悬停其上不会立即唤起整组底部控制按钮。

## 媒体、脚本与设备

- 本地媒体库、封面、NFO 元数据、标签与收藏。
- 同一视频可关联不同作者的脚本，并加载所选脚本对应的多轴伴随文件。
- Funscript 同步、多轴脚本、TCode 输出和 Handy 连接。
- 普通播放历史与暂停退出记录分开：只有明确暂停后正常退出才记录暂停退出点，切换视频不生成该记录。

## 界面与设置

- WPF 界面统一字体、按钮、间距和滑动开关。
- 简体中文与英文、深色与浅色主题、五种强调色。
- 动画可关闭；窄窗口优先保留常用播放操作。
- 安装后的 FFmpeg 从程序目录定位，不暴露开发者安装地址。
- Handy 密钥由每位用户自行配置，以 Windows 当前用户加密保存；发行包不包含开发者私人配置或连接密钥。

新配置默认使用简体中文，可在 **设置 → 外观** 切换为英文。README 的语言不会自动改变程序语言。

## 下载与安装

最新本地候选：`ScriptPlayer-v1.0.12.6-windows-x64-portable-candidate.zip`。
需要 Windows 10/11 x64 和 .NET Framework 4.8。完整解压到新的可写目录，直接运行 `ScriptPlayer.exe`；保留所有 DLL 和子目录，无需在线组件安装器或 7-Zip 安装步骤。升级时使用新目录，不覆盖旧版和用户配置。详见 [安装说明](INSTALL.md)。

本地便携候选包含 MSYS2 UCRT64 mpv 0.41.0-8（mpv v0.41.0）和 Gyan FFmpeg 9.0.2 full。组件身份及校验见 `tools/runtime-manifest.json`。源码仓库不跟踪这些二进制，单独下载源码不会自动获得组件。

完整第三方对应材料审查仍待完成；没有宣称在另一台干净电脑或实物设备上完成验证。详见 [运行组件材料状态](RUNTIME-MATERIALS.md)。旧 v1.0.12.3 在线安装脚本不用于此候选。

## 源码与构建

WPF + .NET Framework 4.8 + libmpv。主工程：`ScriptPlayer/ScriptPlayer/ScriptPlayer.csproj`；解决方案：`ScriptPlayer/ScriptPlayer.sln`。

本仓库提供 `tools/Build-Public.ps1` 与 `tools/runtime-manifest.json`。构建前恢复依赖包，并将匹配的运行组件放入 `ScriptPlayer/ScriptPlayer/Runtime`。本机已验证构建使用已有 MK312 产物，**尚未验证新机器从零构建整个解决方案**，不能将脚本理解为干净环境的一键构建保证。

根目录 `SHA256SUMS.txt` 记录已准备的 ZIP 校验值（同时保留 v1.0.12.4 候选校验），不用于校验源码目录。PARTIAL 材料包不是完整对应源码包。开发固定使用当前检出目录，参见 [工作目录约定](docs/WORKSPACE.md)。NFO 格式及示例见 [中文指南](docs/NFO.zh-CN.md) 与 [English guide](docs/NFO.en.md)。

## 反馈

报告问题时请提供版本、Windows 版本、复现步骤和设备型号，不上传连接密钥或私人配置。设备支持不代表全部硬件、固件与显卡组合均已验证。

## 关于

这是一个出于个人兴趣持续打磨的社区项目，希望本地播放更加自然、顺手。

基于 [FredTungsten/ScriptPlayer](https://github.com/FredTungsten/ScriptPlayer) 深度二次开发，保留脚本同步与设备控制基础。非官方衍生版本，遵循 [GPL-3.0](LICENSE)，来源与修改说明见 [NOTICE-CN.md](NOTICE-CN.md)。感谢原作者及所有开源贡献者。

*Enjoy your freedom.*
