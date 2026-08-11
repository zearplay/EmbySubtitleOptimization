using System;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Branding;
using MediaBrowser.Model.Logging;

namespace EmbySubtitleOptimization
{
    internal sealed class WebSubtitleCssManager
    {
        private const string BrandingConfigurationKey = "branding";
        private const string StartMarker = "/* EmbySubtitleOptimization:fullscreen-canvas:start */";
        private const string EndMarker = "/* EmbySubtitleOptimization:fullscreen-canvas:end */";
        private const string ManagedCss = StartMarker + "\n"
            + ".htmlvideo-subtitles-canvas-parent .htmlvideo-subtitles-canvas {\n"
            + "  width: 100% !important;\n"
            + "  height: 100% !important;\n"
            + "  top: 0 !important;\n"
            + "  left: 0 !important;\n"
            + "  inset-inline-start: 0 !important;\n"
            + "  object-fit: contain !important;\n"
            + "  object-position: center center !important;\n"
            + "}\n"
            + EndMarker;

        private readonly IConfigurationManager configurationManager;
        private readonly ILogger logger;

        public WebSubtitleCssManager(IConfigurationManager configurationManager, ILogger logger)
        {
            this.configurationManager = configurationManager;
            this.logger = logger;
        }

        public void Apply(bool enabled)
        {
            try
            {
                var branding = configurationManager.GetConfiguration(BrandingConfigurationKey) as BrandingOptions;
                if (branding == null)
                {
                    logger.Info("{0}", "Unable to load Emby branding configuration; fullscreen subtitle canvas fix was not applied");
                    return;
                }

                var original = branding.CustomCss ?? string.Empty;
                var unmanaged = RemoveManagedCss(original);
                var updated = enabled
                    ? AppendManagedCss(unmanaged)
                    : unmanaged;
                if (string.Equals(original, updated, StringComparison.Ordinal))
                {
                    return;
                }

                branding.CustomCss = updated;
                configurationManager.SaveConfiguration(BrandingConfigurationKey, branding);
                logger.Info("Web fullscreen subtitle canvas fix {0}", enabled ? "enabled" : "disabled");
            }
            catch (Exception exception)
            {
                logger.ErrorException("Unable to update Emby branding CSS for fullscreen subtitles", exception);
            }
        }

        internal static string UpdateCss(string customCss, bool enabled)
        {
            var unmanaged = RemoveManagedCss(customCss ?? string.Empty);
            return enabled ? AppendManagedCss(unmanaged) : unmanaged;
        }

        private static string AppendManagedCss(string customCss)
        {
            var trimmed = customCss.TrimEnd();
            return trimmed.Length == 0
                ? ManagedCss
                : trimmed + "\n\n" + ManagedCss;
        }

        private static string RemoveManagedCss(string customCss)
        {
            var result = customCss;
            while (true)
            {
                var start = result.IndexOf(StartMarker, StringComparison.Ordinal);
                if (start < 0)
                {
                    return result.TrimEnd();
                }

                var end = result.IndexOf(EndMarker, start + StartMarker.Length, StringComparison.Ordinal);
                if (end < 0)
                {
                    return result.Substring(0, start).TrimEnd();
                }

                result = result.Remove(start, end + EndMarker.Length - start);
            }
        }
    }
}
