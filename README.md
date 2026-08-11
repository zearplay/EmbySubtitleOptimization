# Emby Subtitle Optimization

Emby Subtitle Optimization 是一个 Emby Server 插件。插件优化外置 ASS、SSA 和 SRT 字幕的行宽、字体和双语排版。

插件不会修改源字幕。计划任务会在视频目录中生成带 `.optimized.ass` 后缀的字幕副本。

## 处理规则

- 1080p、2K 和 4K 视频使用独立的最大行宽配置。
- 汉字、日文假名和韩文字符通常按 2 个显示宽度单位计算；拉丁字母通常按 1 个单位计算。
- 超过最大行宽的普通对白优先在空格或标点处换行。
- 双语字幕不使用横向拉伸或压缩，也不会生成 `\fscx` 覆盖标签。
- ASS/SSA 字幕以 Dialogue 对应 Style 的 `Fontsize` 为基准。主字幕和副字幕使用独立的字号比例，并分别生成 `\fs数字`。
- 原有内联 `\fs`、`\fs+` 和 `\fs-` 会替换为插件计算出的主字幕或副字幕字号。
- 单字幕直接按主字幕处理；主字幕和副字幕可分别设置字体颜色与字体风格。
- 主字幕、副字幕和 SRT 转 ASS 默认字体使用固定的通用字体下拉框，不读取服务器载体字体。
- 主字幕和副字幕可分别设置 ASS `Spacing`。插件使用 `\fsp` 写入字符间距。
- 双字幕以第一行为主字幕、第二行为副字幕，并支持设置两行的上下间隔。
- 字幕位置默认为“不修改原字幕位置”，保留 ASS/SSA 的 Style 对齐与边距、Dialogue 边距、`\pos` 和 `\move`。也可选择“最底部居中”，并设置距底部距离。
- 含定位、移动、裁剪、绘图或卡拉 OK 标签的 ASS/SSA 事件保持不变。
- SRT 字幕转换为 ASS 副本；转换后的默认字体可以单独选择。双语两行仍分别使用主字幕和副字幕字体。

默认行宽如下：

| 视频分辨率 | 最大行宽 | SRT 转 ASS 默认 Fontsize |
| --- | ---: | ---: |
| 1080p 及以下 | 40 | 46 |
| 2K | 46 | 60 |
| 4K | 52 | 84 |

默认样式如下：

| 字幕类型 | 字体 | 字体颜色 | 字体风格 |
| --- | --- | --- | --- |
| 主字幕（ASS/SSA 单字幕及双字幕第一行） | Source Han Sans SC | `#FFFFFF` | 粗体 |
| 双字幕副字幕（第二行） | Arial | `#FFD966` | 常规 |
| SRT 转 ASS 单字幕 | 可选的 SRT 默认字体 | `#FFFFFF` | 粗体 |

字体颜色支持 `#RRGGBB` 和 `#AARRGGBB`。字体风格支持常规、粗体、斜体和粗斜体。`Spacing` 的可选范围为 -20 到 50，默认值为 0。双字幕上下间隔默认为 0，不添加额外间隔。

主字幕 Fontsize 比例默认为 100%，副字幕 Fontsize 比例默认为 70%。例如 Style `Fontsize` 为 40 时，主字幕使用 `\fs40`，副字幕使用 `\fs28`。单字幕使用主字幕比例。

插件保留 Dialogue 文本中的 `\r` 和 `\rStyle`，以免改变原字幕依赖 Style 的位置；随后重新应用对应主字幕或副字幕的 `\fs`、字体、颜色和字体风格，避免 Style 重置覆盖插件设置。

“距最底部距离”仅在“最底部居中”模式下生效，默认值为 60。该数值以 1080p 字幕画布像素为基准，并按 ASS/SSA 的 `PlayResY` 或 SRT 转换画布高度同比例换算。

生成标记包含处理修订号。插件的字幕改写逻辑升级后，下一次计划任务会自动重新生成旧的 `.optimized.ass` 文件。

