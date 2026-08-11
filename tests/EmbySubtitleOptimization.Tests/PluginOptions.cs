namespace EmbySubtitleOptimization
{
    internal sealed class PluginOptions
    {
        public int MaxLineWidth1080P { get; set; } = 40;
        public int MaxLineWidth2K { get; set; } = 46;
        public int MaxLineWidth4K { get; set; } = 52;
        public double CommonFontSize { get; set; } = 17;
        public string PrimaryFontName { get; set; } = "Source Han Sans SC";
        public string SecondaryFontName { get; set; } = "Arial";
        public string SrtDefaultFontName { get; set; } = "Arial";
        public int PrimaryFontSizePercent { get; set; } = 100;
        public int SecondaryFontSizePercent { get; set; } = 70;
        public string PrimarySubtitleColor { get; set; } = "#FFFFFF";
        public string SecondarySubtitleColor { get; set; } = "#FFD966";
        public SubtitleFontStyle PrimaryFontStyle { get; set; } = SubtitleFontStyle.Bold;
        public SubtitleFontStyle SecondaryFontStyle { get; set; } = SubtitleFontStyle.Regular;
        public double PrimaryCharacterSpacing { get; set; }
        public double SecondaryCharacterSpacing { get; set; }
        public int BilingualLineSpacing { get; set; } = 0;
    }
}
