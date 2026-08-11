using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace EmbySubtitleOptimization.Subtitles
{
    internal static class SubtitleStyleFormatter
    {
        private static readonly Regex AssOverrideBlockRegex = new Regex(@"\{(?<tags>[^}]*)\}", RegexOptions.Compiled);
        private static readonly Regex InlineFontSizeRegex = new Regex(
            @"\\fs(?![a-z])\s*(?:[+-]?(?:\d+(?:\.\d*)?|\.\d+))?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex InlineStyleResetRegex = new Regex(
            @"\\r[^\\}]*",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ForbiddenInlineTagRegex = new Regex(
            @"\\(?:2c|3c|4c|blur|be|shad|xbord|bord|xshad|yshad|fscx|fscy)(?![a-z])\s*(?:&H[0-9a-f]+&?|[+-]?(?:\d+(?:\.\d*)?|\.\d+))?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static string FormatAndOptimize(
            string text,
            ResolutionProfile profile,
            PluginOptions options,
            string singleFontName = null,
            double? styleFontSize = null)
        {
            var sanitizedText = RemoveInlineStyleResets(RemoveForbiddenInlineTags(text));
            var inheritedFontSize = styleFontSize > 0 ? styleFontSize.Value : profile.FontSize;
            var baseFontSize = options.CommonFontSize > 0 ? options.CommonFontSize : inheritedFontSize;
            var primaryFontSize = ScaleFontSize(baseFontSize, options.PrimaryFontSizePercent);
            var secondaryFontSize = ScaleFontSize(baseFontSize, options.SecondaryFontSizePercent);
            if (string.IsNullOrWhiteSpace(sanitizedText))
            {
                return sanitizedText;
            }

            if (TextLayout.IsSpecialEffect(sanitizedText))
            {
                return NormalizeInlineFontSize(sanitizedText, primaryFontSize);
            }

            var inheritedFontSizeText = RemoveInlineFontSize(sanitizedText);
            var originalLines = Regex.Split(inheritedFontSizeText, @"\\[Nn]");
            var isBilingual = originalLines.Length == 2 && TextLayout.IsBilingualPair(originalLines[0], originalLines[1]);
            if (!isBilingual)
            {
                var fontName = string.IsNullOrWhiteSpace(singleFontName) ? options.PrimaryFontName : singleFontName;
                var styled = BuildOverride(options.PrimarySubtitleColor, options.PrimaryFontStyle, fontName, primaryFontSize, options.PrimaryCharacterSpacing) + inheritedFontSizeText;
                return Optimize(styled, profile, options);
            }

            var primaryOverride = BuildOverride(options.PrimarySubtitleColor, options.PrimaryFontStyle, options.PrimaryFontName, primaryFontSize, options.PrimaryCharacterSpacing);
            var secondaryOverride = BuildOverride(options.SecondarySubtitleColor, options.SecondaryFontStyle, options.SecondaryFontName, secondaryFontSize, options.SecondaryCharacterSpacing);
            var bilingualText = primaryOverride + originalLines[0] + "\\N" + secondaryOverride + originalLines[1];
            var optimized = Optimize(bilingualText, profile, options);
            return InsertBilingualGap(optimized, secondaryOverride, profile, options.BilingualLineSpacing);
        }

        internal static string RemoveInlineFontSize(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            return AssOverrideBlockRegex.Replace(text, match =>
            {
                var tags = InlineFontSizeRegex.Replace(match.Groups["tags"].Value, string.Empty);
                return tags.Length == 0 ? string.Empty : "{" + tags + "}";
            });
        }

        internal static string RemoveForbiddenInlineTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            return AssOverrideBlockRegex.Replace(text, match =>
            {
                var tags = ForbiddenInlineTagRegex.Replace(match.Groups["tags"].Value, string.Empty);
                return tags.Length == 0 ? string.Empty : "{" + tags + "}";
            });
        }

        internal static string RemoveInlineStyleResets(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            return AssOverrideBlockRegex.Replace(text, match =>
            {
                var tags = InlineStyleResetRegex.Replace(match.Groups["tags"].Value, string.Empty);
                return tags.Length == 0 ? string.Empty : "{" + tags + "}";
            });
        }

        internal static string NormalizeInlineFontSize(string text, double styleFontSize)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var replacement = "\\fs" + styleFontSize.ToString("0.##", CultureInfo.InvariantCulture);
            var inherited = AssOverrideBlockRegex.Replace(text, match =>
            {
                var tags = InlineFontSizeRegex.Replace(match.Groups["tags"].Value, string.Empty);
                return tags.Length == 0 ? string.Empty : "{" + tags + "}";
            });
            return "{" + replacement + "}" + inherited;
        }

        internal static string BuildOverride(string htmlColor, SubtitleFontStyle fontStyle, string fontName, double fontSize, double characterSpacing)
        {
            var assColor = ToAssColor(htmlColor, out var assAlpha);
            var bold = fontStyle == SubtitleFontStyle.Bold || fontStyle == SubtitleFontStyle.BoldItalic ? 1 : 0;
            var italic = fontStyle == SubtitleFontStyle.Italic || fontStyle == SubtitleFontStyle.BoldItalic ? 1 : 0;
            var safeFontName = Regex.Replace(fontName ?? "Arial", @"[{}\\]", string.Empty).Trim();
            if (safeFontName.Length == 0) safeFontName = "Arial";
            var size = fontSize.ToString("0.##", CultureInfo.InvariantCulture);
            var spacing = characterSpacing.ToString("0.##", CultureInfo.InvariantCulture);
            return "{\\fn" + safeFontName + "\\fs" + size + "\\fsp" + spacing + "\\c&H" + assColor + "&\\alpha&H" + assAlpha + "&\\b" + bold + "\\i" + italic + "}";
        }

        private static string Optimize(string text, ResolutionProfile profile, PluginOptions options)
        {
            return TextLayout.OptimizeAssText(text, profile.MaxLineWidth);
        }

        private static double ScaleFontSize(double styleFontSize, int percent)
        {
            return Math.Max(1, Math.Round(styleFontSize * percent / 100.0, 2));
        }

        private static string InsertBilingualGap(string text, string secondaryOverride, ResolutionProfile profile, int referenceGap)
        {
            if (referenceGap <= 0)
            {
                return text;
            }

            var secondaryIndex = text.IndexOf(secondaryOverride, StringComparison.Ordinal);
            if (secondaryIndex < 0)
            {
                return text;
            }

            var separatorIndex = text.LastIndexOf("\\N", secondaryIndex, StringComparison.Ordinal);
            if (separatorIndex < 0)
            {
                return text;
            }

            var scaledGap = Math.Max(1, (int)Math.Round(referenceGap * profile.Height / 1080.0));
            var spacer = "\\N{\\fs" + scaledGap.ToString(CultureInfo.InvariantCulture) + "}\\h{\\r}\\N";
            return text.Substring(0, separatorIndex) + spacer + text.Substring(separatorIndex + 2);
        }

        private static string ToAssColor(string htmlColor, out string assAlpha)
        {
            var normalized = (htmlColor ?? "#FFFFFF").Trim().TrimStart('#');
            if (normalized.Length != 6 && normalized.Length != 8)
            {
                normalized = "FFFFFF";
            }

            var alpha = normalized.Length == 8 ? Convert.ToByte(normalized.Substring(0, 2), 16) : (byte)255;
            var rgbOffset = normalized.Length == 8 ? 2 : 0;
            var red = normalized.Substring(rgbOffset, 2);
            var green = normalized.Substring(rgbOffset + 2, 2);
            var blue = normalized.Substring(rgbOffset + 4, 2);
            assAlpha = (255 - alpha).ToString("X2", CultureInfo.InvariantCulture);
            return (blue + green + red).ToUpperInvariant();
        }
    }
}
