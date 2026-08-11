using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace EmbySubtitleOptimization.Subtitles
{
    internal static class TextLayout
    {
        private static readonly Regex AssOverrideRegex = new Regex(@"\{[^}]*\}", RegexOptions.Compiled);
        private static readonly Regex HtmlTagRegex = new Regex(@"<[^>]+>", RegexOptions.Compiled);
        private static readonly char[] PreferredBreakCharacters = { ' ', '\t', ',', '.', '!', '?', ';', ':', '，', '。', '！', '？', '；', '：', '、', '—', '-', '）', ')', '】', ']' };

        public static string OptimizeAssText(string text, int maxWidth)
        {
            if (string.IsNullOrWhiteSpace(text) || IsSpecialEffect(text))
            {
                return text;
            }

            var originalLines = Regex.Split(text, @"\\[Nn]");

            var wrapped = originalLines.SelectMany(line => WrapLine(line, maxWidth)).ToList();
            return string.Join("\\N", wrapped);
        }

        public static IReadOnlyList<string> WrapLine(string line, int maxWidth)
        {
            var result = new List<string>();
            var remaining = line?.Trim() ?? string.Empty;

            while (Measure(remaining) > maxWidth)
            {
                var splitIndex = FindSplitIndex(remaining, maxWidth);
                if (splitIndex <= 0 || splitIndex >= remaining.Length)
                {
                    break;
                }

                var first = remaining.Substring(0, splitIndex).TrimEnd();
                if (first.Length == 0)
                {
                    break;
                }

                result.Add(first);
                remaining = remaining.Substring(splitIndex).TrimStart();
            }

            if (remaining.Length > 0 || result.Count == 0)
            {
                result.Add(remaining);
            }

            return result;
        }

        public static int Measure(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            var visible = HtmlTagRegex.Replace(AssOverrideRegex.Replace(text, string.Empty), string.Empty);
            var width = 0;
            for (var index = 0; index < visible.Length; index++)
            {
                var character = visible[index];
                if (char.IsHighSurrogate(character) && index + 1 < visible.Length && char.IsLowSurrogate(visible[index + 1]))
                {
                    width += 2;
                    index++;
                    continue;
                }

                var category = char.GetUnicodeCategory(character);
                if (category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.EnclosingMark || category == UnicodeCategory.Format)
                {
                    continue;
                }

                width += IsWide(character) ? 2 : 1;
            }

            return width;
        }

        public static bool IsBilingualPair(string first, string second)
        {
            var firstCjk = CountCjk(first);
            var secondCjk = CountCjk(second);
            var firstLetters = CountLetters(first);
            var secondLetters = CountLetters(second);

            return (firstCjk >= 2 && secondLetters >= 3 && secondCjk * 2 < firstCjk)
                   || (secondCjk >= 2 && firstLetters >= 3 && firstCjk * 2 < secondCjk);
        }

        private static int FindSplitIndex(string text, int maxWidth)
        {
            var width = 0;
            var inAssTag = false;
            var inHtmlTag = false;
            var candidates = new List<int>();

            for (var index = 0; index < text.Length; index++)
            {
                var character = text[index];
                if (character == '{') inAssTag = true;
                if (character == '<') inHtmlTag = true;

                if (!inAssTag && !inHtmlTag)
                {
                    width += IsWide(character) ? 2 : 1;
                    if (PreferredBreakCharacters.Contains(character))
                    {
                        candidates.Add(index + 1);
                    }
                }

                if (character == '}') inAssTag = false;
                if (character == '>') inHtmlTag = false;

                if (width >= maxWidth)
                {
                    var lowerBound = maxWidth * 55 / 100;
                    var preferred = candidates.LastOrDefault(candidate => Measure(text.Substring(0, candidate)) >= lowerBound);
                    return preferred > 0 ? preferred : index + 1;
                }
            }

            return text.Length;
        }

        internal static bool IsSpecialEffect(string text)
        {
            var lower = text.ToLowerInvariant();
            return Regex.IsMatch(lower, @"\\(?:pos|move|clip|iclip|t|fad|fade|org)\s*\(")
                   || Regex.IsMatch(lower, @"\\p[1-9]\d*")
                   || Regex.IsMatch(lower, @"\\k[fo]?\d+")
                   || Regex.IsMatch(lower, @"\\an[1-9](?!\d)|\\a\d+")
                   || Regex.IsMatch(lower, @"\\(?:fr[xyz]?|fa[xy])(?![a-z])\s*[+-]?(?:\d+(?:\.\d*)?|\.\d+)");
        }

        private static int CountCjk(string text)
        {
            return text.Count(character => (character >= 0x3400 && character <= 0x9fff)
                                           || (character >= 0x3040 && character <= 0x30ff)
                                           || (character >= 0xac00 && character <= 0xd7af));
        }

        private static int CountLetters(string text)
        {
            return text.Count(character => character <= 0x024f && char.IsLetter(character));
        }

        private static bool IsWide(char character)
        {
            return (character >= 0x1100 && character <= 0x115f)
                   || (character >= 0x2e80 && character <= 0xa4cf)
                   || (character >= 0xac00 && character <= 0xd7a3)
                   || (character >= 0xf900 && character <= 0xfaff)
                   || (character >= 0xfe10 && character <= 0xfe6f)
                   || (character >= 0xff00 && character <= 0xff60)
                   || (character >= 0xffe0 && character <= 0xffe6);
        }
    }
}
