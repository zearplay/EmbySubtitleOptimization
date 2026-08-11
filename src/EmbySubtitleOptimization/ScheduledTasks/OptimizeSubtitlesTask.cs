using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EmbySubtitleOptimization.Subtitles;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;

namespace EmbySubtitleOptimization.ScheduledTasks
{
    /// <summary>Scans local video folders and generates optimized ASS subtitle copies.</summary>
    public sealed class OptimizeSubtitlesTask : IScheduledTask
    {
        private static readonly string[] SubtitleExtensions = { ".ass", ".ssa", ".srt" };
        private readonly ILibraryManager libraryManager;
        private readonly ILogger logger;
        private readonly SubtitleFileProcessor processor = new SubtitleFileProcessor();

        public OptimizeSubtitlesTask(ILibraryManager libraryManager, ILogManager logManager)
        {
            this.libraryManager = libraryManager;
            logger = logManager.GetLogger(Plugin.Instance?.Name ?? "Emby Subtitle Optimization");
        }

        public string Name => "优化 ASS/SRT 字幕";
        public string Key => "EmbySubtitleOptimization";
        public string Description => "扫描电影和剧集目录，为外置 ASS、SSA、SRT 字幕生成分辨率自适应的 ASS 副本。";
        public string Category => "字幕";

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return Array.Empty<TaskTriggerInfo>();
        }

        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var options = Plugin.Instance?.Options ?? new PluginOptions();
            var items = libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { "Movie", "Episode" }
            });

            var changed = 0;
            var failed = 0;
            for (var index = 0; index < items.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    changed += ProcessItem(items[index], options);
                }
                catch (Exception exception)
                {
                    failed++;
                    logger.ErrorException("Unable to optimize subtitles for {0}", exception, items[index].Path);
                }

                progress.Report(items.Length == 0 ? 100 : (index + 1) * 100.0 / items.Length);
            }

            logger.Info("Subtitle optimization completed: {0} files generated, {1} items failed", changed, failed);
            return Task.CompletedTask;
        }

        private int ProcessItem(BaseItem item, PluginOptions options)
        {
            if (string.IsNullOrWhiteSpace(item.Path) || !item.IsFileProtocol)
            {
                return 0;
            }

            var directory = Path.GetDirectoryName(item.Path);
            var videoBaseName = Path.GetFileNameWithoutExtension(item.Path);
            if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(videoBaseName) || !Directory.Exists(directory))
            {
                return 0;
            }

            var outputToken = "." + options.OutputSuffix + ".ass";
            var sources = Directory.EnumerateFiles(directory, videoBaseName + ".*")
                .Where(path => SubtitleExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .Where(path => !path.EndsWith(outputToken, StringComparison.OrdinalIgnoreCase))
                .Where(path => IsEnabled(path, options))
                .OrderBy(path => SubtitlePriority(Path.GetExtension(path)))
                .ToArray();

            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var changed = 0;
            foreach (var source in sources)
            {
                var target = Path.Combine(directory, Path.GetFileNameWithoutExtension(source) + "." + options.OutputSuffix + ".ass");
                if (!targets.Add(target))
                {
                    logger.Warn("Skipping subtitle because another source maps to the same output: {0}", source);
                    continue;
                }

                var result = processor.Process(source, target, item.Width, item.Height, options);
                if (result.Changed)
                {
                    changed++;
                    logger.Info("Generated {0} subtitle: {1}", result.ProfileName, target);
                }
            }

            return changed;
        }

        private static bool IsEnabled(string path, PluginOptions options)
        {
            var extension = Path.GetExtension(path);
            return extension.Equals(".srt", StringComparison.OrdinalIgnoreCase) ? options.EnableSrt : options.EnableAss;
        }

        private static int SubtitlePriority(string extension)
        {
            if (extension.Equals(".ass", StringComparison.OrdinalIgnoreCase)) return 0;
            if (extension.Equals(".ssa", StringComparison.OrdinalIgnoreCase)) return 1;
            return 2;
        }
    }
}

