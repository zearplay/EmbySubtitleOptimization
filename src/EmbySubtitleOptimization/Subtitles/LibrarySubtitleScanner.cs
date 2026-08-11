using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace EmbySubtitleOptimization.Subtitles
{
    /// <summary>Recursively finds source subtitles below configured library locations.</summary>
    internal static class LibrarySubtitleScanner
    {
        private static readonly string[] SubtitleExtensions = { ".ass", ".ssa", ".srt" };

        public static IReadOnlyList<string> Find(
            IEnumerable<string> roots,
            string outputSuffix,
            bool enableAss,
            bool enableSrt,
            CancellationToken cancellationToken,
            Action<string, Exception> onError = null)
        {
            var outputToken = "." + outputSuffix + ".ass";
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var root in (roots ?? Array.Empty<string>()).Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var path in EnumerateFiles(root, cancellationToken, onError))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var extension = Path.GetExtension(path);
                    if (!SubtitleExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) continue;
                    if (path.EndsWith(outputToken, StringComparison.OrdinalIgnoreCase)) continue;
                    if (extension.Equals(".srt", StringComparison.OrdinalIgnoreCase) ? !enableSrt : !enableAss) continue;
                    if (HasGenerationMarker(path, onError)) continue;
                    result.Add(path);
                }
            }

            return result
                .OrderBy(Path.GetDirectoryName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
                .ThenBy(path => SubtitlePriority(Path.GetExtension(path)))
                .ToArray();
        }

        private static IEnumerable<string> EnumerateFiles(string root, CancellationToken cancellationToken, Action<string, Exception> onError)
        {
            if (!Directory.Exists(root))
            {
                onError?.Invoke(root, new DirectoryNotFoundException("Configured library location does not exist or is not accessible."));
                yield break;
            }

            var pending = new Stack<string>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            pending.Push(root);

            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = pending.Pop();
                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(directory);
                }
                catch (Exception exception)
                {
                    onError?.Invoke(directory, exception);
                    continue;
                }

                if (!visited.Add(fullPath)) continue;

                string[] files;
                try
                {
                    files = Directory.GetFiles(fullPath);
                }
                catch (Exception exception)
                {
                    onError?.Invoke(fullPath, exception);
                    continue;
                }

                foreach (var file in files)
                {
                    yield return file;
                }

                string[] directories;
                try
                {
                    directories = Directory.GetDirectories(fullPath);
                }
                catch (Exception exception)
                {
                    onError?.Invoke(fullPath, exception);
                    continue;
                }

                foreach (var child in directories)
                {
                    try
                    {
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
                        pending.Push(child);
                    }
                    catch (Exception exception)
                    {
                        onError?.Invoke(child, exception);
                    }
                }
            }
        }

        private static int SubtitlePriority(string extension)
        {
            if (extension.Equals(".ass", StringComparison.OrdinalIgnoreCase)) return 0;
            if (extension.Equals(".ssa", StringComparison.OrdinalIgnoreCase)) return 1;
            return 2;
        }

        private static bool HasGenerationMarker(string path, Action<string, Exception> onError)
        {
            try
            {
                using (var reader = new StreamReader(path, true))
                {
                    var buffer = new char[2048];
                    var count = reader.Read(buffer, 0, buffer.Length);
                    return new string(buffer, 0, count).IndexOf("ESO revision=", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch (Exception exception)
            {
                onError?.Invoke(path, exception);
                return true;
            }
        }
    }
}
