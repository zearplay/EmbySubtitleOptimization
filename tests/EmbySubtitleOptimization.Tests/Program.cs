using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using EmbySubtitleOptimization.Subtitles;

namespace EmbySubtitleOptimization.Tests
{
    internal static class Program
    {
        private static int failures;

        private static int Main()
        {
            TestResolutionProfiles();
            TestCjkWidthAndWrapping();
            TestBilingualHasNoHorizontalScale();
            TestSingleAndBilingualStyles();
            TestSpecialEffectsArePreserved();
            TestSrtConversion();
            TestAssEventWithCommas();
            TestSubtitlePositionModes();
            TestFileProcessingIsIncremental();
            TestLibrarySubtitleScanner();

            Console.WriteLine(failures == 0 ? "All subtitle optimizer tests passed." : failures + " test(s) failed.");
            return failures == 0 ? 0 : 1;
        }

        private static void TestResolutionProfiles()
        {
            var options = new PluginOptions();
            var profile1080P = ResolutionProfile.FromVideo(1920, 1080, options);
            var profile1440P = ResolutionProfile.FromVideo(2560, 1440, options);
            var profile2160P = ResolutionProfile.FromVideo(3840, 2160, options);
            Equal("1080p", profile1080P.Name, "1080p profile");
            Equal("2K", profile1440P.Name, "2K profile");
            Equal("4K", profile2160P.Name, "4K profile");
            var landscapeCases = new[]
            {
                (Name: "VGA", Width: 640, Height: 480, Expected: 27),
                (Name: "SD", Width: 720, Height: 480, Expected: 30),
                (Name: "SVGA", Width: 800, Height: 600, Expected: 33),
                (Name: "XGA", Width: 1024, Height: 768, Expected: 43),
                (Name: "SXGA", Width: 1280, Height: 1024, Expected: 53),
                (Name: "HD", Width: 1280, Height: 720, Expected: 53),
                (Name: "WXGA 16:10", Width: 1280, Height: 800, Expected: 53),
                (Name: "WXGA 16:9", Width: 1366, Height: 768, Expected: 57),
                (Name: "SXGA+", Width: 1400, Height: 1050, Expected: 58),
                (Name: "UXGA", Width: 1600, Height: 1200, Expected: 67),
                (Name: "FHD", Width: 1920, Height: 1080, Expected: 80),
                (Name: "WUXGA", Width: 1920, Height: 1200, Expected: 80),
                (Name: "UW-FHD", Width: 2560, Height: 1080, Expected: 107),
                (Name: "QHD", Width: 2560, Height: 1440, Expected: 107),
                (Name: "UW-QHD", Width: 3440, Height: 1440, Expected: 143),
                (Name: "DFHD", Width: 3840, Height: 1080, Expected: 160),
                (Name: "UHD 4K", Width: 3840, Height: 2160, Expected: 160),
                (Name: "DCI 4K", Width: 4096, Height: 2160, Expected: 171),
                (Name: "DQHD", Width: 5120, Height: 1440, Expected: 213),
                (Name: "8K UHD", Width: 7680, Height: 4320, Expected: 320)
            };
            foreach (var resolution in landscapeCases)
            {
                Equal(
                    resolution.Expected,
                    ResolutionProfile.FromVideo(resolution.Width, resolution.Height, options).MaxLineWidth,
                    resolution.Name + " landscape line-width adaptation");
            }

            var customBaseOptions = new PluginOptions { MaxLineWidth1080P = 60 };
            foreach (var resolution in landscapeCases)
            {
                True(
                    ResolutionProfile.FromVideo(resolution.Width, resolution.Height, customBaseOptions).MaxLineWidth != resolution.Expected,
                    resolution.Name + " changes when the configured base line width changes");
            }

            Equal(60, ResolutionProfile.FromVideo(1920, 1080, customBaseOptions).MaxLineWidth, "custom base applies to 1920-wide screens");
            Equal(80, ResolutionProfile.FromVideo(2560, 1440, customBaseOptions).MaxLineWidth, "custom base rescales 2560-wide screens");
            Equal(120, ResolutionProfile.FromVideo(3840, 2160, customBaseOptions).MaxLineWidth, "custom base rescales 3840-wide screens");
            Equal(240, ResolutionProfile.FromVideo(7680, 4320, customBaseOptions).MaxLineWidth, "custom base rescales 7680-wide screens");
            Equal(60, ResolutionProfile.FromVideo(1080, 1920, customBaseOptions).MaxLineWidth, "portrait screen uses the changed base without scaling");

            Equal(80, ResolutionProfile.FromVideo(1080, 1920, options).MaxLineWidth, "portrait screen keeps the configured base without proportional adaptation");
            Equal(80, ResolutionProfile.FromVideo(720, 1280, options).MaxLineWidth, "smaller portrait screen also keeps the configured base");
            Equal(80, ResolutionProfile.FromVideo(0, 2160, options).MaxLineWidth, "missing screen width uses the 1920-wide base");
            Equal(17d, options.CommonFontSize, "common Fontsize defaults to 17");
            Equal("Source Han Sans SC", options.PrimaryFontName, "primary font defaults to Source Han Sans SC");
            Equal(70, options.SecondaryFontSizePercent, "secondary Fontsize ratio defaults to 70 percent");
            Equal(0, options.BilingualLineSpacing, "bilingual spacing defaults to zero");
            Equal(0.1d, options.PrimaryBorderWidth, "primary border width defaults to 0.1");
            Equal(0.1d, options.SecondaryBorderWidth, "secondary border width defaults to 0.1");
        }

