using System.ComponentModel;
using System.Collections.Generic;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Validation;
using Emby.Web.GenericEdit.Common;
using Emby.Web.GenericEdit.Elements;
using MediaBrowser.Model.Attributes;

namespace EmbySubtitleOptimization
{
    /// <summary>Plugin configuration displayed by Emby's simple UI.</summary>
    public sealed class PluginOptions : EditableOptionsBase
    {
        public PluginOptions()
        {
            EnableAss = true;
            EnableSrt = true;
            BalanceBilingualLines = true;
            MaxLineWidth1080P = 80;
            MaxLineWidth2K = 46;
            MaxLineWidth4K = 52;
            CommonFontSize = 17;
            MaxBilingualWidthRatio = 1.45;
            MinHorizontalScalePercent = 70;
            MaxHorizontalScalePercent = 125;
            SingleFontName = "Arial";
            PrimaryFontName = "Source Han Sans SC";
            SecondaryFontName = "Arial";
            SrtDefaultFontName = "Arial";
            PrimaryFontSizePercent = 100;
            SecondaryFontSizePercent = 70;
            SingleSubtitleColor = "#FFFFFF";
            PrimarySubtitleColor = "#FFFFFF";
            SecondarySubtitleColor = "#FFD966";
            PrimaryBorderEnabled = true;
            SecondaryBorderEnabled = true;
            PrimaryBorderWidth = 0.1;
            SecondaryBorderWidth = 0.1;
            PrimaryBorderColor = "#000000";
            SecondaryBorderColor = "#000000";
            SingleFontStyle = SubtitleFontStyle.Regular;
            PrimaryFontStyle = SubtitleFontStyle.Bold;
            SecondaryFontStyle = SubtitleFontStyle.Regular;
            PrimaryCharacterSpacing = 0;
            SecondaryCharacterSpacing = 0;
            BilingualLineSpacing = 0;
            PositionMode = SubtitlePositionMode.PreserveOriginal;
            BottomDistance1080P = 20;
            EnableWebFullscreenCanvasFix = true;
            OutputSuffix = "optimized";
        }

        public override string EditorTitle => "字幕优化设置";

        public override string EditorDescription =>
            "插件不会修改源字幕。运行「优化 ASS/SRT 字幕」计划任务后，插件在视频目录中生成 .optimized.ass 副本。";

        public CaptionItem CommonSettingsCaption { get; set; } = new CaptionItem("通用设置");

        [DisplayName("处理 ASS/SSA 字幕")]
        [Description("为普通对白插入自适应换行；定位、绘图和卡拉 OK 特效行保持不变。")]
        public bool EnableAss { get; set; }

        [DisplayName("处理 SRT 字幕")]
        [Description("将 SRT 转换为带自适应样式的 ASS 副本。")]
        public bool EnableSrt { get; set; }

        [DisplayName("1920×1080 基准每行最大字符宽度")]
        [Description("默认值为 80。修改此值后，所有横屏分辨率都会按照实际横向分辨率相对于 1920 像素的比例重新计算；竖屏直接使用基准值。")]
        public int MaxLineWidth1080P { get; set; }

        [Browsable(false)]
        public int MaxLineWidth2K { get; set; }

        [Browsable(false)]
        public int MaxLineWidth4K { get; set; }

        [DisplayName("Fontsize")]
        [Description("统一基础字号，默认值为 17。设置为 0 时，ASS/SSA 使用 Style Fontsize，SRT 按视频分辨率自动选择。")]
        public double CommonFontSize { get; set; }

        [DisplayName("SRT 转 ASS 默认字体")]
        [Description("用于 SRT 转换后 ASS 文件的默认样式和单字幕。字体选项来自固定的通用字体列表。")]
        [SelectItemsSource(nameof(AvailableFonts))]
        public string SrtDefaultFontName { get; set; }

        [DisplayName("双字幕上下间隔")]
        [Description("普通双字幕会按照主、副字幕的实际 Fontsize 紧凑排列。此值只增加额外间隔，以 1080p 字幕画布像素为基准；默认值为 0。")]
        public int BilingualLineSpacing { get; set; }

        [DisplayName("字幕位置")]
        [Description("默认不修改 ASS/SSA 的原始 Style、定位标签、对齐方式和边距；也可将普通对白统一设置为最底部居中。特效字幕始终保留原位置。")]
        public SubtitlePositionMode PositionMode { get; set; }

        [DisplayName("距最底部距离")]
        [Description("仅在“最底部居中”时生效。以 1920×1080 的画面高度为基准，所有分辨率均按“设置值 × 实际高度 ÷ 1080”换算。默认值为 20。")]
        public int BottomDistance1080P { get; set; }

