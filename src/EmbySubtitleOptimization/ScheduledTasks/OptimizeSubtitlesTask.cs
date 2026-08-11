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
    /// <summary>Scans configured library folders and generates optimized ASS subtitle copies.</summary>
    public sealed class OptimizeSubtitlesTask : IScheduledTask
    {
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
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerStartup
                },
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerInterval,
                    IntervalTicks = TimeSpan.FromMinutes(15).Ticks
                }
            };
        }

        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var options = Plugin.Instance?.Options ?? new PluginOptions();
            var items = libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { "Movie", "Episode" }
            });
            var itemsByDirectory = IndexMediaItems(items);
            var roots = GetLibraryRoots(items);
            logger.Info("Scanning {0} configured library location(s) for subtitle files", roots.Count);
            var sources = LibrarySubtitleScanner.Find(
                roots,
                options.OutputSuffix,
                options.EnableAss,
                options.EnableSrt,
                cancellationToken,
                (path, exception) => logger.Warn("Unable to scan subtitle path {0}: {1}", path, exception.Message));

            var changed = 0;
            var failed = 0;
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < sources.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var source = sources[index];
                try
                {
                    var target = Path.Combine(
                        Path.GetDirectoryName(source),
                        Path.GetFileNameWithoutExtension(source) + "." + options.OutputSuffix + ".ass");
                    if (!targets.Add(target))
                    {
                        logger.Warn("Skipping subtitle because another source maps to the same output: {0}", source);
                        continue;
                    }

                    var mediaItem = FindMatchingMediaItem(source, itemsByDirectory);
                    var result = processor.Process(
                        source,
                        target,
                        mediaItem?.Width ?? 1920,
                        mediaItem?.Height ?? 1080,
                        options);
                    if (result.Changed)
                    {
                        changed++;
                        logger.Info("Generated {0} subtitle: {1}", result.ProfileName, target);
                    }
                }
                catch (Exception exception)
                {
                    failed++;
                    logger.ErrorException("Unable to optimize subtitle {0}", exception, source);
                }

                progress.Report(sources.Count == 0 ? 100 : (index + 1) * 100.0 / sources.Count);
            }

            if (sources.Count == 0) progress.Report(100);

            logger.Info("Subtitle optimization completed: {0} files generated, {1} files failed, {2} library subtitle files found", changed, failed, sources.Count);
            return Task.CompletedTask;
        }

        private IReadOnlyCollection<string> GetLibraryRoots(IEnumerable<BaseItem> items)
        {
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var virtualFolders = libraryManager.GetVirtualFolders();
            if (virtualFolders != null)
            {
                foreach (var folder in virtualFolders)
                {
                    foreach (var location in folder.Locations ?? Array.Empty<string>())
                    {
                        if (!string.IsNullOrWhiteSpace(location)) roots.Add(location);
                    }
                }
            }

            if (roots.Count == 0)
            {
                foreach (var item in items)
                {
                    if (string.IsNullOrWhiteSpace(item.Path)) continue;
                    var directory = Path.GetDirectoryName(item.Path);
                    if (!string.IsNullOrWhiteSpace(directory)) roots.Add(directory);
                }
            }

            return roots;
        }

        private static IReadOnlyDictionary<string, List<BaseItem>> IndexMediaItems(IEnumerable<BaseItem> items)
        {
            var result = new Dictionary<string, List<BaseItem>>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Path)) continue;
                var directory = Path.GetDirectoryName(item.Path);
                if (string.IsNullOrWhiteSpace(directory)) continue;
                if (!result.TryGetValue(directory, out var directoryItems))
                {
                    directoryItems = new List<BaseItem>();
                    result[directory] = directoryItems;
                }

                directoryItems.Add(item);
            }

            return result;
        }

        private static BaseItem FindMatchingMediaItem(string subtitlePath, IReadOnlyDictionary<string, List<BaseItem>> itemsByDirectory)
        {
            var directory = Path.GetDirectoryName(subtitlePath);
            if (string.IsNullOrWhiteSpace(directory) || !itemsByDirectory.TryGetValue(directory, out var candidates) || candidates.Count == 0)
            {
                return null;
            }

            var subtitleBaseName = Path.GetFileNameWithoutExtension(subtitlePath);
            var matched = candidates
                .Select(item => new { Item = item, BaseName = Path.GetFileNameWithoutExtension(item.Path) })
                .Where(value => subtitleBaseName.Equals(value.BaseName, StringComparison.OrdinalIgnoreCase)
                                || subtitleBaseName.StartsWith(value.BaseName + ".", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(value => value.BaseName.Length)
                .Select(value => value.Item)
                .FirstOrDefault();
            return matched ?? (candidates.Count == 1 ? candidates[0] : null);
        }
    }
}