        private static void TestCjkWidthAndWrapping()
        {
            Equal(8, TextLayout.Measure("测试ABcd"), "CJK visual width");
            var wrapped = TextLayout.WrapLine("这是一条非常非常长的中文字幕，需要自动换行。", 20);
            True(wrapped.Count >= 2, "long CJK line wraps");
            True(wrapped.All(line => TextLayout.Measure(line) <= 22), "wrapped CJK lines stay near limit");
        }

        private static void TestBilingualHasNoHorizontalScale()
        {
            var value = TextLayout.OptimizeAssText(
                "很短的中文\\NThis English subtitle line is considerably longer than the Chinese line",
                100);
            True(!value.Contains("\\fscx"), "bilingual text is not horizontally scaled");
        }

        private static void TestSpecialEffectsArePreserved()
        {
            const string input = "{\\pos(100,200)\\fs28\\bord2\\blur3\\3c&HFFFFFF&\\xshad1}这是一条不能被改写的超长定位字幕";
            const string expected = "{\\fnSource Han Sans SC\\fs17\\fsp0\\c&HFFFFFF&\\alpha&H00&\\b1\\i0\\3c&H000000&\\3a&H00&\\bord0.1}{\\pos(100,200)}这是一条不能被改写的超长定位字幕";
            var options = new PluginOptions();
            var profile = ResolutionProfile.FromVideo(1920, 1080, options);
            Equal(expected, SubtitleStyleFormatter.FormatAndOptimize(input, profile, options), "positioned ASS event keeps effects but inherits Style Fontsize");
            Equal(
                "{\\fs33}{\\i1}A{\\fsp1\\fscy90\\bord2}B",
                SubtitleStyleFormatter.NormalizeInlineFontSize("{\\fs+4\\i1}A{\\fsp1\\fscy90\\fs-2\\bord2}B", 33),
                "relative inline Fontsize is replaced by an effective leading fs tag without changing fsp or fscy");
            Equal(
                "{\\1c&H112233&\\fsp1}允许标签",
                SubtitleStyleFormatter.RemoveForbiddenInlineTags("{\\2c&HFFFFFF&\\3c&H000000&\\4c&H111111&\\blur2\\be1\\shad3\\xbord2\\bord1\\xshad4\\yshad-1\\fscx90\\fscy80\\1c&H112233&\\fsp1}允许标签"),
                "forbidden inline effects are removed while allowed tags remain");
        }