「通用设置」中的 Fontsize 默认为 17。值为 0 时，ASS/SSA 使用 Dialogue 对应 Style 的 `Fontsize`，SRT 根据视频分辨率使用默认字号。值大于 0 时，该值作为统一基础字号，再分别应用主字幕和副字幕比例。

插件从主字幕和副字幕的内联覆盖块中移除 `\2c`、`\3c`、`\4c`、`\blur`、`\be`、`\shad`、`\xbord`、`\bord`、`\xshad` 和 `\yshad`。SRT 转 ASS 不生成描边和阴影字段。

插件从 ASS/SSA Style `Format` 和 `Style` 中删除 `SecondaryColour`、`OutlineColour`、`BackColour`、`ScaleX`、`ScaleY`、`BorderStyle`、`Outline` 和 `Shadow`。SSA 中等价的 `TertiaryColour` 也会删除。内联 `\fscx` 和 `\fscy` 同样会删除。

通用字体列表包含 Arial、Helvetica、Verdana、Noto、思源、微软雅黑、黑体、宋体、苹方、DejaVu 和 Liberation 等常用字体家族，以及 `sans-serif`、`serif` 和 `monospace`。插件不检查所选字体是否已安装。播放端找不到字体时，ASS 渲染器可能使用替代字体。

## 安装

前置条件：Emby Server 版本需要兼容 `MediaBrowser.Server.Core 4.9.1.90`。首次安装前，先备份 Emby 配置目录。

1. 从 `artifacts/plugin` 目录获取构建后的 `EmbySubtitleOptimization.dll`。
2. 停止 Emby Server。
3. 将 `EmbySubtitleOptimization.dll` 复制到 Emby Server 的 `programdata/plugins` 目录。
4. 启动 Emby Server。
5. 打开「控制台 > 插件」，确认插件列表中显示「Emby Subtitle Optimization」。

如果插件未显示，请检查 Emby Server 日志中的程序集加载错误。不要删除源字幕文件。

## 使用

1. 打开「控制台 > 插件 > Emby Subtitle Optimization > 设置」。
2. 在「通用设置」中调整处理范围、最大行宽、Fontsize、SRT 转 ASS 默认字体、双字幕上下间隔、字幕位置、距最底部距离和输出后缀。
3. 在「主字幕设置」中选择字体、Fontsize 比例、字体颜色、字体风格和 `Spacing`。单字幕沿用主字幕设置。
4. 在「副字幕设置」中选择双字幕第二行的字体、Fontsize 比例、字体颜色、字体风格和 `Spacing`。
5. 保存设置。
6. 打开「控制台 > 计划任务」。
7. 运行「优化 ASS/SRT 字幕」。
8. 对媒体库执行扫描，或等待 Emby 的实时监控识别新字幕。
9. 播放视频，并选择名称中包含 `optimized` 的字幕轨道。

再次运行计划任务时，源字幕和设置均未变化的文件会被跳过。源字幕更新或插件设置变化后，插件会重新生成对应副本。

## 构建

安装 .NET 8 SDK 或更高版本，然后运行：

```bash
./scripts/build.sh
```

脚本先运行字幕排版测试，再将插件发布到 `artifacts/plugin`。插件本身以 `netstandard2.0` 为目标框架。

## 限制

- 插件只处理 Emby 媒体库中的电影和剧集，以及视频文件同目录下的外置字幕。
- 插件不提取或修改视频容器中的内封字幕。
- 插件不处理远程 URL、STRM 和不可写目录。
- 字体效果取决于播放客户端的 ASS 支持以及字体是否可用。
- 当前文本读取支持 UTF-8、UTF-8 BOM、UTF-16 LE 和 UTF-16 BE。其他本地编码应先转换为 UTF-8。

## 开发依据

项目结构基于 [Emby 官方插件 SDK](https://github.com/MediaBrowser/Emby.SDK) 的 Simple UI 模板。Emby 官方文档说明，服务端插件可通过 `IScheduledTask` 提供计划任务，并通过 Simple UI 生成配置页。
