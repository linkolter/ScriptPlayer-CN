# ScriptPlayer 中文增强版

面向中文用户的 ScriptPlayer 增强版本。在原版的视频与脚本同步播放能力之上，本项目加入中文界面、媒体库管理、多作者脚本选择、内嵌 MPV 播放器以及 TCode 多轴设备支持。

> [!IMPORTANT]
> 本项目是 [FredTungsten/ScriptPlayer](https://github.com/FredTungsten/ScriptPlayer) 的第三方中文增强 Fork，并非原作者提供或维护的官方中文发行版。项目保留上游来源说明，并继续遵循 [GNU GPL-3.0](LICENSE) 许可证。

当前版本：`1.0.11`

## 当前功能

### 中文界面

- 对菜单、设置、提示信息及主要操作界面进行了中文化。
- 增加常见标签的中文翻译，便于整理和浏览本地媒体。

### 中文增强播放列表

- 重新设计媒体列表显示方式。
- 支持封面、标签、收藏和脚本数量等信息。
- 支持读取本地 NFO 信息，便于管理本地视频库。

### 多作者脚本选择

这里的“多脚本”是指：**同一个视频可以对应不同作者制作的多份脚本，每次播放时选择其中一位作者的脚本进入视频。**

例如：

```text
影片.mp4
影片-作者甲.funscript
影片-作者甲.twist.funscript
影片-作者甲.surge.funscript
影片-作者乙.funscript
影片-作者乙.twist.funscript
```

选择“作者甲”后，只会加载作者甲的主脚本及其同名多轴脚本；`.twist`、`.surge` 等多轴伴随文件不会被误显示成独立作者。

### 内嵌 MPV 播放器

- 可在 ScriptPlayer 窗口内部播放视频。
- 播放画面限制在视频区域中，不再覆盖播放列表、时间轴和其他操作界面。
- 使用 D3D11，并支持 MPV 硬件解码，降低部分电脑播放高清视频时的 CPU 占用。
- 仍保留原有播放方式，方便按设备和兼容性需求切换。

### TCode 多轴支持

- 支持通过串口连接 TCode、OSR2、SR6 等兼容设备，默认波特率为 `115200`。
- 支持 TCode v0.3 常用通道：`L0`、`L1`、`L2`、`R0`、`R1`、`R2`、`V0`、`A0`、`A1`、`A2`。
- 支持独立的 `.surge`、`.sway`、`.twist`、`.roll`、`.pitch` 等多轴 funscript 文件。
- 支持 funscript 1.1 的 `axes` 结构和 funscript 2.0 的 `channels` 结构。
- 连接后可在界面中查看串口及当前已加载的轴。

> [!WARNING]
> 多轴设备首次使用时，请先降低设备速度和活动范围，并确认各轴方向、限位与机械结构安全。不同固件和设备的轴定义可能存在差异。

## 下载与使用

可在本仓库的 [Releases](https://github.com/linkolter/ScriptPlayer-CN/releases) 页面下载便携版压缩包。

1. 下载对应版本的压缩包并完整解压。
2. 运行 `ScriptPlayer.exe`。
3. 在播放方式中选择所需播放器；使用内嵌播放器时可选择“MPV 内嵌”。
4. 使用 TCode 设备时，从设备菜单选择串口并连接。

生成视频预览图或缩略图时，可能仍需安装 [FFmpeg](https://ffmpeg.org/download.html#build-windows)。部分原版设备连接功能可能需要安装 [Visual C++ Redistributable x64](https://aka.ms/vs/17/release/vc_redist.x64.exe)。

## 后续计划

以下内容是当前开发方向，不代表已经完成，也不承诺固定发布时间：

- **低配电脑性能优化**：减少重复计时器回调、目录重复扫描和不必要的界面刷新。
- **大媒体库优化**：为封面列表加入虚拟化、缩略图按需加载和缓存控制，降低内存占用。
- **播放与时间轴优化**：减少时间轴重复计算和绘制，改善长视频及高密度脚本下的流畅度。
- **MPV 稳定性与布局**：继续完善窗口缩放、全屏、切换播放方式和不同显卡环境下的兼容性。
- **多作者脚本体验**：完善作者识别、选择记忆、命名规则提示及异常文件处理。
- **TCode 多轴完善**：增加轴映射、范围、反向、速度和设备配置选项，并扩大真机测试范围。
- **稳定性修复**：针对低配电脑、旧版 Windows、不同显卡驱动和串口设备持续排查崩溃问题。
- **汉化补全**：继续清理遗漏的英文界面与提示文本。

## 问题反馈

提交问题时，建议同时提供：

- 中文增强版版本号；
- Windows 版本和电脑大致配置；
- 使用的播放方式；
- 可复现步骤及错误截图；
- 若与脚本有关，请说明文件命名方式和脚本格式；
- 若与 TCode 有关，请说明设备、固件版本及使用的轴。

请勿在公开问题中上传包含隐私信息或无权分发的视频与脚本。

## 项目来源与许可证

- 上游项目：[FredTungsten/ScriptPlayer](https://github.com/FredTungsten/ScriptPlayer)
- 上游作者：[FredTungsten](https://github.com/FredTungsten) 及 ScriptPlayer 贡献者
- 中文增强版修改说明：[NOTICE-CN.md](NOTICE-CN.md)
- 开源许可证：[GNU General Public License v3.0](LICENSE)

感谢 ScriptPlayer 原作者和所有上游贡献者。原始代码版权归相应作者所有；本 Fork 的中文化与增强修改继续按 GPL-3.0 发布。分发修改后的二进制版本时，应同时提供或明确指向对应版本的完整源代码。

## 构建说明

源码解决方案位于 `ScriptPlayer/ScriptPlayer.sln`。本项目继承自较早期的 Windows/.NET Framework 桌面工程，构建时需要与项目引用相匹配的 Visual Studio、.NET Framework 开发组件及 NuGet 依赖。

如需了解原版安装、故障排查和设备兼容信息，请参考 [上游 Wiki](https://github.com/FredTungsten/ScriptPlayer/wiki)。
