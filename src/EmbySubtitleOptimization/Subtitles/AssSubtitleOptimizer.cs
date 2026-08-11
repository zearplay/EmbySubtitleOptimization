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
            NormalizeStyleFields(lines);
            var eventSection = false;
            var textIndex = 9;
            var styleIndex = 3;
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

                var styleName = styleIndex >= 0 && fieldsInEvent.Length > styleIndex ? fieldsInEvent[styleIndex].Trim() : string.Empty;
                var fontSize = styleFontSizes.TryGetValue(styleName, out var configuredFontSize)
                    ? configuredFontSize
                    : profile.FontSize;
                var formattedText = SubtitleStyleFormatter.FormatAndOptimize(fieldsInEvent[textIndex], profile, options, null, fontSize);
                if (options.PositionMode == SubtitlePositionMode.BottomCenter)
                {
                    formattedText = ForceBottomCenter(formattedText, scriptWidth, scriptHeight, profile, options.BottomDistance1080P);
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

        private static string ForceBottomCenter(
            string text,
            int scriptWidth,
            int scriptHeight,
            ResolutionProfile profile,
            int bottomDistance1080P)
        {
            var withoutOriginalPosition = OverrideBlockRegex.Replace(text ?? string.Empty, match =>
            {
                var tags = InlinePositionRegex.Replace(match.Groups["tags"].Value, string.Empty);
                return tags.Length == 0 ? string.Empty : "{" + tags + "}";
            });
            var videoDistance = profile.ScaleVerticalFrom1080(bottomDistance1080P);
            var scaledDistance = (int)Math.Round(
                videoDistance * scriptHeight / (double)profile.Height,
                MidpointRounding.AwayFromZero);
            var x = scriptWidth / 2;
            var y = Math.Max(0, scriptHeight - scaledDistance);
            return "{\\an2\\pos(" + x.ToString(CultureInfo.InvariantCulture) + "," + y.ToString(CultureInfo.InvariantCulture) + ")}" + withoutOriginalPosition;
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

        private static void NormalizeStyleFields(IList<string> lines)
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
                    if (outputFields[fieldIndex].Equals(borderColorField, StringComparison.OrdinalIgnoreCase))
                    {
                        outputValues[fieldIndex] = "&H00000000";
                    }
                    else if (outputFields[fieldIndex].Equals("BorderStyle", StringComparison.OrdinalIgnoreCase)
                             || outputFields[fieldIndex].Equals("Outline", StringComparison.OrdinalIgnoreCase))
                    {
                        outputValues[fieldIndex] = "1";
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

    }
}
