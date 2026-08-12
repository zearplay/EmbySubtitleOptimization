using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace EmbySubtitleOptimization.Subtitles
{
    internal static class SrtSubtitleConverter
    {
        private static readonly Regex CueRegex = new Regex(
            @"(?ms)(?:^|\n)(?:\d+\s*\n)?(?<start>\d{1,2}:\d{2}:\d{2}[,.]\d{1,3})\s*-->\s*(?<end>\d{1,2}:\d{2}:\d{2}[,.]\d{1,3})[^\n]*\n(?<text>.*?)(?=\n{2,}|\z)",
            RegexOptions.Compiled);

        public static string Convert(string content, ResolutionProfile profile, PluginOptions options, string generationMarker)
        {
            var normalized = Regex.Replace(content ?? string.Empty, "\\r\\n?", "\n").Trim();
            var cues = ParseCues(normalized).ToList();
            var builder = new StringBuilder();

            AppendHeader(builder, profile, options, generationMarker);
            foreach (var cue in cues)
            {
                var assText = ConvertMarkup(cue.Text.Replace("\n", "\\N"));
                var formatted = SubtitleStyleFormatter.FormatAndOptimizeWithLayout(
                    assText,
                    profile,
                    options,
                    options.SrtDefaultFontName,
                    profile.FontSize);

                if (formatted.IsBilingual)
                {
                    var baseMargin = options.PositionMode == SubtitlePositionMode.BottomCenter
                        ? profile.ScaleVerticalFrom1080(options.BottomDistance1080P)
                        : profile.MarginV;
                    var gap = profile.ScaleVerticalFrom1080(options.BilingualLineSpacing);
                    var secondaryUpwardOffset = CalculateSecondaryUpwardOffset(
                        formatted.SecondaryFontSize,
                        options.SecondaryUpwardOffsetPercent);
                    var boundaryY = Math.Max(
                        0,
                        profile.Height
                        - baseMargin
                        - CalculateBlockHeight(formatted.SecondaryFontSize, formatted.SecondaryLineCount)
                        - gap);
                    var centerX = profile.Width / 2;
                    AppendDialogue(builder, cue, 0, ForcePosition(formatted.PrimaryText, 2, centerX, boundaryY));
                    AppendDialogue(builder, cue, 0, ForcePosition(formatted.SecondaryText, 8, centerX, boundaryY + gap - secondaryUpwardOffset));
                    continue;
                }

                AppendDialogue(builder, cue, 0, formatted.Text);
            }

            return builder.ToString();
        }

        private static void AppendDialogue(StringBuilder builder, Cue cue, int marginV, string text)
        {
            builder.Append("Dialogue: 0,")
                .Append(ToAssTime(cue.Start)).Append(',')
                .Append(ToAssTime(cue.End))
                .Append(",ESO,ESO,0,0,")
                .Append(marginV.ToString(CultureInfo.InvariantCulture))
                .Append(",,")
                .Append(text)
                .Append('\n');
        }

        private static int CalculateBlockHeight(double fontSize, int lineCount)
        {
            return Math.Max(1, (int)Math.Round(fontSize * Math.Max(1, lineCount), MidpointRounding.AwayFromZero));
        }

        private static int CalculateSecondaryUpwardOffset(double fontSize, int percent)
        {
            return Math.Max(0, (int)Math.Round(fontSize * percent / 100.0, MidpointRounding.AwayFromZero));
        }

        private static string ForcePosition(string text, int alignment, int x, int y)
        {
            return "{\\an" + alignment.ToString(CultureInfo.InvariantCulture)
                   + "\\pos(" + x.ToString(CultureInfo.InvariantCulture)
                   + "," + Math.Max(0, y).ToString(CultureInfo.InvariantCulture)
                   + ")}" + text;
        }

        private static IEnumerable<Cue> ParseCues(string content)
        {
            foreach (Match match in CueRegex.Matches(content))
            {
                if (TryParseTime(match.Groups["start"].Value, out var start)
                    && TryParseTime(match.Groups["end"].Value, out var end))
                {
                    yield return new Cue(start, end, match.Groups["text"].Value.Trim());
                }
            }
        }

        private static void AppendHeader(StringBuilder builder, ResolutionProfile profile, PluginOptions options, string generationMarker)
        {
            var font = string.IsNullOrWhiteSpace(options.SrtDefaultFontName) ? "Arial" : options.SrtDefaultFontName.Replace(",", string.Empty);
            var fontSize = options.CommonFontSize > 0 ? options.CommonFontSize : profile.FontSize;
            var marginV = options.PositionMode == SubtitlePositionMode.BottomCenter
                ? profile.ScaleVerticalFrom1080(options.BottomDistance1080P)
                : profile.MarginV;
            builder.AppendLine("[Script Info]")
                .Append("; ").AppendLine(generationMarker)
                .AppendLine("ScriptType: v4.00+")
                .AppendLine("WrapStyle: 2")
                .Append("PlayResX: ").AppendLine(profile.Width.ToString(CultureInfo.InvariantCulture))
                .Append("PlayResY: ").AppendLine(profile.Height.ToString(CultureInfo.InvariantCulture))
                .AppendLine()
                .AppendLine("[V4+ Styles]")
                .AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, OutlineColour, Bold, Italic, Underline, StrikeOut, Spacing, Angle, BorderStyle, Outline, Alignment, MarginL, MarginR, MarginV, Encoding")
                .Append("Style: ESO,").Append(font).Append(',').Append(fontSize.ToString("0.##", CultureInfo.InvariantCulture))
                .Append(",&H00FFFFFF,&H00000000,0,0,0,0,")
                .Append(options.PrimaryCharacterSpacing.ToString("0.##", CultureInfo.InvariantCulture)).Append(",0,1,0.1,2,")
                .Append(profile.Width / 20).Append(',').Append(profile.Width / 20).Append(',').Append(marginV)
                .AppendLine(",1")
                .AppendLine()
                .AppendLine("[Events]")
                .AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");
        }

        private static string ConvertMarkup(string text)
        {
            var decoded = WebUtility.HtmlDecode(text);
            decoded = Regex.Replace(decoded, @"<\s*i\s*>", "{\\i1}", RegexOptions.IgnoreCase);
            decoded = Regex.Replace(decoded, @"<\s*/\s*i\s*>", "{\\i0}", RegexOptions.IgnoreCase);
            decoded = Regex.Replace(decoded, @"<\s*b\s*>", "{\\b1}", RegexOptions.IgnoreCase);
            decoded = Regex.Replace(decoded, @"<\s*/\s*b\s*>", "{\\b0}", RegexOptions.IgnoreCase);
            decoded = Regex.Replace(decoded, @"<\s*u\s*>", "{\\u1}", RegexOptions.IgnoreCase);
            decoded = Regex.Replace(decoded, @"<\s*/\s*u\s*>", "{\\u0}", RegexOptions.IgnoreCase);
            return Regex.Replace(decoded, @"<[^>]+>", string.Empty);
        }

        private static bool TryParseTime(string value, out TimeSpan result)
        {
            return TimeSpan.TryParseExact(
                value.Replace(',', '.'),
                new[] { @"h\:mm\:ss\.f", @"h\:mm\:ss\.ff", @"h\:mm\:ss\.fff", @"hh\:mm\:ss\.f", @"hh\:mm\:ss\.ff", @"hh\:mm\:ss\.fff" },
                CultureInfo.InvariantCulture,
                out result);
        }

        private static string ToAssTime(TimeSpan time)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}.{3:00}", (int)time.TotalHours, time.Minutes, time.Seconds, time.Milliseconds / 10);
        }

        private sealed class Cue
        {
            public Cue(TimeSpan start, TimeSpan end, string text)
            {
                Start = start;
                End = end;
                Text = text;
            }

            public TimeSpan Start { get; }
            public TimeSpan End { get; }
            public string Text { get; }
        }
    }
}