        private static void TestSingleAndBilingualStyles()
        {
            var options = new PluginOptions
            {
                PrimarySubtitleColor = "#FF0000",
                PrimaryFontName = "Source Han Sans SC",
                PrimaryFontStyle = SubtitleFontStyle.BoldItalic,
                PrimaryBorderEnabled = true,
                PrimaryBorderWidth = 1.5,
                PrimaryBorderColor = "#112233",
                SecondarySubtitleColor = "#8000FF00",
                SecondaryFontName = "Roboto",
                SecondaryFontStyle = SubtitleFontStyle.Italic,
                SecondaryBorderEnabled = true,
                SecondaryBorderWidth = 2.25,
                SecondaryBorderColor = "#80445566",
                PrimaryCharacterSpacing = 1.5,
                SecondaryCharacterSpacing = -0.5,
                CommonFontSize = 50,
                BilingualLineSpacing = 10
            };
            var profile = ResolutionProfile.FromVideo(1920, 1080, options);

            var single = SubtitleStyleFormatter.FormatAndOptimize("单行字幕", profile, options);
            True(single.StartsWith("{\\fnSource Han Sans SC\\fs50\\fsp1.5\\c&H0000FF&\\alpha&H00&\\b1\\i1\\3c&H332211&\\3a&H00&\\bord1.5}", StringComparison.Ordinal), "single subtitle uses common Fontsize and primary settings");

            var bilingual = SubtitleStyleFormatter.FormatAndOptimize(
                "{\\fnOriginalPrimary\\fs99\\fsp9\\1c&H123456&\\alpha&H80&\\b0\\i0}主中文字幕\\N{\\rEng\\fnMicrosoft YaHei\\fs88\\fsp8\\1c&HE8E8E8&\\alpha&H40&\\b1\\i0}A much longer secondary English subtitle",
                profile,
                options);
            True(bilingual.Contains("\\fnSource Han Sans SC\\fs50\\fsp1.5\\c&H0000FF&\\alpha&H00&\\b1\\i1"), "primary subtitle uses 100 percent of common Fontsize");
            True(bilingual.Contains("\\fnRoboto\\fs35\\fsp-0.5\\c&H00FF00&\\alpha&H7F&\\b0\\i1"), "secondary subtitle uses 70 percent of common Fontsize");
            True(bilingual.Contains("\\3c&H332211&\\3a&H00&\\bord1.5"), "primary subtitle uses its configured outer border");
            True(bilingual.Contains("\\3c&H665544&\\3a&H7F&\\bord2.25"), "secondary subtitle uses its configured outer border");
            True(bilingual.Contains("\\rEng\\fnRoboto\\fs35"), "configured secondary Fontsize is reapplied after the original Style reset");
            True(!bilingual.Contains("OriginalPrimary") && !bilingual.Contains("Microsoft YaHei"), "original inline fonts cannot override configured fonts");
            True(!bilingual.Contains("\\fs99") && !bilingual.Contains("\\fs88") && !bilingual.Contains("\\fsp9") && !bilingual.Contains("\\fsp8"), "original inline size and spacing cannot override configured values");
            True(!bilingual.Contains("&H123456&") && !bilingual.Contains("&HE8E8E8&") && !bilingual.Contains("\\alpha&H80&") && !bilingual.Contains("\\alpha&H40&"), "original inline colors cannot override configured colors");
            True(!bilingual.Contains("\\fscx"), "styled bilingual subtitle has no horizontal scale override");
            True(bilingual.Contains("{\\fs10}\\h{\\r}\\N"), "bilingual line gap uses Fontsize without ScaleY");
        }

