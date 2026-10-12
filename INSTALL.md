# Installation / 安装说明

## English

Application version: **v1.0.12.3**, Windows x64, .NET Framework 4.8. This download contains the application and a runtime installer. FFmpeg and libmpv are downloaded directly from their original distributors; they are **not included in this ZIP**. Complete historical third-party source/build inputs are still pending; see `RUNTIME-MATERIALS.md`.

1. Verify the app ZIP against release `SHA256SUMS.txt`, then extract the complete ZIP into a **new writable folder**. Keep all application files together.
2. Install [7-Zip](https://www.7-zip.org/) if unavailable. The installer uses your existing `7z.exe` and never installs a tool automatically.
3. Close ScriptPlayer, review the local installer files, then double-click **Install-Runtimes.cmd**. An Internet connection to GitHub's original distributors and release-asset delivery service is required. The launcher sets `ExecutionPolicy Bypass` for its own PowerShell process only; it does not change persistent system/user policy or download any script. Organization-enforced restrictions still apply; follow your organization's policy if blocked.
4. Wait for `Installed` or `AlreadyInstalled`, then run **ScriptPlayer.exe**. New profiles default to Chinese; select English in **设置 → 外观** (Settings → Appearance).

The pinned downloads total about **183 MiB**. Allow around **600 MiB of free space** while installing, in addition to the application. The resulting `ffmpeg.exe` and `libmpv-2.dll` are placed beside `ScriptPlayer.exe`. Temporary downloads are removed after success or successful rollback. No media directories, user settings, history, keys, registry entries, or Windows PATH are changed.

Advanced usage from PowerShell in the extracted folder:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install-Runtimes.ps1 -SevenZip 'C:\Program Files\7-Zip\7z.exe'
# Optional: original archives already downloaded into a cache folder
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install-Runtimes.ps1 -ArchiveDirectory 'D:\RuntimeDownloads'
```

Offline/cache filenames: `mpv-dev-x86_64-20260814-git-7b8915bc1d.7z` and `ffmpeg-8.1-full_build.7z`. The exact original URLs, sizes and hashes are in `runtime-downloads.json`. Archives and extracted EXE/DLL are both verified. Existing matching components are reused; a different existing component stops installation **without overwriting it**. Use a new program folder if you want a different installation. No downloaded script is executed.

If an error occurs before installation, neither target file is published. A failure during installation rolls back newly moved files. If rollback cannot finish, the error names a retained `.runtime-install-…` folder containing `recovery.json` and staged files; keep it and report the error. The record identifies which target currently exists. Do not assume that an incomplete rollback restored everything. Correct the blocking file permission/lock and inspect the reported state before retrying.

Recheck extracted files with `SHA256SUMS-app.txt`; it describes files shipped in the app ZIP, excluding itself and the separately downloaded runtimes. Verify installed runtime files against `runtime-downloads.json`. For updates, prefer a new program folder and keep your personal application data. Re-enter Handy credentials after changing Windows account or computer; never share your private profile.

## 简体中文

版本 **v1.0.12.3**，Windows x64，需 .NET Framework 4.8。本包包含程序与组件下载脚本，**不包含 FFmpeg/libmpv 二进制**；脚本从原作者下载已测试的精确版本。完整历史源码与构建输入仍待补，见 `RUNTIME-MATERIALS.md`。

1. 校验 ZIP，完整解压到新的可写目录。
2. 安装官方 [7-Zip](https://www.7-zip.org/)（若已有则复用，不自动安装工具）。
3. 退出播放器，检查本地安装脚本后双击 **Install-Runtimes.cmd**。需能访问 GitHub 及其发行文件下载服务。启动器仅为当前 PowerShell 安装进程指定 `ExecutionPolicy Bypass`，不修改系统/用户持久策略，不下载脚本；组织强制策略仍适用，若被阻止请遵循组织策略。
4. 出现 `Installed` / `AlreadyInstalled` 后启动 **ScriptPlayer.exe**。

首次组件下载约 **183 MiB**，安装时建议另留 **600 MiB** 空间。压缩包及提取文件均校验大小与 SHA256；`ffmpeg.exe`、`libmpv-2.dll` 安装在主程序旁。匹配的旧文件直接复用，冲突文件拒绝覆盖。支持 `-SevenZip` 指定工具、`-ArchiveDirectory` 指定已下载原包目录；原包文件名和来源见英文段及 `runtime-downloads.json`。

失败时回滚本次新文件；若回滚也失败，会保留错误信息指定的临时目录及 `recovery.json`，请勿删除，按实际状态处理后再重试。脚本不修改媒体、私人配置、密钥、注册表或 PATH。更新建议使用新程序目录并保留个人数据；分享时不要附带自己的配置、历史、日志或密钥。
