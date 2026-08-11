using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace EmbySubtitleOptimization.Subtitles
{
    internal static class AssSubtitleOptimizer
    {
        public static string Optimize(string content, ResolutionProfile profile, PluginOptions options, string generationMarker)
        {
            var normalized = NormalizeNewLines(content);
            var lines = normalized.Split('\n').ToList();
            var styleFontSizes = ReadStyleFontSizes(lines);
            RemoveForbiddenStyleFields(lines);
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
                fieldsInEvent[textIndex] = SubtitleStyleFormatter.FormatAndOptimize(fieldsInEvent[textIndex], profile, options, null, fontSize);
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

        private static void RemoveForbiddenStyleFields(IList<string> lines)
        {
            var styleSection = false;
            int[] keptIndices = null;
            var styleFieldCount = 0;

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
                    styleSection = trimmed.Equals("[V4+ Styles]", StringComparison.OrdinalIgnoreCase)
                                   || trimmed.Equals("[V4 Styles]", StringComparison.OrdinalIgnoreCase);
                    keptIndices = null;
                    styleFieldCount = 0;
                    continue;
                }

                if (!styleSection) continue;
                if (trimmed.StartsWith("Format:", StringComparison.OrdinalIgnoreCase))
                {
                    var fields = trimmed.Substring(7).Split(',').Select(field => field.Trim()).ToArray();
                    styleFieldCount = fields.Length;
                    keptIndices = fields.Select((field, fieldIndex) => new { field, fieldIndex })
                        .Where(value => !IsForbiddenStyleField(value.field))
                        .Select(value => value.fieldIndex)
                        .ToArray();
                    lines[index] = "Format: " + string.Join(", ", keptIndices.Select(fieldIndex => fields[fieldIndex]));
                    continue;
                }

                if (keptIndices == null || !trimmed.StartsWith("Style:", StringComparison.OrdinalIgnoreCase)) continue;
                var values = trimmed.Substring(6).TrimStart().Split(new[] { ',' }, styleFieldCount);
                lines[index] = "Style: " + string.Join(",", keptIndices.Where(fieldIndex => fieldIndex < values.Length).Select(fieldIndex => values[fieldIndex]));
            }
        }

        private static bool IsForbiddenStyleField(string fieldName)
        {
            return fieldName.Equals("SecondaryColour", StringComparison.OrdinalIgnoreCase)
                   || fieldName.Equals("OutlineColour", StringComparison.OrdinalIgnoreCase)
                   || fieldName.Equals("OutlineColor", StringComparison.OrdinalIgnoreCase)
                   || fieldName.Equals("TertiaryColour", StringComparison.OrdinalIgnoreCase)
                   || fieldName.Equals("BackColour", StringComparison.OrdinalIgnoreCase)
                   || fieldName.Equals("ScaleX", StringComparison.OrdinalIgnoreCase)
                   || fieldName.Equals("ScaleY", StringComparison.OrdinalIgnoreCase)
                   || fieldName.Equals("BorderStyle", StringComparison.OrdinalIgnoreCase)
                   || fieldName.Equals("Outline", StringComparison.OrdinalIgnoreCase)
                   || fieldName.Equals("Shadow", StringComparison.OrdinalIgnoreCase);
        }

    }
}
