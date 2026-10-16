# Issue tracker: Local Markdown

所有路径相对于 当前仓库根目录。

- 每项功能使用 `.scratch/<feature-slug>/`。
- 规格位于 `spec.md`。
- 每张任务票单独放在 `issues/<NN>-<slug>.md`，从 01 编号。
- 任务状态使用文件顶部的 `Status:` 行。
- 讨论追加到文件末尾的 `## Comments`。
- “发布到任务跟踪器”表示创建本地文件，不创建远程 Issue。
- 获取任务时读取用户指定的文件。

## Wayfinding operations

- 地图：`.scratch/<effort>/map.md`。
- 子票：`issues/NN-<slug>.md`。
- Type 可使用 research/prototype/grilling/task。
- 用 `Blocked by:` 记录前置票编号。
- 按编号选择未完成、未领取且前置票均 resolved 的任务。
- 开始前写入 `Status: claimed`。
- 完成后追加 `## Answer`，设为 resolved，并更新地图的 Decisions-so-far。
