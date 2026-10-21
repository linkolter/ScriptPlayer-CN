# ScriptPlayer

**English** | [简体中文](README.zh-CN.md)

*Simple playback. More freedom.*

A Windows video and Funscript player with a redesigned interface, a local media library, and a timeline built around script playback.

Current source version: **v1.0.12.6**. The updated portable package is a local candidate; publishing it is a separate step. Check the [Releases page](https://github.com/linkolter/ScriptPlayer-CN/releases) for files actually available to download.

Device connection now comes first on Home. Handy, Wi-Fi, USB and Bluetooth SPP/COM share settings subpages. Bluetooth requires a Windows-paired outgoing COM port; BLE/GATT is not added. See [changes and validation](CHANGELOG.md).

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
- Off, Light, Standard and Custom animation modes, per-effect switches, Windows reduced-motion support and automatic simplification for limited graphics capability.
- An independent animated-preview option and narrow layouts that prioritize essential playback controls.
- The installed FFmpeg is located relative to the application, without a developer-specific installation path.
- Each user configures their own Handy key. It is encrypted for the current Windows user; release packages do not include the developer's private configuration or credentials.

The current application starts in Simplified Chinese with a new profile. Choose English in **设置 → 外观** (Settings → Appearance). The README language does not change the application language automatically.

## Download and install

Latest local candidate: `ScriptPlayer-v1.0.12.6-windows-x64-portable-candidate.zip`.
Requires Windows 10/11 x64 and .NET Framework 4.8. Extract the complete ZIP into a new writable folder and run `ScriptPlayer.exe`. Keep all DLLs and subfolders; no online runtime installer or 7-Zip installation step is needed. Upgrade into a new folder to preserve the previous app and configuration. See [installation details](INSTALL.md).

The local portable candidate includes MSYS2 UCRT64 mpv 0.41.0-8 (mpv v0.41.0) and Gyan FFmpeg 9.0.2 full. Component identities and checksums are recorded in `tools/runtime-manifest.json`. Runtime binaries are not tracked in this source repository; downloading source alone does not provide them.

Complete third-party corresponding-material review remains pending. A different clean computer and physical devices have not been validated. See [runtime-material status](RUNTIME-MATERIALS.md). The legacy v1.0.12.3 online installer is not used for this candidate.

## Source and building

WPF + .NET Framework 4.8 + libmpv. Main project: `ScriptPlayer/ScriptPlayer/ScriptPlayer.csproj`; solution: `ScriptPlayer/ScriptPlayer.sln`.

This repository includes `tools/Build-Public.ps1` and `tools/runtime-manifest.json`. Restore the required packages and place the matching runtime files in `ScriptPlayer/ScriptPlayer/Runtime` before building. The validated local build used existing MK312 output; a clean build of the whole solution on a fresh machine has **not** been verified. Do not treat the packaged script as a one-command clean-machine build guarantee.

The root `SHA256SUMS.txt` records the prepared ZIP checksums (including retained v1.0.12.4 candidates), not checkout-file hashes. The PARTIAL source archive is not a complete corresponding-source bundle. Local development uses this single checkout; see [workspace routing](docs/WORKSPACE.md). NFO examples are documented in [English](docs/NFO.en.md) and [Chinese](docs/NFO.zh-CN.md).

## Feedback

Please include the application version, Windows version, reproduction steps, and device model when reporting a problem. Do not post connection keys or private configuration. Device support does not mean every hardware, firmware, and graphics combination has been tested.

## About

A community project gradually developed out of personal interest in a smoother local playback experience.

Based on [FredTungsten/ScriptPlayer](https://github.com/FredTungsten/ScriptPlayer), retaining its script synchronization and device control foundations. This is an unofficial derivative under [GPL-3.0](LICENSE). See [origin and modification notices](NOTICE-CN.md). Thanks to the original developers and everyone contributing to open source.

*Enjoy your freedom.*
