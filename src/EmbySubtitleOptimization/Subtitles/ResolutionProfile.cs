using System;

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

        public int ScaleVerticalFrom1080(int value)
        {
            return Math.Max(0, (int)Math.Round(value * Height / 1080.0, MidpointRounding.AwayFromZero));
        }

        public static ResolutionProfile FromVideo(int width, int height, PluginOptions options)
        {
            var resolvedWidth = width > 0 ? width : 1920;
            var resolvedHeight = height > 0 ? height : 1080;
            var maxLineWidth = CalculateMaxLineWidth(width, height, options.MaxLineWidth1080P);

            if (resolvedHeight > 1600 || resolvedWidth > 2800)
            {
                return new ResolutionProfile("4K", resolvedWidth, resolvedHeight, maxLineWidth, 84, 120);
            }

            if (resolvedHeight > 1080 || resolvedWidth > 1920)
            {
                return new ResolutionProfile("2K", resolvedWidth, resolvedHeight, maxLineWidth, 60, 80);
            }

            return new ResolutionProfile("1080p", resolvedWidth, resolvedHeight, maxLineWidth, 46, 60);
        }

        internal static int CalculateMaxLineWidth(int screenWidth, int screenHeight, int baseLineWidth)
        {
            if (screenWidth > 0 && screenHeight > 0 && screenWidth < screenHeight)
            {
                return baseLineWidth;
            }

            var effectiveWidth = screenWidth > 0 ? screenWidth : 1920;
            return Math.Max(1, (int)Math.Round(baseLineWidth * effectiveWidth / 1920.0, MidpointRounding.AwayFromZero));
        }
    }
}
