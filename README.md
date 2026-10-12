# ScriptPlayer

**English** | [简体中文](README.zh-CN.md)

*Simple playback. More freedom.*

A Windows video and Funscript player with a redesigned interface, a local media library, and a timeline built around script playback.

Current local candidate: **v1.0.12.3**. Downloads will be available on [Releases](https://github.com/linkolter/ScriptPlayer-CN/releases) after release preparation is complete. The new split packages are local candidates, not published downloads.

## Playback that feels natural

- Video playback powered by libmpv; FFmpeg handles thumbnails and other media processing.
- Large covers with a single-line title make the sidebar easy to browse.
- The left playlist, bottom controls, and right adjustment panel appear independently. Dragging or opening a menu keeps the relevant controls visible.
- An integrated heatmap and seek timeline shows bookmarks, AB loop points, and paused-exit markers. Hover previews help you find a scene.
- The heatmap can stay visible without revealing the entire bottom control panel on hover.

## Media, scripts, and devices

- A local media library with covers, NFO metadata, tags, and favorites.
- Multiple authors' script variants and the multi-axis companion files associated with the selected script.
- Funscript synchronization, multi-axis scripts, TCode output, and Handy connectivity.
- Playback history and paused-exit markers are separate. A paused-exit marker is saved only when you explicitly pause and then exit normally; switching videos does not create one.

## Make it yours

- A WPF interface with consistent fonts, buttons, spacing, and switches.
- Simplified Chinese and English, dark and light themes, and five accent colors.
- Optional interface animations and layouts that prioritize essential controls in narrow windows.
- The bundled FFmpeg is located relative to the application, without a developer-specific installation path.
- Each user configures their own Handy key. It is encrypted for the current Windows user; release packages do not include the developer's private configuration or credentials.

The current application starts in Simplified Chinese with a new profile. Choose English in **设置 → 外观** (Settings → Appearance). The README language does not change the application language automatically.

## Download and install

The proposed release provides three separate Windows x64 downloads:

1. `ScriptPlayer-v1.0.12.3-windows-x64-app.zip`
2. `ScriptPlayer-v1.0.12.3-windows-x64-mpv.zip`
3. `ScriptPlayer-v1.0.12.3-windows-x64-ffmpeg.zip`

Extract **all three into the same folder**, keeping their contents and subfolders. `ScriptPlayer.exe`, `libmpv-2.dll`, and `ffmpeg.exe` must be beside one another. Then run `ScriptPlayer.exe` and add your media and scripts. See [installation details / 安装说明](INSTALL.md).

For an update, reuse runtime files only when their hashes match that release's `runtime-manifest.json`. Re-enter your Handy key when moving to another Windows user or computer.

**Runtime distribution preparation is still open.** Exact core source revisions have been identified, but the complete corresponding dependency sources and build materials for the current mpv/FFmpeg builds are not yet assembled. Separating binaries into release assets does not remove these requirements. See [third-party source status](docs/THIRD_PARTY_SOURCES.md). The current runtime archives are local candidates only.

## Source and building

WPF + .NET Framework 4.8 + libmpv. Main project: `ScriptPlayer/ScriptPlayer/ScriptPlayer.csproj`; solution: `ScriptPlayer/ScriptPlayer.sln`.

The source candidate includes `tools/Build-Public.ps1` and `tools/runtime-manifest.json`. Restore the required packages and place the matching runtime files in `ScriptPlayer/ScriptPlayer/Runtime` before building. The validated local build used existing MK312 output; a clean build of the whole solution on a fresh machine has **not** been verified. Do not treat the packaged script as a one-command clean-machine build guarantee.

## Feedback

Please include the application version, Windows version, reproduction steps, and device model when reporting a problem. Do not post connection keys or private configuration. Device support does not mean every hardware, firmware, and graphics combination has been tested.

## About

A community project gradually developed out of personal interest in a smoother local playback experience.

Based on [FredTungsten/ScriptPlayer](https://github.com/FredTungsten/ScriptPlayer), retaining its script synchronization and device control foundations. This is an unofficial derivative under [GPL-3.0](LICENSE). See [origin and modification notices](NOTICE-CN.md). Thanks to the original developers and everyone contributing to open source.

*Enjoy your freedom.*
