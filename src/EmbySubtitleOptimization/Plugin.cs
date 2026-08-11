using System;
using MediaBrowser.Common;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
using EmbySubtitleOptimization.Fonts;

namespace EmbySubtitleOptimization
{
    /// <summary>Emby plugin entry point.</summary>
    public sealed class Plugin : BasePluginSimpleUI<PluginOptions>
    {
        private static readonly Guid PluginId = new Guid("8566851e-d044-4c96-bf14-bfae70bb5ea8");
        public Plugin(IApplicationHost applicationHost, ILogManager logManager)
            : base(applicationHost)
        {
            Instance = this;
            Logger = logManager.GetLogger(Name);
            Logger.Info("{0} loaded", Name);
        }

        public static Plugin Instance { get; private set; }

        internal ILogger Logger { get; }

        public override string Name => "Emby Subtitle Optimization";

        public override string Description => "按视频分辨率优化 ASS、SSA 和 SRT 外置字幕的行宽与双语排版。";

        public override Guid Id => PluginId;

        public PluginOptions Options => PrepareFontSettings(GetOptions());

        protected override PluginOptions OnBeforeShowUI(PluginOptions options)
        {
            return PrepareFontSettings(options);
        }

        protected override void OnOptionsSaved(PluginOptions options)
        {
            PrepareFontSettings(options);
            Logger.Info("{0} settings updated", Name);
        }

        private PluginOptions PrepareFontSettings(PluginOptions options)
        {
            MigrateFontSettings(options);
            options.PrimaryFontName = GenericFontCatalog.Resolve(options.PrimaryFontName);
            options.SecondaryFontName = GenericFontCatalog.Resolve(options.SecondaryFontName);
            options.SrtDefaultFontName = GenericFontCatalog.Resolve(options.SrtDefaultFontName);
            options.AvailableFonts = GenericFontCatalog.CreateOptions();
            return options;
        }

        private static void MigrateFontSettings(PluginOptions options)
        {
            var hasLegacyFont = !string.IsNullOrWhiteSpace(options.FontName);
            var fallbackFont = hasLegacyFont ? options.FontName.Trim() : "Arial";
            if (string.IsNullOrWhiteSpace(options.PrimaryFontName) || hasLegacyFont && options.PrimaryFontName == "Arial")
            {
                options.PrimaryFontName = fallbackFont;
            }

            if (string.IsNullOrWhiteSpace(options.SecondaryFontName) || hasLegacyFont && options.SecondaryFontName == "Arial")
            {
                options.SecondaryFontName = fallbackFont;
            }

            if (string.IsNullOrWhiteSpace(options.SrtDefaultFontName))
            {
                options.SrtDefaultFontName = options.PrimaryFontName;
            }

            if (options.SettingsSchemaVersion < 1 && options.BilingualLineSpacing == 8)
            {
                options.BilingualLineSpacing = 2;
            }

            if (options.SettingsSchemaVersion < 2 && options.CommonFontSize == 0)
            {
                options.CommonFontSize = 20;
            }

            if (options.SettingsSchemaVersion < 3)
            {
                if (options.BilingualLineSpacing == 2)
                {
                    options.BilingualLineSpacing = 0;
                }

                if (string.Equals(options.PrimaryFontName, "Arial", StringComparison.OrdinalIgnoreCase))
                {
                    options.PrimaryFontName = "Source Han Sans SC";
                }

                if (options.SecondaryFontSizePercent == 85)
                {
                    options.SecondaryFontSizePercent = 70;
                }

                if (options.CommonFontSize == 20)
                {
                    options.CommonFontSize = 17;
                }
            }

            options.FontName = null;
            options.SettingsSchemaVersion = 3;
        }
    }
}
