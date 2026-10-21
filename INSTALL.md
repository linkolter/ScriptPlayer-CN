# Installation / 安装

## English

Prepared local candidate: `ScriptPlayer-v1.0.12.6-windows-x64-portable-candidate.zip`.
Check the repository Releases page for whether that attachment has been published.

1. Use Windows 10/11 x64 with .NET Framework 4.8.
2. Verify the ZIP against the matching SHA256SUMS.txt.
3. Extract the complete ZIP into a new writable folder.
4. Keep every DLL and subfolder, then run ScriptPlayer.exe.
5. Select English under 设置 → 外观. Enter your own device credentials.

The candidate includes mpv v0.41.0 and FFmpeg 9.0.2; no runtime download script is needed.
Source-only downloads do not contain native runtime binaries. Do not use the legacy
v1.0.12.3 Install-Runtimes.cmd for this package. For upgrades, preserve the old app
and private profile; do not publish your settings or credentials. A new computer or
Windows user needs its own Handy key configuration.

## 简体中文

本地候选包为 `ScriptPlayer-v1.0.12.6-windows-x64-portable-candidate.zip`，
是否已发布请以 Releases 实际附件为准。

1. 系统需要 Windows 10/11 x64 与 .NET Framework 4.8。
2. 使用对应 SHA256SUMS.txt 核对压缩包。
3. 完整解压到新的可写目录。
4. 保留全部 DLL 和子目录，运行 ScriptPlayer.exe。
5. 设备密钥自行配置；更换电脑或 Windows 用户后重新填写。

候选包已附 mpv v0.41.0 与 FFmpeg 9.0.2。
不需要额外在线安装组件或安装 7-Zip。仅下载源码不含组件二进制。
旧 v1.0.12.3 在线安装脚本不适用于此包。升级使用新目录并保留旧程序和
私人配置，不要将密钥及设置上传仓库。

完整第三方对应材料审查、另一台干净电脑验证与实物设备验证仍待完成。
参见 [运行组件材料状态](RUNTIME-MATERIALS.md)。
