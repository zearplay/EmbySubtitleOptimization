using System;
using System.Collections.Generic;
using System.Linq;
using Emby.Web.GenericEdit.Common;

namespace EmbySubtitleOptimization.Fonts
{
    internal static class GenericFontCatalog
    {
        private static readonly string[] FontFamilies =
        {
            "Arial",
            "Arial Unicode MS",
            "Helvetica",
            "Verdana",
            "Tahoma",
            "Trebuchet MS",
            "Georgia",
            "Times New Roman",
            "Courier New",
            "Roboto",
            "Roboto Condensed",
            "Open Sans",
            "Lato",
            "Ubuntu",
            "Inter",
            "Montserrat",
            "Fira Sans",
            "Droid Sans",
            "Noto Sans",
            "Noto Serif",
            "Noto Sans CJK SC",
            "Noto Serif CJK SC",
            "Source Han Sans SC",
            "Source Han Serif SC",
            "Microsoft YaHei",
            "SimHei",
            "SimSun",
            "PingFang SC",
            "Heiti SC",
            "WenQuanYi Micro Hei",
            "WenQuanYi Zen Hei",
            "Sarasa Gothic SC",
            "DejaVu Sans",
            "DejaVu Serif",
            "Liberation Sans",
            "Liberation Serif",
            "sans-serif",
            "serif",
            "monospace"
        };

        public static IEnumerable<EditorSelectOption> CreateOptions()
        {
            return FontFamilies.Select(value => new EditorSelectOption
            {
                Value = value,
                Name = value,
                IsEnabled = true
            }).ToArray();
        }

        public static string Resolve(string requestedFont)
        {
            return FontFamilies.FirstOrDefault(value => string.Equals(value, requestedFont?.Trim(), StringComparison.OrdinalIgnoreCase))
                   ?? "Arial";
        }
    }
}
