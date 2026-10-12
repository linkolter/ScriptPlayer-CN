# Current download route — 2026-10-12

The selected app archive excludes FFmpeg/libmpv binaries and includes tools/runtime-installer files copied beside ScriptPlayer.exe. It downloads the tested versions from the original distributors. See ../INSTALL.md and ../RUNTIME-MATERIALS.md. Original binary redistribution is still pending complete corresponding materials; this route does not relabel partial inputs complete.

The notes below describe the earlier local bundled candidates and their build/privacy constraints. They are historical evidence, not instructions to publish those runtime assets.

# 公开发布与私人配置

源码仓库保存源码、项目文件和许可证；FFmpeg/libmpv大二进制放GitHub Releases便携包，不嵌入主EXE。不上传整个开发工作区、测试profile、历史快照、日志、真实媒体或调试符号。gitignore不能替代最终包审计，尤其已跟踪文件/历史。

## 设置与密钥

内置FFmpeg显示“随程序提供（ffmpeg.exe）”；运行时从程序目录定位，默认设置不保存安装地址。显式外部路径只在用户自行选用时保留。

Handy连接密钥只在内存中供连接使用；普通XML导出不包含该字段。正常配置目录是当前用户APPDATA下的ScriptPlayer；Private/HandyConnection.dpapi采用Windows DPAPI CurrentUser。用户需要自行配置密钥，不随软件分发；不要分享整个个人配置目录，加密文件也不应上传。

旧XML密钥加载后，下一次成功保存先提交加密凭据，再原子替换不含密钥的普通XML。私密写入失败保留旧XML；损坏密文未编辑时阻止覆盖，重新输入密钥后才允许替换。普通XML损坏时仍读独立密钥，并阻止默认设置自动覆盖原文件；应修复配置后重启。取消/复制编辑保持这些保护。DPAPI不能直接跨Windows用户/电脑迁移；跨机需重新输入。

这两份文件不是跨文件原子事务：private成功而XML失败时，private可能已更新，XML仍旧版，并报告未完成。旧XML仍含legacy字段时下次启动优先保留其旧值。未清理用户历史备份；本地旧快照可能含旧明文，不能公开。其他设备密码仍属私人配置，发布包一律不包含profile，不声称所有密码存储已统一加密。

## 公开构建与检查

使用PublicRelease强制Rebuild并禁用DebugType/DebugSymbols，防止PE调试记录包含个人源目录；仅不复制PDB不足够。发布源码通过白名单导出，内部文档按需移除个人路径，不修改生产代码。对最终目录及ZIP解压内容检查个人路径与已知密钥，仅输出布尔/计数/命中文件，不显示密钥。

发布前核实EXE版本和源码身份、完整Stage4、凭据正常及失败路径，保存SHA256清单。当前本机构建依赖已有MK312产物；源码包不宣称新机器已干净构建。

## 许可证与公开前事项

本项目沿用GPL-3.0和原ScriptPlayer来源声明。随程序附带第三方许可证，并为发布版本提供对应源代码。当前FFmpeg二进制自报8.1-full_build-www.gyan.dev，启用gpl/version3/static；不自动替换新版本，不把当前提供者最新版当成这份二进制。mpv来源记录见随附Licenses。

当前本地下载候选尚未具备FFmpeg/libmpv及所有静态依赖的完整对应源码/构建材料附件。必须补齐精确版本与材料并建立实际下载入口，再作为公开发行上传；来源主页或GPL正文不等于已完成此项。

官方参考：

- GitHub大文件与Releases：https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github
- FFmpeg许可与分发：https://ffmpeg.org/legal.html
- FFmpeg Windows构建提供者：https://www.gyan.dev/ffmpeg/builds/
- mpv来源许可：https://github.com/mpv-player/mpv/blob/master/Copyright

此轮只准备本地候选，未连接或修改你的GitHub仓库，未上传文件或发论坛帖子。
