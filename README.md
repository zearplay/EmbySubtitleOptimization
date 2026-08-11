# Emby Subtitle Optimization

## 安装文件

- 版本：`0.17.0`
- 文件：[EmbySubtitleOptimization.dll](artifacts/plugin/EmbySubtitleOptimization.dll)
- SHA-256：`45ea8946e3c0ec007aa83d453675347b4feaf8e3c8b1fb29ae411f6ff8b327b0`

## 安装方式

先在 Emby Server 控制台中打开服务器名称旁的三点菜单，然后查看服务器信息，确认实际数据目录。插件目录是数据目录下的 `plugins`。

### 不同平台的插件目录

| 平台 | 插件目录 |
| --- | --- |
| Windows | `C:\Users\{user}\AppData\Roaming\Emby-Server\programdata\plugins` |
| macOS | `/Users/{user}/emby-server/plugins` 或 `/Users/{user}/.config/emby-server/plugins` |
| Linux | `/var/lib/emby/plugins` |
| Docker | 宿主机映射的 Emby Server 数据目录下的 `plugins`；以容器映射为准 |
| Synology DSM 7 | `/volume1/@appdata/EmbyServer/plugins` |
| Synology DSM 6 | `/volume1/Emby/plugins` |
| QNAP | `/share/CACHEDEV1_DATA/.qpkg/EmbyServer/programdata/plugins`、`/share/HDA_DATA/.qpkg/EmbyServer/programdata/plugins` 或实际存储池路径 |
| Asustor | `/home/emby/plugins` |
| TerraMaster | `/home/emby/plugins` |
| Western Digital | `/mnt/HD/HD_a2/emby/plugins` |
| Thecus | `/raid/data/module/EmbyServer/programdata/plugins` |
| Android | `/storage/emulated/0/Android/data/com.emby.embyserver/files/plugins` |

实际目录可能因安装包、NAS 存储池和容器映射而不同。服务器信息中显示的路径优先。

### Windows

1. 下载 [EmbySubtitleOptimization.dll](artifacts/plugin/EmbySubtitleOptimization.dll)。
2. 停止 Emby Server 或 Emby Server Windows 服务。
3. 将 DLL 复制到 Windows 插件目录。
4. 启动 Emby Server。
5. 打开「控制台 > 插件 > 我的插件」，确认插件已经显示。

### macOS

1. 下载 [EmbySubtitleOptimization.dll](artifacts/plugin/EmbySubtitleOptimization.dll)。
2. 退出 Emby Server。
3. 将 DLL 复制到实际插件目录。
4. 启动 Emby Server，然后在插件列表中确认安装结果。

### Linux

1. 下载 [EmbySubtitleOptimization.dll](artifacts/plugin/EmbySubtitleOptimization.dll)。
2. 停止 Emby Server 服务。
3. 将 DLL 复制到 `/var/lib/emby/plugins`。
4. 将 DLL 的所有者和权限设置为与同目录其他插件 DLL 一致。
5. 启动 Emby Server 服务，然后在插件列表中确认安装结果。

### Docker

1. 下载 [EmbySubtitleOptimization.dll](artifacts/plugin/EmbySubtitleOptimization.dll)。
2. 停止 Emby Server 容器。
3. 找到宿主机映射的 Emby Server 数据目录。
4. 将 DLL 复制到该数据目录下的 `plugins`。
5. 确认容器内的 Emby Server 运行账户能够读取 DLL。
6. 启动容器，然后在插件列表中确认安装结果。

### Synology、QNAP 和其他 NAS

1. 下载 [EmbySubtitleOptimization.dll](artifacts/plugin/EmbySubtitleOptimization.dll)。
2. 在 NAS 套件管理器中停止 Emby Server。
3. 通过 SSH、SFTP、WinSCP 或 NAS 文件管理器打开实际插件目录。
4. 将 DLL 复制到插件目录。
5. 将 DLL 的所有者和权限设置为与同目录其他插件 DLL 一致。
6. 启动 Emby Server 套件，然后在插件列表中确认安装结果。

### Android

1. 下载 [EmbySubtitleOptimization.dll](artifacts/plugin/EmbySubtitleOptimization.dll)。
2. 停止 Emby Server 应用。
3. 将 DLL 复制到 Android 插件目录。
4. 启动 Emby Server，然后在插件列表中确认安装结果。

### 升级

1. 停止 Emby Server、服务、容器或 NAS 套件。
2. 备份现有 `EmbySubtitleOptimization.dll`。
3. 使用新 DLL 替换插件目录中的旧文件。
4. 检查 Linux、Docker 和 NAS 环境中的文件所有者与权限。
5. 启动 Emby Server，然后确认插件版本。

## 设置选项

单字幕直接使用主字幕设置。双字幕第一行使用主字幕设置，第二行使用副字幕设置。

