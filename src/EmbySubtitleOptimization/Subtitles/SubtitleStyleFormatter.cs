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
            @"\\(?:2c|3c|3a|4c|blur|be|shad|xbord|bord|xshad|yshad|fscx|fscy)(?![a-z])\s*(?:&H[0-9a-f]+&?|[+-]?(?:\d+(?:\.\d*)?|\.\d+))?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ConfigurableInlineTagRegex = new Regex(
            @"\\fn[^\\}]*|\\fsp\s*[+-]?(?:\d+(?:\.\d*)?|\.\d+)?|\\(?:1c|c|1a|alpha)(?![a-z])\s*(?:&H[0-9a-f]+&?)?|\\[bi](?![a-z])\s*[+-]?\d*",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static string FormatAndOptimize(
            string text,
            ResolutionProfile profile,
            PluginOptions options,
            string singleFontName = null,
            double? styleFontSize = null)
        {
            return FormatAndOptimizeWithLayout(text, profile, options, singleFontName, styleFontSize).Text;
        }

        internal static FormattedSubtitle FormatAndOptimizeWithLayout(
            string text,
            ResolutionProfile profile,
            PluginOptions options,
            string singleFontName = null,
            double? styleFontSize = null)
        {
            var sanitizedText = RemoveConfigurableInlineTags(RemoveForbiddenInlineTags(text));
            var inheritedFontSize = styleFontSize > 0 ? styleFontSize.Value : profile.FontSize;
            var baseFontSize = options.CommonFontSize > 0 ? options.CommonFontSize : inheritedFontSize;
            var primaryFontSize = ScaleFontSize(baseFontSize, options.PrimaryFontSizePercent);
            var secondaryFontSize = ScaleFontSize(baseFontSize, options.SecondaryFontSizePercent);
            if (string.IsNullOrWhiteSpace(sanitizedText))
            {
                return FormattedSubtitle.Single(sanitizedText);
            }

            if (TextLayout.IsSpecialEffect(sanitizedText))
            {
                var specialOverride = BuildOverride(
                    options.PrimarySubtitleColor,
                    options.PrimaryFontStyle,
                    options.PrimaryFontName,
                    primaryFontSize,
                    options.PrimaryCharacterSpacing,
                    options.PrimaryBorderEnabled,
                    options.PrimaryBorderWidth,
                    options.PrimaryBorderColor);
                var specialText = RemoveInlineFontSize(sanitizedText);
                return FormattedSubtitle.Single(specialOverride + ReapplyOverrideAfterStyleResets(specialText, specialOverride));
            }

            var inheritedFontSizeText = RemoveInlineFontSize(sanitizedText);
            var originalLines = Regex.Split(inheritedFontSizeText, @"\\[Nn]");
            var isBilingual = originalLines.Length == 2 && TextLayout.IsBilingualPair(originalLines[0], originalLines[1]);
            if (!isBilingual)
            {
                var fontName = string.IsNullOrWhiteSpace(singleFontName) ? options.PrimaryFontName : singleFontName;
                var singleOverride = BuildOverride(
                    options.PrimarySubtitleColor,
                    options.PrimaryFontStyle,
                    fontName,
                    primaryFontSize,
                    options.PrimaryCharacterSpacing,
                    options.PrimaryBorderEnabled,
                    options.PrimaryBorderWidth,
                    options.PrimaryBorderColor);
                var styled = singleOverride + ReapplyOverrideAfterStyleResets(inheritedFontSizeText, singleOverride);
                return FormattedSubtitle.Single(Optimize(styled, profile, options));
            }

            var primaryOverride = BuildOverride(
                options.PrimarySubtitleColor,
                options.PrimaryFontStyle,
                options.PrimaryFontName,
                primaryFontSize,
                options.PrimaryCharacterSpacing,
                options.PrimaryBorderEnabled,
                options.PrimaryBorderWidth,
                options.PrimaryBorderColor);
            var secondaryOverride = BuildOverride(
                options.SecondarySubtitleColor,
                options.SecondaryFontStyle,
                options.SecondaryFontName,
                secondaryFontSize,
                options.SecondaryCharacterSpacing,
                options.SecondaryBorderEnabled,
                options.SecondaryBorderWidth,
                options.SecondaryBorderColor);
            var primaryText = ReapplyOverrideAfterStyleResets(originalLines[0], primaryOverride);
            var secondaryText = ReapplyOverrideAfterStyleResets(originalLines[1], secondaryOverride);
            var optimizedPrimary = Optimize(primaryOverride + primaryText, profile, options);
            var optimizedSecondary = Optimize(secondaryOverride + secondaryText, profile, options);
            var bilingualText = optimizedPrimary + "\\N" + optimizedSecondary;
            var optimized = Optimize(bilingualText, profile, options);
            return FormattedSubtitle.Bilingual(
                InsertBilingualGap(optimized, secondaryOverride, profile, options.BilingualLineSpacing),
                optimizedPrimary,
                optimizedSecondary,
                primaryFontSize,
                secondaryFontSize);
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

        internal static string RemoveConfigurableInlineTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            return AssOverrideBlockRegex.Replace(text, match =>
            {
                var tags = ConfigurableInlineTagRegex.Replace(match.Groups["tags"].Value, string.Empty);
                return tags.Length == 0 ? string.Empty : "{" + tags + "}";
            });
        }

        internal static string ReapplyOverrideAfterStyleResets(string text, string styleOverride)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var overrideTags = styleOverride.TrimStart('{').TrimEnd('}');

            return AssOverrideBlockRegex.Replace(text, match =>
            {
                var tags = InlineStyleResetRegex.Replace(
                    match.Groups["tags"].Value,
                    reset => "\\r" + overrideTags);
                return "{" + tags + "}";
            });
        }

        internal static string NormalizeInlineFontSize(string text, double styleFontSize)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var replacement = "\\fs" + styleFontSize.ToString("0.##", CultureInfo.InvariantCulture);
            var inherited = AssOverrideBlockRegex.Replace(text, match =>
            {
                var tags = InlineFontSizeRegex.Replace(match.Groups["tags"].Value, string.Empty);
                tags = InlineStyleResetRegex.Replace(tags, reset => reset.Value + replacement);
                return tags.Length == 0 ? string.Empty : "{" + tags + "}";
            });
            return "{" + replacement + "}" + inherited;
        }

        internal static string BuildOverride(
            string htmlColor,
            SubtitleFontStyle fontStyle,
            string fontName,
            double fontSize,
            double characterSpacing,
            bool borderEnabled,
            double borderWidth,
            string borderColor)
        {
            var assColor = ToAssColor(htmlColor, out var assAlpha);
            var bold = fontStyle == SubtitleFontStyle.Bold || fontStyle == SubtitleFontStyle.BoldItalic ? 1 : 0;
            var italic = fontStyle == SubtitleFontStyle.Italic || fontStyle == SubtitleFontStyle.BoldItalic ? 1 : 0;
            var safeFontName = Regex.Replace(fontName ?? "Arial", @"[{}\\]", string.Empty).Trim();
            if (safeFontName.Length == 0) safeFontName = "Arial";
            var size = fontSize.ToString("0.##", CultureInfo.InvariantCulture);
            var spacing = characterSpacing.ToString("0.##", CultureInfo.InvariantCulture);
            var borderTags = BuildBorderTags(borderEnabled, borderWidth, borderColor);
            return "{\\fn" + safeFontName + "\\fs" + size + "\\fsp" + spacing + "\\c&H" + assColor + "&\\alpha&H" + assAlpha + "&\\b" + bold + "\\i" + italic + borderTags + "}";
        }

        private static string BuildBorderTags(bool borderEnabled, double borderWidth, string borderColor)
        {
            if (!borderEnabled) return "\\bord0";
            var assBorderColor = ToAssColor(borderColor, out var assBorderAlpha);
            var width = Math.Max(0, borderWidth).ToString("0.##", CultureInfo.InvariantCulture);
            return "\\3c&H" + assBorderColor + "&\\3a&H" + assBorderAlpha + "&\\bord" + width;
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

        internal static string ToAssStyleColor(string htmlColor)
        {
            var assColor = ToAssColor(htmlColor, out var assAlpha);
            return "&H" + assAlpha + assColor;
        }
    }

    internal sealed class FormattedSubtitle
    {
        private FormattedSubtitle(
            string text,
            string primaryText,
            string secondaryText,
            double primaryFontSize,
            double secondaryFontSize)
        {
            Text = text;
            PrimaryText = primaryText;
            SecondaryText = secondaryText;
            PrimaryFontSize = primaryFontSize;
            SecondaryFontSize = secondaryFontSize;
        }

        public string Text { get; }
        public string PrimaryText { get; }
        public string SecondaryText { get; }
        public double PrimaryFontSize { get; }
        public double SecondaryFontSize { get; }
        public bool IsBilingual => PrimaryText != null && SecondaryText != null;
        public int PrimaryLineCount => CountLines(PrimaryText);
        public int SecondaryLineCount => CountLines(SecondaryText);

        public static FormattedSubtitle Single(string text)
        {
            return new FormattedSubtitle(text, null, null, 0, 0);
        }

        public static FormattedSubtitle Bilingual(
            string text,
            string primaryText,
            string secondaryText,
            double primaryFontSize,
            double secondaryFontSize)
        {
            return new FormattedSubtitle(text, primaryText, secondaryText, primaryFontSize, secondaryFontSize);
        }

        private static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return Regex.Split(text, @"\\[Nn]").Length;
        }
    }
}
