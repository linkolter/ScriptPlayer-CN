# ScriptPlayer

[English](README.md) | **简体中文**

*让播放回归简单，让体验更加自由。*

一款面向 Windows 的本地视频与 Funscript 脚本播放器，重新整理了界面、媒体库和围绕脚本播放的时间轴，让日常操作更加自然、顺手。

当前版本：**v1.0.12.3**。应用源码在本仓库公开；Windows 程序包、配套源码和安装说明请查看 [v1.0.12.3 发布页](https://github.com/linkolter/ScriptPlayer-CN/releases/tag/v1.0.12.3)。

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

下载 `ScriptPlayer-v1.0.12.3-windows-x64-online-install.zip`，包含程序和已审查的组件下载脚本，不附 FFmpeg/libmpv 二进制。

完整解压到新的可写目录，安装官方 [7-Zip](https://www.7-zip.org/)（若已有则复用），双击 `Install-Runtimes.cmd`。脚本从 Gyan、shinchiro 下载已测试的精确组件，校验压缩包及提取文件的大小与 SHA256，绝不覆盖已有组件，然后启动 `ScriptPlayer.exe`。启动器仅为当前安装进程允许本地脚本，不修改系统持久执行策略。详见[安装说明 / Installation](INSTALL.md)。

首次组件下载约 183 MiB。已有匹配组件时不需要下载或解压器；组件冲突时停止。更换电脑或 Windows 用户后需要重新输入 Handy 密钥。

**完整历史第三方材料仍待补。** 精确核心源码以 PARTIAL 附件提供，不能作为完整对应源码包。旧版 mpv/FFmpeg 二进制分包继续保留为本地候选，不属于此次下载附件。详见[运行组件材料状态](RUNTIME-MATERIALS.md)。

## 源码与构建

WPF + .NET Framework 4.8 + libmpv。主工程：`ScriptPlayer/ScriptPlayer/ScriptPlayer.csproj`；解决方案：`ScriptPlayer/ScriptPlayer.sln`。

本仓库提供 `tools/Build-Public.ps1` 与 `tools/runtime-manifest.json`。构建前恢复依赖包，并将匹配的运行组件放入 `ScriptPlayer/ScriptPlayer/Runtime`。本机已验证构建使用已有 MK312 产物，**尚未验证新机器从零构建整个解决方案**，不能将脚本理解为干净环境的一键构建保证。

根目录 `SHA256SUMS.txt` 记录整理源码包内文件的原始字节；Git 换行规范化及后续文档更新后，不应用它校验当前检出目录。

## 反馈

报告问题时请提供版本、Windows 版本、复现步骤和设备型号，不上传连接密钥或私人配置。设备支持不代表全部硬件、固件与显卡组合均已验证。

## 关于

这是一个出于个人兴趣持续打磨的社区项目，希望本地播放更加自然、顺手。

基于 [FredTungsten/ScriptPlayer](https://github.com/FredTungsten/ScriptPlayer) 深度二次开发，保留脚本同步与设备控制基础。非官方衍生版本，遵循 [GPL-3.0](LICENSE)，来源与修改说明见 [NOTICE-CN.md](NOTICE-CN.md)。感谢原作者及所有开源贡献者。

*Enjoy your freedom.*
