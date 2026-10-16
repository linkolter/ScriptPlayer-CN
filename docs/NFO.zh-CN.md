# ScriptPlayer v1.0.12.3：NFO 格式与编写说明

**NFO 是可选的媒体说明文件，不是播放视频或同步脚本的前置要求。** 没有 NFO 时，媒体标题使用文件名；视频与 funscript 的关联仍按文件匹配规则处理。NFO 用于补充显示标题、标签、收藏、简介和脚本统计。

本说明按 v1.0.12.3 的 `NfoMetadata` 和媒体列表实际读取逻辑整理。它是播放器支持的 XML 子集，不代表完整支持所有 Kodi/Jellyfin 的 NFO 字段。

## 最方便的创建方法

1. 将视频加入媒体列表。
2. 在条目右键菜单中选择 **编辑本地 NFO / EroScripts 标签**。
3. 填写标题、标签、简介等信息，保存。

新文件由程序写成 XML。编辑器的日常元数据操作不要求填写脚本统计，也不会生成 funscript。旧式纯文本 NFO 只作兼容读取，不能直接通过编辑器隐式转换或覆盖；需要先备份，再另行转换为 XML。

## 文件放在哪里？

放在视频所在目录，与视频使用相同的主文件名，扩展名改成 `.nfo`：

```text
Demo.mp4
Demo.nfo
Demo.funscript
Demo.twist.funscript
Demo.surge.funscript
```

多作者示例：

```text
Demo.mp4
Demo.nfo
Demo-AuthorA.funscript
Demo-AuthorA.twist.funscript
Demo-AuthorB.funscript
Demo-AuthorB.surge.funscript
```

媒体列表优先用视频关联的 NFO，因此通常一个 `Demo.nfo` 即可。直接按脚本路径查找 NFO 时，解析器先找同名文件，再逐段去掉末尾 `-` 后缀查找共享文件；不要把这种回退当成所有命名格式都能识别。最稳妥的做法仍是与视频同名。

**NFO 中的标题不用与文件名完全一致。** 标题只影响显示，不会自动重命名视频或脚本。

## 最小模板

所有元数据字段都是可选的，下面已足够用于标题和标签展示：

```xml
<?xml version="1.0" encoding="UTF-8"?>
<movie>
  <title>Demo Video</title>
  <tag>Demo</tag>
  <tag>Multi-axis</tag>
  <favorite>false</favorite>
</movie>
```

下载 `examples/Demo-minimal.nfo`，改内容后另存为自己视频对应的文件名即可。不要覆盖已有 NFO；先保留一份原文件。

## 支持的基础字段

以下字段放在 `<movie>` 的直接子级。标签名区分大小写，推荐使用表中的小写名称。

| 字段 | 用途 | 编写方式 |
| --- | --- | --- |
| `title` | 媒体显示标题 | 普通文本；缺失或为空时显示文件名 |
| `originaltitle` | 原始标题 | 可选文本；不负责文件匹配 |
| `type` | 自定义类型说明 | 例如 `Video`、`Multi-axis`；不是设备轴映射设置 |
| `source` | 来源链接 | 可选普通 URL 文本；不要填密钥或含凭据的链接 |
| `plot` | 简介 | 可选文本；多行内容可使用 CDATA |
| `favorite` | 是否收藏 | 推荐 `true` 或 `false`；省略默认为未收藏 |
| `tag` | 标签 | 每个标签单独写一个 `tag`，可以重复添加节点 |
| `scripts` | 脚本统计容器 | 可省略；结构见下文 |

兼容别名：`name` 对应 `title`；`original-title` 对应 `originaltitle`；`url`、`website` 对应 `source`；`mediatype` 对应 `type`；`description` 对应 `plot`；`favourite` 对应 `favorite`；`genre` 也可作为标签。建议使用标准名称，不重复填写互相矛盾的别名。

完整基础示例：

```xml
<?xml version="1.0" encoding="UTF-8"?>
<movie>
  <title>Demo Video</title>
  <originaltitle>Original Demo Title</originaltitle>
  <type>Multi-axis</type>
  <source>https://example.com/demo</source>
  <plot><![CDATA[A local demonstration video with two script variants.]]></plot>
  <favorite>false</favorite>
  <tag>Demo</tag>
  <tag>Multi-axis</tag>
  <tag>AuthorA</tag>
</movie>
```

