# Runtime materials / 运行组件材料

Current v1.0.12.4 local portable candidate uses MSYS2 UCRT64 mpv 0.41.0-8
(mpv v0.41.0, client API 2.5, internal FFmpeg 9.0.2) and standalone Gyan
FFmpeg 9.0.2 full. Exact package origins, file sizes, hashes and source/build
references are recorded in `tools/runtime-manifest.json`, mirrored in the
application Runtime/runtime-versions.json. Native binaries are ignored by Git.

Licenses and recorded runtime notices are under ScriptPlayer/ScriptPlayer/Licenses/Runtime.
The prepared PARTIAL archive contains recorded core sources and references; it
does not establish complete corresponding sources for every dependency and build.
Complete corresponding-material review remains pending. Local portable packaging
does not by itself establish readiness for public redistribution.

本地候选已完成当前机器的编译、完整 Stage4 及组件专项测试；未验证另一台
干净 Windows 电脑、全部显卡组合或实物设备。旧 v1.0.12.3 下载脚本和旧组件
材料属于历史记录，不可用于证明新组件身份或新包的材料完整性。

See docs/THIRD_PARTY_SOURCES.md, docs/RELEASING.md and docs/TESTING.md.