        private static void TestSrtConversion()
        {
            const string srt = "1\n00:00:01,000 --> 00:00:03,250\n<i>这是一条很长很长的中文字幕，需要换行显示。</i>\n\n2\n00:00:04,000 --> 00:00:05,000\n主字幕\nSecondary subtitle";
            var options = new PluginOptions
            {
                MaxLineWidth1080P = 20,
                PrimaryFontName = "Arial",
                SecondaryFontName = "Helvetica",
                SrtDefaultFontName = "Verdana",
                CommonFontSize = 52,
                PrimaryCharacterSpacing = 1.25,
                SecondaryCharacterSpacing = -0.5,
                PositionMode = SubtitlePositionMode.BottomCenter,
                BottomDistance1080P = 90
            };
            var output = SrtSubtitleConverter.Convert(srt, ResolutionProfile.FromVideo(1920, 1080, options), options, "test-marker");
            True(output.Contains("PlayResX: 1920"), "SRT output uses video resolution");
            True(output.Contains("Style: ESO,Verdana,52"), "SRT ASS style uses selected font and common Fontsize");
            True(output.Contains("Format: Name, Fontname, Fontsize, PrimaryColour, OutlineColour, Bold, Italic, Underline, StrikeOut, Spacing, Angle, BorderStyle, Outline, Alignment, MarginL, MarginR, MarginV, Encoding"), "SRT ASS style includes the default border fields");
            True(output.Contains("Style: ESO,Verdana,52,&H00FFFFFF,&H00000000,0,0,0,0,1.25,0,1,0.1,2,"), "SRT ASS style uses a black border with width 0.1");
            True(output.Contains(",96,96,90,1"), "SRT bottom-center mode uses the configured bottom distance");
            True(!output.Contains("ScaledBorderAndShadow"), "SRT ASS omits ScaledBorderAndShadow");
            True(output.Contains("{\\fnVerdana\\fs52\\fsp1.25"), "single SRT cue uses common Fontsize");
            True(output.Contains("{\\fnArial\\fs52\\fsp1.25"), "bilingual SRT primary line uses common Fontsize");
            True(output.Contains("{\\fnHelvetica\\fs36.4\\fsp-0.5"), "bilingual SRT secondary line uses its percentage of common Fontsize");
            Equal(2, output.Split('\n').Count(line => line.StartsWith("Dialogue:", StringComparison.Ordinal)), "all SRT cues convert");
            True(!output.Contains("{\\i1}"), "SRT italic markup cannot override the configured font style");
            True(output.Contains("\\N"), "long SRT cue wraps");
        }

