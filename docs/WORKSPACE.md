# 固定开发目录

2026-10-16 起，GitHub Desktop 已添加的 `github-upload/ScriptPlayer-CN-20261012`
是唯一开发仓库。目录名中的日期保留不变；程序版本由源码版本信息管理。
所有源码、测试、文档、构建脚本直接在此修改，不再逐版另建开发副本。
`ScriptPlayer-merged` 保留为历史参考和依赖缓存，不作为编译源码。

## 本机兼容入口

在工作区根目录运行以下入口，它们仅转发到本仓库 `tools/workspace` 中的实现：

```powershell
$taskPublish = 'playback-baseline/builds/' + (Get-Date -Format 'yyyyMMdd-HHmmss')
.\build-media-player.ps1 -PublishName $taskPublish -PublicRelease
.\run-playback-baseline.ps1 -PublishName $taskPublish -Stage4
```

`tests/Find-LocalWorkspace.ps1` 定位上层工作区中的本地 net48 引用与测试夹具。
这些入口复用现有 MK312 产物和忽略的包缓存，不表示新电脑已验证从零构建。
独立构建参考 `tools/Build-Public.ps1` 和 TESTING.md。

## 源码与成品

- GitHub Desktop 观察本仓库；本轮同步保留为待提交变更，未自动提交或推送。
- Runtime 的组件身份 JSON 进入源码；DLL/EXE、包缓存、bin/obj、配置和密钥被忽略。
- Release ZIP 保存在工作区 public-release；GitHub 源码同步不会自动上传 Release 附件。
- 修改前的 629 个已跟踪文件快照保存在工作区
  `.scratch/repo-consolidation-20261016/before-repo`；工作区入口原件在相邻
  `before-workspace-routing`。现有 Git 历史保持不变。