        [DisplayName("修复网页端全屏字幕位置")]
        [Description("默认启用。修复 Emby Web 进入全屏后 ASS 字幕画布仍使用窗口模式尺寸，导致字幕整体向上、向左移动的问题。关闭后会移除插件添加的 CSS，不影响其他自定义 CSS。")]
        public bool EnableWebFullscreenCanvasFix { get; set; }

        [DisplayName("输出文件后缀")]
        [Description("生成文件名中的标记。只能包含英文字母、数字、连字符或下划线。")]
        [Required]
        public string OutputSuffix { get; set; }

        public SpacerItem PrimarySettingsSpacer { get; set; } = new SpacerItem();

        public CaptionItem PrimarySettingsCaption { get; set; } = new CaptionItem("主字幕设置");

        [DisplayName("字体（含单字幕）")]
        [Description("用于 ASS/SSA 单字幕，以及 ASS/SSA 和 SRT 双字幕第一行。")]
        [SelectItemsSource(nameof(AvailableFonts))]
        public string PrimaryFontName { get; set; }

        [DisplayName("Fontsize 比例（含单字幕）")]
        [Description("以 Dialogue 引用 Style 的 Fontsize 为基准。100 表示保持 Style 字号。")]
        public int PrimaryFontSizePercent { get; set; }

        [DisplayName("字体颜色（含单字幕）")]
        [Description("用于单字幕和双字幕第一行。使用 #RRGGBB 或 #AARRGGBB 格式。")]
        public string PrimarySubtitleColor { get; set; }

        [DisplayName("字体风格（含单字幕）")]
        [Description("用于单字幕和双字幕第一行。")]
        public SubtitleFontStyle PrimaryFontStyle { get; set; }

        [DisplayName("字体边框（含单字幕）")]
        [Description("控制单字幕和双字幕第一行是否使用外边框。边框类型固定为外边框。")]
        public bool PrimaryBorderEnabled { get; set; }

        [DisplayName("边框宽度（含单字幕）")]
        [Description("单字幕和双字幕第一行的外边框宽度。默认值为 0.1。")]
        public double PrimaryBorderWidth { get; set; }

        [DisplayName("边框颜色（含单字幕）")]
        [Description("单字幕和双字幕第一行的外边框颜色。使用 #RRGGBB 或 #AARRGGBB 格式，默认值为黑色。")]
        public string PrimaryBorderColor { get; set; }

        [DisplayName("Spacing（含单字幕）")]
        [Description("ASS 字符间距。默认值为 0；负数收紧字符，正数放宽字符。")]
        public double PrimaryCharacterSpacing { get; set; }

        public SpacerItem SecondarySettingsSpacer { get; set; } = new SpacerItem();

        public CaptionItem SecondarySettingsCaption { get; set; } = new CaptionItem("副字幕设置");

        [DisplayName("字体")]
        [Description("用于 ASS/SSA 和 SRT 双字幕的第二行。")]
        [SelectItemsSource(nameof(AvailableFonts))]
        public string SecondaryFontName { get; set; }

        [DisplayName("Fontsize 比例")]
        [Description("以 Dialogue 引用 Style 的 Fontsize 为基准。默认值为 70。")]
        public int SecondaryFontSizePercent { get; set; }

        [DisplayName("字体颜色")]
        [Description("双字幕第二行使用的颜色。使用 #RRGGBB 或 #AARRGGBB 格式。")]
        public string SecondarySubtitleColor { get; set; }

        [DisplayName("字体风格")]
        [Description("用于双字幕的第二行。")]
        public SubtitleFontStyle SecondaryFontStyle { get; set; }

        [DisplayName("字体边框")]
        [Description("控制双字幕第二行是否使用外边框。边框类型固定为外边框。")]
        public bool SecondaryBorderEnabled { get; set; }

        [DisplayName("边框宽度")]
        [Description("双字幕第二行的外边框宽度。默认值为 0.1。")]
        public double SecondaryBorderWidth { get; set; }

        [DisplayName("边框颜色")]
        [Description("双字幕第二行的外边框颜色。使用 #RRGGBB 或 #AARRGGBB 格式，默认值为黑色。")]
        public string SecondaryBorderColor { get; set; }

        [DisplayName("Spacing")]
        [Description("双字幕第二行的 ASS 字符间距。默认值为 0。")]
        public double SecondaryCharacterSpacing { get; set; }

        [Browsable(false)]
        public IEnumerable<EditorSelectOption> AvailableFonts { get; set; }