        private static void TestAssEventWithCommas()
        {
            const string ass = "[Script Info]\nScriptType: v4.00+\nPlayResY: 288\n\n[V4+ Styles]\nFormat: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\nStyle: Default,Arial,33,&H00FFFFFF,&H000000FF,&H00FFFFFF,&H00000000,0,0,0,0,100,100,0,0,3,2,0,2,10,10,10,1\n\n[Events]\nFormat: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\nDialogue: 0,0:00:01.00,0:00:03.00,Default,,0,0,0,,{\\bord2\\blur1}这是带有逗号,而且非常长的字幕\\N{\\fs22\\fnOriginalEnglish\\fsp9\\1c&H123456&\\alpha&H80&\\b1\\i1\\4c&HFFFFFF&\\yshad2}English subtitle";
            var options = new PluginOptions { MaxLineWidth1080P = 20, CommonFontSize = 0, BilingualLineSpacing = 0 };
            var output = AssSubtitleOptimizer.Optimize(ass, ResolutionProfile.FromVideo(1920, 1080, options), options, "test-marker");
            True(output.Contains("逗号,"), "comma in ASS text is preserved");
            True(output.Contains("Style: Default,Arial,33,"), "ASS Style Fontsize is preserved");
            True(!output.Contains("SecondaryColour") && !output.Contains("BackColour"), "unused ASS color effect fields are removed");
            True(!output.Contains("ScaleX") && !output.Contains("ScaleY"), "ASS scale fields are removed");
            True(!output.Contains("Shadow"), "ASS shadow field is removed");
            var styleFormat = output.Split('\n').First(line => line.StartsWith("Format: Name, Fontname", StringComparison.Ordinal));
            var styleFields = styleFormat.Substring(7).Split(',').Select(value => value.Trim()).ToArray();
            var styleLine = output.Split('\n').Single(line => line.StartsWith("Style: Default,", StringComparison.Ordinal));
            var styleValues = styleLine.Substring(7).Split(',');
            Equal("&H00000000", styleValues[Array.IndexOf(styleFields, "OutlineColour")], "ASS border color is forced to black");
            Equal("1", styleValues[Array.IndexOf(styleFields, "BorderStyle")], "ASS border style is forced to normal outline");
            Equal("0.1", styleValues[Array.IndexOf(styleFields, "Outline")], "ASS fallback border width is forced to 0.1");
            True(!output.Contains("{\\fs22}English subtitle"), "original inline ASS Fontsize is replaced");
            True(output.Contains("\\fs33"), "primary subtitle uses 100 percent of Style Fontsize");
            True(output.Contains("\\fs23.1"), "secondary subtitle uses 70 percent of Style Fontsize");
            Equal(2, Regex.Matches(output, @"\\fs(?![pc])(?:\d|\.)").Count, "bilingual lines have independent fs tags");
            True(!Regex.IsMatch(output, @"\\(?:2c|4c|blur|be|shad|xbord|xshad|yshad|fscx|fscy)(?![a-z])", RegexOptions.IgnoreCase), "forbidden inline tags are absent from optimized ASS");
            True(!output.Contains("\\bord2") && !output.Contains("\\3c&HFFFFFF&"), "source border overrides are replaced by configured border settings");
            True(!output.Contains("OriginalEnglish") && !output.Contains("\\fsp9") && !output.Contains("&H123456&") && !output.Contains("\\alpha&H80&"), "original inline ASS style cannot override plugin settings");
            True(output.Contains("\\N"), "ASS dialogue wraps");
            True(output.Contains("test-marker"), "generation marker is added");

            var ssaOutput = AssSubtitleOptimizer.Optimize(
                ass.Replace("[V4+ Styles]", "[V4 Styles]").Replace("OutlineColour", "TertiaryColour"),
                ResolutionProfile.FromVideo(1920, 1080, options),
                options,
                "ssa-border-test");
            var ssaFormat = ssaOutput.Split('\n').First(line => line.StartsWith("Format: Name, Fontname", StringComparison.Ordinal));
            var ssaFields = ssaFormat.Substring(7).Split(',').Select(value => value.Trim()).ToArray();
            var ssaStyle = ssaOutput.Split('\n').Single(line => line.StartsWith("Style: Default,", StringComparison.Ordinal));
            var ssaValues = ssaStyle.Substring(7).Split(',');
            Equal("&H00000000", ssaValues[Array.IndexOf(ssaFields, "TertiaryColour")], "SSA border color is forced to black");
            Equal("0.1", ssaValues[Array.IndexOf(ssaFields, "Outline")], "SSA fallback border width is forced to 0.1");
        }