## 可选：脚本统计

统计放在 `scripts` 内，每份脚本一个 `script`。`file` 填脚本文件名，建议包含 `.funscript` 扩展名，不写本机绝对路径。

```xml
<scripts>
  <script file="Demo-AuthorA.funscript">
    <duration>00:01:00</duration>
    <durationms>60000</durationms>
    <actions>121</actions>
  </script>
  <script file="Demo-AuthorB.funscript">
    <duration>00:00:45</duration>
    <durationms>45000</durationms>
    <actions>91</actions>
  </script>
</scripts>
```

把这段放在 `</movie>` 前，不要在根节点外再添加一个 `scripts`。这里数字仅为格式演示，并非你的视频统计。

| 字段 | 含义 | 当前读取行为 |
| --- | --- | --- |
| `script` 的 `file` 属性 | 统计对应的脚本文件名 | 必须有文件名，否则该统计条目被忽略 |
| `duration` | 脚本时长显示文本 | 直接显示文本，建议 `HH:MM:SS`；也能读取已有 `MM:SS` |
| `durationms` | 脚本时长，毫秒 | 读取整数；当前列表时长文本来自 `duration`，不会由它自动格式化 |
| `actions` | 动作数量 | 整数，可省略 |
| `maxspeed` | 最大速度统计 | 可选整数，读取后展示；不是设备速度指令 |
| `avgspeed` | 平均速度统计 | 可选整数，读取后展示；不是设备速度指令 |

`file` 也兼容 `script` 下的 `file` 或 `filename` 子节点；`actions` 兼容 `actioncount`；`avgspeed` 兼容 `averagespeed`。建议统一用上面的标准写法。

**统计可以不填，也不会自动计算。** 不知道真实数值时省略，避免把猜测当成媒体数据。速度统计的单位取决于生成它的工具，当前 NFO 读取器没有统一换算或单位标识；不要将不同工具的数字直接作为设备控制参数比较。

`scripts` 不是播放清单，不负责选择作者、下载脚本或加载设备轴。实际脚本选择和多轴加载仍取决于磁盘文件及播放器匹配规则。为正确展示各作者统计，`file` 应与实际选中的脚本名一致。

## 编写注意事项

- 保存为 UTF-8，确认文件真的是 `Demo.nfo`，不是 `Demo.nfo.txt`。Windows 文件管理器可开启显示文件扩展名。
- 使用单个 `movie` 根节点，每个开始标签都要有结束标签。推荐不添加命名空间和 DTD。
- 普通文本中的 `&` 写成 `&amp;`，`<` 写成 `&lt;`。例如标题 `A & B` 应写成 `<title>A &amp; B</title>`。
- 链接中的参数分隔符同样要转义：`https://example.com/?a=1&amp;b=2`。不想手工转义简介时可以使用 CDATA，但其中不能包含 `]]>`。
- 标签推荐一个节点一个值，不依赖逗号拼接。原始标签可能被界面翻译显示，切换语言不等于修改 NFO 文件。
- 未识别的 XML 扩展字段在正常的元数据编辑中会保留，但不会自动变成播放器功能。例如 `thumb`、`poster` 当前不用于从 NFO 自动加载封面；封面沿用播放器已有预览与生成机制。
- 在外部编辑后，可通过媒体列表“更多”中的 **重新检查所有条目** 重新读取。当前不承诺实时监听所有外部 NFO 变化。
- 视频或脚本改名后，检查对应 NFO 名称及 `script file`。当前关联改名不会自动迁移 NFO 内部统计引用。

## 与已有 NFO 的兼容性

采用 `movie` 根节点，包含 `title`、`originaltitle`、`type`、`source`、多个 `tag`，以及 `scripts/script file` 和上述统计字段的 XML NFO，属于当前支持的结构。可先保留原文件，再对照字段说明检查，不必为了使用播放器重写已有信息。

随附模板使用独立的中性示例，不含个人媒体内容、私人来源链接、配置或连接密钥。
