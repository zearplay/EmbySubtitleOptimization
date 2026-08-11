namespace EmbySubtitleOptimization.Subtitles
{
    internal sealed class ResolutionProfile
    {
        private ResolutionProfile(string name, int width, int height, int maxLineWidth, int fontSize, int marginV)
        {
            Name = name;
            Width = width;
            Height = height;
            MaxLineWidth = maxLineWidth;
            FontSize = fontSize;
            MarginV = marginV;
        }

        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        public int MaxLineWidth { get; }
        public int FontSize { get; }
        public int MarginV { get; }

        public static ResolutionProfile FromVideo(int width, int height, PluginOptions options)
        {
            if (height > 1600 || width > 2800)
            {
                return new ResolutionProfile("4K", width > 0 ? width : 3840, height > 0 ? height : 2160, options.MaxLineWidth4K, 84, 120);
            }

            if (height > 1080 || width > 1920)
            {
                return new ResolutionProfile("2K", width > 0 ? width : 2560, height > 0 ? height : 1440, options.MaxLineWidth2K, 60, 80);
            }

            return new ResolutionProfile("1080p", width > 0 ? width : 1920, height > 0 ? height : 1080, options.MaxLineWidth1080P, 46, 60);
        }
    }
}