        private static void TestSubtitlePositionModes()
        {
            const string ass = "[Script Info]\nScriptType: v4.00+\nPlayResX: 384\nPlayResY: 288\n\n[V4+ Styles]\nFormat: Name, Fontname, Fontsize, PrimaryColour, Bold, Italic, Underline, StrikeOut, Spacing, Angle, Alignment, MarginL, MarginR, MarginV, Encoding\nStyle: Default,Arial,20,&H00FFFFFF,0,0,0,0,0,0,7,10,20,30,1\n\n[Events]\nFormat: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\nDialogue: 0,0:00:01.00,0:00:03.00,Default,,11,22,33,,{\\an7\\pos(100,80)}定位字幕";
            var profile = ResolutionProfile.FromVideo(3840, 2160, new PluginOptions());

            var preserved = AssSubtitleOptimizer.Optimize(ass, profile, new PluginOptions(), "preserve-position");
            True(preserved.Contains("{\\fnSource Han Sans SC\\fs17\\fsp0\\c&HFFFFFF&\\alpha&H00&\\b1\\i0\\3c&H000000&\\3a&H00&\\bord0.1}{\\an7\\pos(100,80)}定位字幕"), "default position mode applies configured style while preserving inline alignment and position");
            True(preserved.Contains(",Default,,11,22,33,,"), "default position mode preserves Dialogue margins");
            True(preserved.Contains(",7,10,20,30,1"), "default position mode preserves Style alignment and margins");

            var bottomOptions = new PluginOptions
            {
                PositionMode = SubtitlePositionMode.BottomCenter,
                BottomDistance1080P = 60
            };
            var bottom = AssSubtitleOptimizer.Optimize(ass, profile, bottomOptions, "bottom-center");
            True(bottom.Contains("{\\an2\\pos(192,272)}"), "bottom-center mode scales the configured distance to the ASS canvas");
            True(!bottom.Contains("\\an7") && !bottom.Contains("\\pos(100,80)"), "bottom-center mode replaces original inline positioning");

            const string srt = "1\n00:00:01,000 --> 00:00:02,000\n测试字幕";
            var assWithoutScriptResolution = ass
                .Replace("PlayResX: 384\n", string.Empty)
                .Replace("PlayResY: 288\n", string.Empty);
            var resolutionCases = new[]
            {
                (Name: "VGA", Width: 640, Height: 480, ExpectedDistance: 27),
                (Name: "SD", Width: 720, Height: 480, ExpectedDistance: 27),
                (Name: "SVGA", Width: 800, Height: 600, ExpectedDistance: 33),
                (Name: "XGA", Width: 1024, Height: 768, ExpectedDistance: 43),
                (Name: "SXGA", Width: 1280, Height: 1024, ExpectedDistance: 57),
                (Name: "HD", Width: 1280, Height: 720, ExpectedDistance: 40),
                (Name: "WXGA 16:10", Width: 1280, Height: 800, ExpectedDistance: 44),
                (Name: "WXGA 16:9", Width: 1366, Height: 768, ExpectedDistance: 43),
                (Name: "SXGA+", Width: 1400, Height: 1050, ExpectedDistance: 58),
                (Name: "UXGA", Width: 1600, Height: 1200, ExpectedDistance: 67),
                (Name: "FHD", Width: 1920, Height: 1080, ExpectedDistance: 60),
                (Name: "WUXGA", Width: 1920, Height: 1200, ExpectedDistance: 67),
                (Name: "UW-FHD", Width: 2560, Height: 1080, ExpectedDistance: 60),
                (Name: "QHD", Width: 2560, Height: 1440, ExpectedDistance: 80),
                (Name: "UW-QHD", Width: 3440, Height: 1440, ExpectedDistance: 80),
                (Name: "DFHD", Width: 3840, Height: 1080, ExpectedDistance: 60),
                (Name: "UHD 4K", Width: 3840, Height: 2160, ExpectedDistance: 120),
                (Name: "DCI 4K", Width: 4096, Height: 2160, ExpectedDistance: 120),
                (Name: "DQHD", Width: 5120, Height: 1440, ExpectedDistance: 80),
                (Name: "8K UHD", Width: 7680, Height: 4320, ExpectedDistance: 240)
            };
            foreach (var resolution in resolutionCases)
            {
                var resolutionProfile = ResolutionProfile.FromVideo(resolution.Width, resolution.Height, bottomOptions);
                Equal(
                    resolution.ExpectedDistance,
                    resolutionProfile.ScaleVerticalFrom1080(bottomOptions.BottomDistance1080P),
                    resolution.Name + " bottom distance scales from the actual screen height");
                var converted = SrtSubtitleConverter.Convert(srt, resolutionProfile, bottomOptions, "position-test");
                var srtStyleFormat = converted.Split('\n').Single(line => line.StartsWith("Format: Name, Fontname", StringComparison.Ordinal));
                var srtStyleFields = srtStyleFormat.Substring(7).Split(',').Select(value => value.Trim()).ToArray();
                var styleLine = converted.Split('\n').Single(line => line.StartsWith("Style: ESO,", StringComparison.Ordinal));
                var styleValues = styleLine.Substring(7).Split(',');
                Equal(
                    resolution.ExpectedDistance.ToString(),
                    styleValues[Array.IndexOf(srtStyleFields, "MarginV")],
                    resolution.Name + " SRT MarginV uses the scaled bottom distance");
                var optimizedAss = AssSubtitleOptimizer.Optimize(
                    assWithoutScriptResolution,
                    resolutionProfile,
                    bottomOptions,
                    "position-test");
                True(
                    optimizedAss.Contains(
                        "{\\an2\\pos(" + (resolution.Width / 2) + "," + (resolution.Height - resolution.ExpectedDistance) + ")}"),
                    resolution.Name + " ASS position uses the scaled bottom distance");
            }
        }

