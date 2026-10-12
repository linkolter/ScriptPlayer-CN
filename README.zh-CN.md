# ScriptPlayer

[English](README.md) | **简体中文**

*让播放回归简单，让体验更加自由。*

一款面向 Windows 的本地视频与 Funscript 脚本播放器，重新整理了界面、媒体库和围绕脚本播放的时间轴，让日常操作更加自然、顺手。

当前本地候选：**v1.0.12.3**。发布准备完成后可从 [Releases](https://github.com/linkolter/ScriptPlayer-CN/releases) 下载。新分包目前仅在本地准备，尚未上传。

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
- 内置 FFmpeg 从程序目录定位，不暴露开发者安装地址。
- Handy 密钥由每位用户自行配置，以 Windows 当前用户加密保存；发行包不包含开发者私人配置或连接密钥。

新配置默认使用简体中文，可在 **设置 → 外观** 切换为英文。README 的语言不会自动改变程序语言。

## 下载与安装

计划提供三份 Windows x64 下载：

1. `ScriptPlayer-v1.0.12.3-windows-x64-app.zip`
2. `ScriptPlayer-v1.0.12.3-windows-x64-mpv.zip`
3. `ScriptPlayer-v1.0.12.3-windows-x64-ffmpeg.zip`

**将三个 ZIP 全部解压到同一个文件夹**，保留文件与子目录。`ScriptPlayer.exe`、`libmpv-2.dll` 和 `ffmpeg.exe` 必须处于同一层，再运行程序并添加自己的媒体和脚本。详见[安装说明 / Installation](INSTALL.md)。

更新时仅在运行组件哈希与该版本 `runtime-manifest.json` 一致的情况下复用旧组件。更换电脑或 Windows 用户后需要重新输入 Handy 密钥。

**运行组件的公开分发准备仍未完成。** 已确认核心源码提交，但当前 mpv/FFmpeg 构建的全部依赖对应源码与构建材料尚未齐备。单独放到 Releases 不会取消这些要求。详见[第三方源码状态](docs/THIRD_PARTY_SOURCES.md)。目前运行组件 ZIP 只作为本地候选。

## 源码与构建

WPF + .NET Framework 4.8 + libmpv。主工程：`ScriptPlayer/ScriptPlayer/ScriptPlayer.csproj`；解决方案：`ScriptPlayer/ScriptPlayer.sln`。

候选源码提供 `tools/Build-Public.ps1` 与 `tools/runtime-manifest.json`。构建前恢复依赖包，并将匹配的运行组件放入 `ScriptPlayer/ScriptPlayer/Runtime`。本机已验证构建使用已有 MK312 产物，**尚未验证新机器从零构建整个解决方案**，不能将脚本理解为干净环境的一键构建保证。

## 反馈

报告问题时请提供版本、Windows 版本、复现步骤和设备型号，不上传连接密钥或私人配置。设备支持不代表全部硬件、固件与显卡组合均已验证。

## 关于

这是一个出于个人兴趣持续打磨的社区项目，希望本地播放更加自然、顺手。

基于 [FredTungsten/ScriptPlayer](https://github.com/FredTungsten/ScriptPlayer) 深度二次开发，保留脚本同步与设备控制基础。非官方衍生版本，遵循 [GPL-3.0](LICENSE)，来源与修改说明见 [NOTICE-CN.md](NOTICE-CN.md)。感谢原作者及所有开源贡献者。

*Enjoy your freedom.*
