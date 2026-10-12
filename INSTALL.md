# Installation / 安装说明

## English

These are local v1.0.12.3 packaging candidates, not a published release. Runtime distribution is pending complete corresponding source and build materials; see `THIRD_PARTY_SOURCES.md` in the app package or `docs/THIRD_PARTY_SOURCES.md` in the source package.

For a first installation, download the **app**, **mpv**, and **FFmpeg** ZIPs for the same release. Extract their contents into one folder. Do not leave the runtime files inside separate `mpv` or `ffmpeg` subfolders:

```text
ScriptPlayer/
  ScriptPlayer.exe
  ScriptPlayer.Shared.dll
  libmpv-2.dll
  ffmpeg.exe
  runtime-manifest.json
  Licenses/
  x64/
  x86/
  ...
```

Keep all files shipped in the app archive. Run `ScriptPlayer.exe` on Windows x64 with .NET Framework 4.8 available. With a new profile, choose English under **设置 → 外观** (Settings → Appearance).

Configure your own media paths and devices. Handy credentials are encrypted for the current Windows user; re-enter the key after moving to another computer or Windows account. Do not include your settings, databases, history, logs, or credentials when sharing the program.

Before an update, close the program. Prefer a new program folder and keep your personal data. Runtime files may be reused only if their SHA256 values match the new release's `runtime-manifest.json`; otherwise use that release's component archives. The app archive alone is not a complete installation.

Component ZIPs contain distinct component manifests and checksum files. Verify `SHA256SUMS-app.txt`, `SHA256SUMS-mpv.txt`, and `SHA256SUMS-ffmpeg.txt` if checking the extracted contents. The release-level `SHA256SUMS.txt` verifies the ZIP downloads themselves.

## 简体中文

这些是 v1.0.12.3 本地分包候选，并非已发布版本。运行组件仍待补齐完整对应源码与构建材料，详见程序包内 `THIRD_PARTY_SOURCES.md` 或源码包内 `docs/THIRD_PARTY_SOURCES.md`。

首次安装需下载同版本的 **app、mpv、FFmpeg 三个 ZIP**，将内容解压到同一文件夹。`ffmpeg.exe` 和 `libmpv-2.dll` 直接放在 `ScriptPlayer.exe` 旁边，不另套 `mpv` / `ffmpeg` 子目录；保留 app 中全部文件。Windows x64 需具备 .NET Framework 4.8。新配置默认简体中文，英文可在 **设置 → 外观** 中选择。

媒体路径与设备连接由用户自行配置。Handy 密钥按 Windows 当前用户加密，跨电脑或账户需重新填写。分享程序时不要附带自己的设置、数据库、历史、日志或密钥。

更新前退出程序，建议使用新程序目录并保留个人数据。只有旧运行组件 SHA256 与新版本 `runtime-manifest.json` 一致时才能复用，否则使用该版本的组件包。只下载 app 无法完成首次安装。

三个 `SHA256SUMS-组件名.txt` 分别校验解压文件，发行附件 `SHA256SUMS.txt` 校验 ZIP 下载。组件清单文件名互不冲突，可共同解压。