        [Browsable(false)]
        public bool BalanceBilingualLines { get; set; }

        [Browsable(false)]
        public double MaxBilingualWidthRatio { get; set; }

        [Browsable(false)]
        public int MinHorizontalScalePercent { get; set; }

        [Browsable(false)]
        public int MaxHorizontalScalePercent { get; set; }

        [Browsable(false)]
        public int CjkFontSizePercent { get; set; }

        [Browsable(false)]
        public int LatinFontSizePercent { get; set; }

        [Browsable(false)]
        public string SingleSubtitleColor { get; set; }

        [Browsable(false)]
        public SubtitleFontStyle SingleFontStyle { get; set; }

        [Browsable(false)]
        public string SingleFontName { get; set; }

        [Browsable(false)]
        public string FontName { get; set; }

        [Browsable(false)]
        public int SettingsSchemaVersion { get; set; }

        protected override void Validate(ValidationContext context)
        {
            ValidateWidth(context, nameof(MaxLineWidth1080P), MaxLineWidth1080P);

            if (CommonFontSize != 0 && (CommonFontSize < 6 || CommonFontSize > 300))
            {
                context.AddValidationError(nameof(CommonFontSize), "Fontsize 必须为 0，或在 6 到 300 之间。 ");
            }

            if (PrimaryFontSizePercent < 50 || PrimaryFontSizePercent > 200)
            {
                context.AddValidationError(nameof(PrimaryFontSizePercent), "主字幕 Fontsize 比例必须在 50 到 200 之间。 ");
            }

            if (SecondaryFontSizePercent < 50 || SecondaryFontSizePercent > 200)
            {
                context.AddValidationError(nameof(SecondaryFontSizePercent), "副字幕 Fontsize 比例必须在 50 到 200 之间。 ");
            }

            if (PrimaryCharacterSpacing < -20 || PrimaryCharacterSpacing > 50)
            {
                context.AddValidationError(nameof(PrimaryCharacterSpacing), "主字幕 Spacing 必须在 -20 到 50 之间。 ");
            }

            if (SecondaryCharacterSpacing < -20 || SecondaryCharacterSpacing > 50)
            {
                context.AddValidationError(nameof(SecondaryCharacterSpacing), "副字幕 Spacing 必须在 -20 到 50 之间。 ");
            }

            ValidateColor(context, nameof(PrimarySubtitleColor), PrimarySubtitleColor);
            ValidateColor(context, nameof(SecondarySubtitleColor), SecondarySubtitleColor);
            ValidateColor(context, nameof(PrimaryBorderColor), PrimaryBorderColor);
            ValidateColor(context, nameof(SecondaryBorderColor), SecondaryBorderColor);

            if (PrimaryBorderWidth < 0 || PrimaryBorderWidth > 20)
            {
                context.AddValidationError(nameof(PrimaryBorderWidth), "主字幕边框宽度必须在 0 到 20 之间。 ");
            }

            if (SecondaryBorderWidth < 0 || SecondaryBorderWidth > 20)
            {
                context.AddValidationError(nameof(SecondaryBorderWidth), "副字幕边框宽度必须在 0 到 20 之间。 ");
            }

            if (BilingualLineSpacing < 0 || BilingualLineSpacing > 80)
            {
                context.AddValidationError(nameof(BilingualLineSpacing), "双字幕上下间隔必须在 0 到 80 之间。 ");
            }

            if (BottomDistance1080P < 0 || BottomDistance1080P > 540)
            {
                context.AddValidationError(nameof(BottomDistance1080P), "距最底部距离必须在 0 到 540 之间。 ");
            }

            if (string.IsNullOrWhiteSpace(OutputSuffix) || !System.Text.RegularExpressions.Regex.IsMatch(OutputSuffix, "^[A-Za-z0-9_-]+$"))
            {
                context.AddValidationError(nameof(OutputSuffix), "输出文件后缀只能包含英文字母、数字、连字符或下划线。 ");
            }
        }

        private static void ValidateWidth(ValidationContext context, string propertyName, int value)
        {
            if (value < 20 || value > 120)
            {
                context.AddValidationError(propertyName, "最大行宽必须在 20 到 120 之间。 ");
            }
        }

        private static void ValidateColor(ValidationContext context, string propertyName, string value)
        {
            if (string.IsNullOrWhiteSpace(value)
                || !System.Text.RegularExpressions.Regex.IsMatch(value, "^#(?:[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$"))
            {
                context.AddValidationError(propertyName, "字体颜色必须使用 #RRGGBB 或 #AARRGGBB 格式。 ");
            }
        }
    }
}