        private static void TestFileProcessingIsIncremental()
        {
            var directory = Path.Combine(Path.GetTempPath(), "eso-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var source = Path.Combine(directory, "movie.zh.srt");
                var target = Path.Combine(directory, "movie.zh.optimized.ass");
                File.WriteAllText(source, "1\n00:00:01,000 --> 00:00:02,000\n测试字幕");

                var processor = new SubtitleFileProcessor();
                var options = new PluginOptions();
                True(processor.Process(source, target, 3840, 2160, options).Changed, "first file processing writes output");
                True(File.ReadAllText(target).Contains("revision=16"), "file marker records processing revision");
                True(File.ReadAllText(target).Contains("profile=4K"), "file marker records resolution profile");
                True(!processor.Process(source, target, 3840, 2160, options).Changed, "unchanged file is skipped");

                True(processor.Process(source, target, 3840, 1080, options).Changed, "video height change regenerates the output even when width and profile name are unchanged");
                True(!processor.Process(source, target, 3840, 1080, options).Changed, "unchanged dimensions are skipped after height-based regeneration");

                options.MaxLineWidth1080P++;
                True(processor.Process(source, target, 3840, 1080, options).Changed, "settings change regenerates output");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static void TestLibrarySubtitleScanner()
        {
            var root = Path.Combine(Path.GetTempPath(), "eso-scan-tests-" + Guid.NewGuid().ToString("N"));
            var nested = Path.Combine(root, "Season 01");
            Directory.CreateDirectory(nested);
            try
            {
                var ass = Path.Combine(root, "movie.zh.ass");
                var ssa = Path.Combine(nested, "episode.zh.ssa");
                var srt = Path.Combine(nested, "episode.en.srt");
                var generatedBySuffix = Path.Combine(root, "movie.zh.optimized.ass");
                var generatedByMarker = Path.Combine(root, "legacy-output.ass");
                File.WriteAllText(ass, "[Script Info]\nTitle: source");
                File.WriteAllText(ssa, "[Script Info]\nTitle: source");
                File.WriteAllText(srt, "1\n00:00:01,000 --> 00:00:02,000\nSubtitle");
                File.WriteAllText(generatedBySuffix, "[Script Info]\nTitle: output");
                File.WriteAllText(generatedByMarker, "; ESO revision=10\n[Script Info]");
                File.WriteAllText(Path.Combine(nested, "notes.txt"), "not a subtitle");

                var all = LibrarySubtitleScanner.Find(
                    new[] { root }, "optimized", true, true, CancellationToken.None);
                True(all.Contains(ass), "library scan finds ASS directly below a configured root");
                True(all.Contains(ssa), "library scan recursively finds SSA in a nested folder");
                True(all.Contains(srt), "library scan recursively finds SRT in a nested folder");
                True(!all.Contains(generatedBySuffix), "library scan skips the configured generated output suffix");
                True(!all.Contains(generatedByMarker), "library scan skips plugin-generated ASS output");

                var assOnly = LibrarySubtitleScanner.Find(
                    new[] { root }, "optimized", true, false, CancellationToken.None);
                True(assOnly.Contains(ass) && assOnly.Contains(ssa), "ASS scan includes ASS and SSA");
                True(!assOnly.Contains(srt), "disabled SRT input is excluded from the scan");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static void Equal<T>(T expected, T actual, string name)
        {
            if (!Equals(expected, actual))
            {
                failures++;
                Console.Error.WriteLine("FAIL " + name + ": expected=" + expected + ", actual=" + actual);
            }
        }

        private static void True(bool value, string name)
        {
            if (!value)
            {
                failures++;
                Console.Error.WriteLine("FAIL " + name);
            }
        }

    }
}
