# Runtime source and build-material status

Date: 2026-10-12. Application: v1.0.12.3.

## Selected distribution route

This app ZIP does not redistribute FFmpeg/libmpv binaries. `Install-Runtimes.ps1` obtains the exact tested components from their original distributors, checks archive and file SHA256/length, and installs them without replacing existing files. Keeping binary downloads upstream does **not** mean their historical corresponding-source materials are complete.

| Component | Original release | Core revision | Installed file SHA256 |
| --- | --- | --- | --- |
| FFmpeg EXE | [Gyan 8.1 full](https://github.com/GyanD/codexffmpeg/releases/tag/8.1) | `9047fa1b084f76b1b4d065af2d743df1b40dfb56` | `D1E2A156261ECC675081943197A85F08F2868784A0AF499171EDE89353EDAD31` |
| libmpv DLL | [shinchiro 20260814 x86_64 dev](https://github.com/shinchiro/mpv-winbuild-cmake/releases/tag/20260814) | `7b8915bc1d04c7e1b61184e00c7fbfaab1911e75` | `F709C7CA8B183BEC76B8158BF0C45C53018C63366750729352612F228FF7BDEA` |
| FFmpeg inside libmpv | Same mpv build | `1d7b14f61d66fdf18f15204c613df9d65396c319` | Included in the DLL above |

## Available materials

`ScriptPlayer-v1.0.12.3-runtime-core-sources-PARTIAL.zip` contains three exact core-source snapshots with their license files, checksums, source manifest, the original FFmpeg configuration/library inventory, and an index of verified build references. It is explicitly **PARTIAL**, not a complete corresponding-source archive. Independent FFmpeg EXE sources cannot substitute for the different FFmpeg revision inside libmpv.

The original mpv [build recipe](https://github.com/shinchiro/mpv-winbuild-cmake/tree/cd1edc11dc6887a50f705717619d879f5a93a488) and [workflow](https://github.com/shinchiro/mpv-winbuild-cmake/actions/runs/31755832255) are indexed by exact revision/run. The recipe snapshot has no separately verified license, so it is referenced rather than copied into this public core archive. The recipe alone does not include all library inputs.

## Still missing

- Gyan's exact direct/transitive source inputs, patches and build instructions, including the historical `openal-soft` revision recorded only as `latest`.
- libmpv's cached dependency revisions and full snapshots, including libass/libplacebo, plus the exact toolchain/cache inputs and complete configuration/link records. The detailed original artifact `9203485392` is unavailable (authenticated 404); no new log substitutes for it.
- Per-dependency license/material inventory and an actual rebuild from a complete, fixed set of inputs.

Obtaining the original builders' snapshots or making a separately validated controlled rebuild would close these gaps. No provider has been contacted. The old bundled runtime ZIPs remain local candidates and are not attachments in this new download set. Do not relabel them as ready to redistribute merely because this installer exists.

## 中文说明

本次程序下载包采用用户确认的“保留已测试组件，由脚本从原作者下载”路线，不附 FFmpeg/libmpv 二进制。三份精确核心源码、配置和依赖版本清单已整理，但完整历史依赖与构建输入仍待补，附件明确标记 **PARTIAL**。原 mpv 构建配方只提供固定来源链接，未核实其单独许可前不转载配方。没有以新版源码代替旧输入，没有更换播放核心或向维护者发送请求。