### 通用设置

| 设置 | 默认值 | 有效值或选项 | 说明 |
| --- | --- | --- | --- |
| 处理 ASS/SSA 字幕 | 开启 | 开启、关闭 | 控制是否处理外置 ASS 和 SSA 字幕。 |
| 处理 SRT 字幕 | 开启 | 开启、关闭 | 控制是否将外置 SRT 转换为 ASS 副本。 |
| 1920×1080 基准每行最大字符宽度 | `40` | `20` 到 `120` | 设置 1920×1080 横屏的基准值。其他横屏按“基准值 × 实际宽度 ÷ 1920”计算；竖屏直接使用基准值。修改基准值后，所有横屏分辨率同步重新计算。 |
| Fontsize | `17` | `0`，或 `6` 到 `300` | 大于 `0` 时作为统一基础字号。设置为 `0` 时，ASS/SSA 使用 Style 的 `Fontsize`，SRT 使用分辨率档位默认字号。 |
| SRT 转 ASS 默认字体 | `Arial` | 字体下拉列表 | 用于 SRT 转换后的 ASS Style 和 SRT 单字幕。 |
| 双字幕上下间隔 | `0` | `0` 到 `80` | `0` 表示不增加间隔。该值以 1080p 为基准，更高分辨率按视频高度同比例换算。 |
| 字幕位置 | 不修改原字幕位置 | 不修改原字幕位置、最底部居中 | 默认保留原 Style 对齐、边距和内联定位。最底部居中会替换原定位。 |
| 距最底部距离 | `60` | `0` 到 `540` | 仅在「最底部居中」模式下生效。以 1920×1080 的画面高度为基准，所有分辨率均按“设置值 × 实际高度 ÷ 1080”换算。 |
| 输出文件后缀 | `optimized` | 英文字母、数字、连字符、下划线 | 用于生成文件名。例如 `Movie.zh.ass` 生成 `Movie.zh.optimized.ass`。 |

### 主字幕设置

| 设置 | 默认值 | 有效值或选项 | 说明 |
| --- | --- | --- | --- |
| 字体（含单字幕） | `Source Han Sans SC` | 字体下拉列表 | 用于 ASS/SSA 单字幕，以及 ASS/SSA 和 SRT 双字幕第一行。SRT 单字幕使用「SRT 转 ASS 默认字体」。 |
| Fontsize 比例（含单字幕） | `100` | `50` 到 `200` | 按基础字号的百分比计算。`100` 表示保持基础字号。 |
| 字体颜色（含单字幕） | `#FFFFFF` | `#RRGGBB`、`#AARRGGBB` | 用于单字幕和双字幕第一行。八位格式中的前两位表示 Alpha。 |
| 字体风格（含单字幕） | 粗体 | 常规、粗体、斜体、粗斜体 | 用于单字幕和双字幕第一行。 |
| Spacing（含单字幕） | `0` | `-20` 到 `50` | ASS 字符间距。负数收紧字符，正数放宽字符。 |

### 副字幕设置

| 设置 | 默认值 | 有效值或选项 | 说明 |
| --- | --- | --- | --- |
| 字体 | `Arial` | 字体下拉列表 | 用于双字幕第二行。 |
| Fontsize 比例 | `70` | `50` 到 `200` | 按基础字号的百分比计算。默认值表示副字幕字号为基础字号的 70%。 |
| 字体颜色 | `#FFD966` | `#RRGGBB`、`#AARRGGBB` | 用于双字幕第二行。八位格式中的前两位表示 Alpha。 |
| 字体风格 | 常规 | 常规、粗体、斜体、粗斜体 | 用于双字幕第二行。 |
| Spacing | `0` | `-20` 到 `50` | 双字幕第二行的 ASS 字符间距。 |

### 字体下拉列表

`Arial`、`Arial Unicode MS`、`Helvetica`、`Verdana`、`Tahoma`、`Trebuchet MS`、`Georgia`、`Times New Roman`、`Courier New`、`Roboto`、`Roboto Condensed`、`Open Sans`、`Lato`、`Ubuntu`、`Inter`、`Montserrat`、`Fira Sans`、`Droid Sans`、`Noto Sans`、`Noto Serif`、`Noto Sans CJK SC`、`Noto Serif CJK SC`、`Source Han Sans SC`、`Source Han Serif SC`、`Microsoft YaHei`、`SimHei`、`SimSun`、`PingFang SC`、`Heiti SC`、`WenQuanYi Micro Hei`、`WenQuanYi Zen Hei`、`Sarasa Gothic SC`、`DejaVu Sans`、`DejaVu Serif`、`Liberation Sans`、`Liberation Serif`、`sans-serif`、`serif`、`monospace`。

字体列表是固定选项，不扫描服务器中的已安装字体。播放端没有所选字体时，字幕渲染器可能使用替代字体。
