using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace EmbySubtitleOptimization.Subtitles
{
    internal static class AssSubtitleOptimizer
    {
        private static readonly Regex InlinePositionRegex = new Regex(
            @"\\(?:pos|move)\s*\([^)]*\)|\\an[1-9](?!\d)|\\a\d+(?!\d)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex OverrideBlockRegex = new Regex(@"\{(?<tags>[^}]*)\}", RegexOptions.Compiled);

        public static string Optimize(string content, ResolutionProfile profile, PluginOptions options, string generationMarker)
        {
            var normalized = NormalizeNewLines(content);
            var lines = normalized.Split('\n').ToList();
            var scriptWidth = ReadScriptResolution(lines, "PlayResX", profile.Width);
            var scriptHeight = ReadScriptResolution(lines, "PlayResY", profile.Height);
            var styleFontSizes = ReadStyleFontSizes(lines);
            var stylePositions = ReadStylePositions(lines);
            NormalizeStyleFields(lines, options);
            var eventSection = false;
            var textIndex = 9;
            var styleIndex = 3;
            var effectIndex = 8;
            var marginVIndex = 7;
            var fieldCount = 10;

            for (var index = 0; index < lines.Count; index++)
            {
                var trimmed = lines[index].Trim();
                if (trimmed.Equals("[Script Info]", StringComparison.OrdinalIgnoreCase))
                {
                    lines.Insert(index + 1, "; " + generationMarker);
                    index++;
                    continue;
                }

                if (trimmed.StartsWith("[", StringComparison.Ordinal))
                {
                    eventSection = trimmed.Equals("[Events]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (!eventSection)
                {
                    continue;
                }

                if (trimmed.StartsWith("Format:", StringComparison.OrdinalIgnoreCase))
                {
                    var fields = trimmed.Substring(7).Split(',').Select(field => field.Trim()).ToArray();
                    fieldCount = fields.Length;
                    textIndex = Array.FindIndex(fields, field => field.Equals("Text", StringComparison.OrdinalIgnoreCase));
                    styleIndex = Array.FindIndex(fields, field => field.Equals("Style", StringComparison.OrdinalIgnoreCase));
                    effectIndex = Array.FindIndex(fields, field => field.Equals("Effect", StringComparison.OrdinalIgnoreCase));
                    marginVIndex = Array.FindIndex(fields, field => field.Equals("MarginV", StringComparison.OrdinalIgnoreCase));
                    if (textIndex < 0) textIndex = fields.Length - 1;
                    continue;
                }

                if (!trimmed.StartsWith("Dialogue:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var prefixLength = lines[index].IndexOf(':') + 1;
                var fieldsInEvent = lines[index].Substring(prefixLength).Split(new[] { ',' }, fieldCount);
                if (fieldsInEvent.Length <= textIndex)
                {
                    continue;
                }

                var originalText = fieldsInEvent[textIndex];
                var hasEventEffect = effectIndex >= 0
                                     && fieldsInEvent.Length > effectIndex
                                     && !string.IsNullOrWhiteSpace(fieldsInEvent[effectIndex]);
                var isSpecialEffect = hasEventEffect || TextLayout.IsSpecialEffect(originalText);
                var styleName = styleIndex >= 0 && fieldsInEvent.Length > styleIndex ? fieldsInEvent[styleIndex].Trim() : string.Empty;
                var fontSize = styleFontSizes.TryGetValue(styleName, out var configuredFontSize)
                    ? configuredFontSize
                    : profile.FontSize;
                var formatted = SubtitleStyleFormatter.FormatAndOptimizeWithLayout(originalText, profile, options, null, fontSize);
                var formattedText = formatted.Text;
                if (options.PositionMode == SubtitlePositionMode.BottomCenter && !isSpecialEffect)
                {
                    var bottomMargin = CalculateBottomMargin(scriptHeight, profile, options.BottomDistance1080P);
                    if (marginVIndex >= 0 && fieldsInEvent.Length > marginVIndex)
                    {
                        fieldsInEvent[marginVIndex] = Math.Max(1, bottomMargin).ToString(CultureInfo.InvariantCulture);
                        if (formatted.IsBilingual)
                        {
                            var primaryFields = (string[])fieldsInEvent.Clone();
                            var secondaryFields = (string[])fieldsInEvent.Clone();
                            var gap = CalculateBilingualGap(scriptHeight, profile, options.BilingualLineSpacing);
                            var primaryMargin = bottomMargin + CalculateBlockHeight(formatted.SecondaryFontSize, formatted.SecondaryLineCount) + gap;
                            primaryFields[marginVIndex] = Math.Max(1, primaryMargin).ToString(CultureInfo.InvariantCulture);
                            primaryFields[textIndex] = ForceBottomCenter(formatted.PrimaryText);
                            secondaryFields[textIndex] = ForceBottomCenter(formatted.SecondaryText);
                            lines[index] = BuildEventLine(lines[index], prefixLength, primaryFields);
                            lines.Insert(index + 1, BuildEventLine(lines[index], prefixLength, secondaryFields));
                            index++;
                            continue;
                        }

                        formattedText = ForceBottomCenter(formattedText);
                    }
                    else
                    {
                        formattedText = ForceBottomCenterWithPosition(formattedText, scriptWidth, scriptHeight, bottomMargin);
                    }
                }

                if (formatted.IsBilingual
                    && !isSpecialEffect
                    && marginVIndex >= 0
                    && fieldsInEvent.Length > marginVIndex
                    && stylePositions.TryGetValue(styleName, out var stylePosition)
                    && (IsBottomAligned(stylePosition.Alignment) || IsTopAligned(stylePosition.Alignment)))
                {
                    var primaryFields = (string[])fieldsInEvent.Clone();
                    var secondaryFields = (string[])fieldsInEvent.Clone();
                    var effectiveMargin = ReadEventMargin(fieldsInEvent[marginVIndex], stylePosition.MarginV);
                    var gap = CalculateBilingualGap(scriptHeight, profile, options.BilingualLineSpacing);
                    if (IsBottomAligned(stylePosition.Alignment))
                    {
                        primaryFields[marginVIndex] = Math.Max(
                                1,
                                effectiveMargin + CalculateBlockHeight(formatted.SecondaryFontSize, formatted.SecondaryLineCount) + gap)
                            .ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        secondaryFields[marginVIndex] = Math.Max(
                                1,
                                effectiveMargin + CalculateBlockHeight(formatted.PrimaryFontSize, formatted.PrimaryLineCount) + gap)
                            .ToString(CultureInfo.InvariantCulture);
                    }

                    primaryFields[textIndex] = formatted.PrimaryText;
                    secondaryFields[textIndex] = formatted.SecondaryText;
                    lines[index] = BuildEventLine(lines[index], prefixLength, primaryFields);
                    lines.Insert(index + 1, BuildEventLine(lines[index], prefixLength, secondaryFields));
                    index++;
                    continue;
                }

                fieldsInEvent[textIndex] = formattedText;
                lines[index] = lines[index].Substring(0, prefixLength) + string.Join(",", fieldsInEvent);
            }

            if (!lines.Any(line => line.Trim().Equals("[Script Info]", StringComparison.OrdinalIgnoreCase)))
            {
                lines.Insert(0, "; " + generationMarker);
            }

            return string.Join("\n", lines).TrimEnd() + "\n";
        }

        private static string NormalizeNewLines(string content)
        {
            return Regex.Replace(content ?? string.Empty, "\\r\\n?", "\n");
        }

        private static int ReadScriptResolution(IEnumerable<string> lines, string fieldName, int fallback)
        {
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith(fieldName + ":", StringComparison.OrdinalIgnoreCase)) continue;
                if (double.TryParse(trimmed.Substring(trimmed.IndexOf(':') + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                    && value > 0)
                {
                    return Math.Max(1, (int)Math.Round(value));
                }
            }

            return Math.Max(1, fallback);
        }

        private static string ForceBottomCenter(string text)
        {
            var withoutOriginalPosition = OverrideBlockRegex.Replace(text ?? string.Empty, match =>
            {
                var tags = InlinePositionRegex.Replace(match.Groups["tags"].Value, string.Empty);
                return tags.Length == 0 ? string.Empty : "{" + tags + "}";
            });

            return "{\\an2}" + withoutOriginalPosition;
        }

        private static string ForceBottomCenterWithPosition(
            string text,
            int scriptWidth,
            int scriptHeight,
            int bottomMargin)
        {
            var withoutOriginalPosition = ForceBottomCenter(text);
            var x = scriptWidth / 2;
            var y = Math.Max(0, scriptHeight - bottomMargin);
            return "{\\pos(" + x.ToString(CultureInfo.InvariantCulture) + "," + y.ToString(CultureInfo.InvariantCulture) + ")}" + withoutOriginalPosition;
        }

        private static int CalculateBottomMargin(
            int scriptHeight,
            ResolutionProfile profile,
            int bottomDistance1080P)
        {
            var videoDistance = profile.ScaleVerticalFrom1080(bottomDistance1080P);
            return Math.Max(0, (int)Math.Round(
                videoDistance * scriptHeight / (double)profile.Height,
                MidpointRounding.AwayFromZero));
        }

        private static int CalculateBilingualGap(
            int scriptHeight,
            ResolutionProfile profile,
            int referenceGap)
        {
            if (referenceGap <= 0) return 0;
            var videoGap = profile.ScaleVerticalFrom1080(referenceGap);
            return Math.Max(1, (int)Math.Round(
                videoGap * scriptHeight / (double)profile.Height,
                MidpointRounding.AwayFromZero));
        }

        private static int CalculateBlockHeight(double fontSize, int lineCount)
        {
            return Math.Max(1, (int)Math.Round(fontSize * Math.Max(1, lineCount), MidpointRounding.AwayFromZero));
        }

        private static int ReadEventMargin(string value, int styleMargin)
        {
            return int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var eventMargin)
                   && eventMargin > 0
                ? eventMargin
                : Math.Max(0, styleMargin);
        }

        private static string BuildEventLine(string sourceLine, int prefixLength, string[] fields)
        {
            return sourceLine.Substring(0, prefixLength) + string.Join(",", fields);
        }

        private static bool IsBottomAligned(int alignment)
        {
            return alignment >= 1 && alignment <= 3;
        }

        private static bool IsTopAligned(int alignment)
        {
            return alignment >= 7 && alignment <= 9;
        }

        private static IReadOnlyDictionary<string, double> ReadStyleFontSizes(IReadOnlyList<string> lines)
        {
            var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var styleSection = false;
            var fieldCount = 23;
            var nameIndex = 0;
            var fontSizeIndex = 2;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("[", StringComparison.Ordinal))
                {
                    styleSection = trimmed.Equals("[V4+ Styles]", StringComparison.OrdinalIgnoreCase)
                                   || trimmed.Equals("[V4 Styles]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (!styleSection) continue;
                if (trimmed.StartsWith("Format:", StringComparison.OrdinalIgnoreCase))
                {
                    var fields = trimmed.Substring(7).Split(',').Select(field => field.Trim()).ToArray();
                    fieldCount = fields.Length;
                    nameIndex = Array.FindIndex(fields, field => field.Equals("Name", StringComparison.OrdinalIgnoreCase));
                    fontSizeIndex = Array.FindIndex(fields, field => field.Equals("Fontsize", StringComparison.OrdinalIgnoreCase));
                    continue;
                }

                if (!trimmed.StartsWith("Style:", StringComparison.OrdinalIgnoreCase)) continue;
                var values = trimmed.Substring(6).Split(new[] { ',' }, fieldCount);
                if (nameIndex < 0 || fontSizeIndex < 0 || values.Length <= Math.Max(nameIndex, fontSizeIndex)) continue;
                if (double.TryParse(values[fontSizeIndex].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var fontSize)
                    && fontSize > 0)
                {
                    result[values[nameIndex].Trim()] = fontSize;
                }
            }

            return result;
        }

        private static IReadOnlyDictionary<string, StylePosition> ReadStylePositions(IReadOnlyList<string> lines)
        {
            var result = new Dictionary<string, StylePosition>(StringComparer.OrdinalIgnoreCase);
            var styleSection = false;
            var legacyStyleSection = false;
            var fieldCount = 23;
            var nameIndex = 0;
            var alignmentIndex = 18;
            var marginVIndex = 21;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("[", StringComparison.Ordinal))
                {
                    legacyStyleSection = trimmed.Equals("[V4 Styles]", StringComparison.OrdinalIgnoreCase);
                    styleSection = trimmed.Equals("[V4+ Styles]", StringComparison.OrdinalIgnoreCase) || legacyStyleSection;
                    continue;
                }

                if (!styleSection) continue;
                if (trimmed.StartsWith("Format:", StringComparison.OrdinalIgnoreCase))
                {
                    var fields = trimmed.Substring(7).Split(',').Select(field => field.Trim()).ToArray();
                    fieldCount = fields.Length;
                    nameIndex = Array.FindIndex(fields, field => field.Equals("Name", StringComparison.OrdinalIgnoreCase));
                    alignmentIndex = Array.FindIndex(fields, field => field.Equals("Alignment", StringComparison.OrdinalIgnoreCase));
                    marginVIndex = Array.FindIndex(fields, field => field.Equals("MarginV", StringComparison.OrdinalIgnoreCase));
                    continue;
                }

                if (!trimmed.StartsWith("Style:", StringComparison.OrdinalIgnoreCase)) continue;
                var values = trimmed.Substring(6).TrimStart().Split(new[] { ',' }, fieldCount);
                if (nameIndex < 0
                    || alignmentIndex < 0
                    || marginVIndex < 0
                    || values.Length <= Math.Max(nameIndex, Math.Max(alignmentIndex, marginVIndex)))
                {
                    continue;
                }

                if (!int.TryParse(values[alignmentIndex].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var alignment))
                {
                    continue;
                }

                int.TryParse(values[marginVIndex].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var marginV);
                result[values[nameIndex].Trim()] = new StylePosition(
                    legacyStyleSection ? ConvertLegacyAlignment(alignment) : alignment,
                    marginV);
            }

            return result;
        }

        private static int ConvertLegacyAlignment(int alignment)
        {
            switch (alignment)
            {
                case 5: return 7;
                case 6: return 8;
                case 7: return 9;
                case 9: return 4;
                case 10: return 5;
                case 11: return 6;
                default: return alignment;
            }
        }

        private static void NormalizeStyleFields(IList<string> lines, PluginOptions options)
        {
            var styleSection = false;
            List<string> outputFields = null;
            List<int> sourceIndices = null;
            var styleFieldCount = 0;
            var borderColorField = "OutlineColour";

            for (var index = 0; index < lines.Count; index++)
            {
                var trimmed = lines[index].Trim();
                if (trimmed.StartsWith("ScaledBorderAndShadow:", StringComparison.OrdinalIgnoreCase))
                {
                    lines.RemoveAt(index--);
                    continue;
                }

                if (trimmed.StartsWith("[", StringComparison.Ordinal))
                {
                    var legacyStyleSection = trimmed.Equals("[V4 Styles]", StringComparison.OrdinalIgnoreCase);
                    styleSection = trimmed.Equals("[V4+ Styles]", StringComparison.OrdinalIgnoreCase) || legacyStyleSection;
                    borderColorField = legacyStyleSection ? "TertiaryColour" : "OutlineColour";
                    outputFields = null;
                    sourceIndices = null;
                    styleFieldCount = 0;
                    continue;
                }

                if (!styleSection) continue;
                if (trimmed.StartsWith("Format:", StringComparison.OrdinalIgnoreCase))
                {
                    var fields = trimmed.Substring(7).Split(',').Select(field => field.Trim()).ToArray();
                    styleFieldCount = fields.Length;
                    outputFields = new List<string>();
                    sourceIndices = new List<int>();
                    for (var fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
                    {
                        if (IsForbiddenStyleField(fields[fieldIndex], borderColorField)) continue;
                        var fieldName = borderColorField.Equals("OutlineColour", StringComparison.OrdinalIgnoreCase)
                                        && fields[fieldIndex].Equals("OutlineColor", StringComparison.OrdinalIgnoreCase)
                            ? "OutlineColour"
                            : fields[fieldIndex];
                        if (outputFields.Any(existing => existing.Equals(fieldName, StringComparison.OrdinalIgnoreCase))) continue;
                        outputFields.Add(fieldName);
                        sourceIndices.Add(fieldIndex);
                    }

                    EnsureStyleField(outputFields, sourceIndices, borderColorField);
                    EnsureStyleField(outputFields, sourceIndices, "BorderStyle");
                    EnsureStyleField(outputFields, sourceIndices, "Outline");
                    lines[index] = "Format: " + string.Join(", ", outputFields);
                    continue;
                }

                if (outputFields == null || !trimmed.StartsWith("Style:", StringComparison.OrdinalIgnoreCase)) continue;
                var values = trimmed.Substring(6).TrimStart().Split(new[] { ',' }, styleFieldCount);
                var outputValues = new string[outputFields.Count];
                for (var fieldIndex = 0; fieldIndex < outputFields.Count; fieldIndex++)
                {
                    if (outputFields[fieldIndex].Equals("Fontname", StringComparison.OrdinalIgnoreCase))
                    {
                        outputValues[fieldIndex] = options.PrimaryFontName;
                    }
                    else if (outputFields[fieldIndex].Equals("Fontsize", StringComparison.OrdinalIgnoreCase))
                    {
                        var sourceIndex = sourceIndices[fieldIndex];
                        var sourceValue = sourceIndex >= 0 && sourceIndex < values.Length ? values[sourceIndex] : string.Empty;
                        outputValues[fieldIndex] = ResolvePrimaryStyleFontSize(sourceValue, options);
                    }
                    else if (outputFields[fieldIndex].Equals("PrimaryColour", StringComparison.OrdinalIgnoreCase))
                    {
                        outputValues[fieldIndex] = SubtitleStyleFormatter.ToAssStyleColor(options.PrimarySubtitleColor);
                    }
                    else if (outputFields[fieldIndex].Equals("Bold", StringComparison.OrdinalIgnoreCase))
                    {
                        outputValues[fieldIndex] = IsBold(options.PrimaryFontStyle) ? "-1" : "0";
                    }
                    else if (outputFields[fieldIndex].Equals("Italic", StringComparison.OrdinalIgnoreCase))
                    {
                        outputValues[fieldIndex] = IsItalic(options.PrimaryFontStyle) ? "-1" : "0";
                    }
                    else if (outputFields[fieldIndex].Equals("Spacing", StringComparison.OrdinalIgnoreCase))
                    {
                        outputValues[fieldIndex] = options.PrimaryCharacterSpacing.ToString("0.##", CultureInfo.InvariantCulture);
                    }
                    else if (outputFields[fieldIndex].Equals(borderColorField, StringComparison.OrdinalIgnoreCase))
                    {
                        outputValues[fieldIndex] = SubtitleStyleFormatter.ToAssStyleColor(options.PrimaryBorderColor);
                    }
                    else if (outputFields[fieldIndex].Equals("BorderStyle", StringComparison.OrdinalIgnoreCase))
                    {
                        outputValues[fieldIndex] = "1";
                    }
                    else if (outputFields[fieldIndex].Equals("Outline", StringComparison.OrdinalIgnoreCase))
                    {
                        outputValues[fieldIndex] = options.PrimaryBorderEnabled
                            ? Math.Max(0, options.PrimaryBorderWidth).ToString("0.##", CultureInfo.InvariantCulture)
                            : "0";
                    }
                    else
                    {
                        var sourceIndex = sourceIndices[fieldIndex];
                        outputValues[fieldIndex] = sourceIndex >= 0 && sourceIndex < values.Length ? values[sourceIndex] : string.Empty;
                    }
                }

                lines[index] = "Style: " + string.Join(",", outputValues);
            }
        }

        private static string ResolvePrimaryStyleFontSize(string sourceValue, PluginOptions options)
        {
            var baseFontSize = options.CommonFontSize;
            if (baseFontSize <= 0
                && !double.TryParse(sourceValue, NumberStyles.Float, CultureInfo.InvariantCulture, out baseFontSize))
            {
                return sourceValue;
            }

            var resolved = Math.Max(1, Math.Round(baseFontSize * options.PrimaryFontSizePercent / 100.0, 2));
            return resolved.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static bool IsBold(SubtitleFontStyle fontStyle)
        {
            return fontStyle == SubtitleFontStyle.Bold || fontStyle == SubtitleFontStyle.BoldItalic;
        }

        private static bool IsItalic(SubtitleFontStyle fontStyle)
        {
            return fontStyle == SubtitleFontStyle.Italic || fontStyle == SubtitleFontStyle.BoldItalic;
        }

        private static void EnsureStyleField(ICollection<string> outputFields, ICollection<int> sourceIndices, string fieldName)
        {
            if (outputFields.Any(existing => existing.Equals(fieldName, StringComparison.OrdinalIgnoreCase))) return;
            outputFields.Add(fieldName);
            sourceIndices.Add(-1);
        }

        private static bool IsForbiddenStyleField(string fieldName, string borderColorField)
        {
            return fieldName.Equals("SecondaryColour", StringComparison.OrdinalIgnoreCase)
                   || fieldName.Equals("BackColour", StringComparison.OrdinalIgnoreCase)
                   || fieldName.Equals("ScaleX", StringComparison.OrdinalIgnoreCase)
                   || fieldName.Equals("ScaleY", StringComparison.OrdinalIgnoreCase)
                   || fieldName.Equals("Shadow", StringComparison.OrdinalIgnoreCase)
                   || (fieldName.Equals("TertiaryColour", StringComparison.OrdinalIgnoreCase)
                       && !borderColorField.Equals("TertiaryColour", StringComparison.OrdinalIgnoreCase))
                   || ((fieldName.Equals("OutlineColour", StringComparison.OrdinalIgnoreCase)
                        || fieldName.Equals("OutlineColor", StringComparison.OrdinalIgnoreCase))
                       && !borderColorField.Equals("OutlineColour", StringComparison.OrdinalIgnoreCase));
        }

        private sealed class StylePosition
        {
            public StylePosition(int alignment, int marginV)
            {
                Alignment = alignment;
                MarginV = marginV;
            }

            public int Alignment { get; }
            public int MarginV { get; }
        }

    }
}
