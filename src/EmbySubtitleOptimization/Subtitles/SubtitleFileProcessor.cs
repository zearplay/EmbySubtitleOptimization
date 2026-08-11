using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace EmbySubtitleOptimization.Subtitles
{
    internal sealed class SubtitleFileProcessor
    {
        private const int ProcessingRevision = 21;

        public ProcessResult Process(string sourcePath, string targetPath, int videoWidth, int videoHeight, PluginOptions options)
        {
            var sourceInfo = new FileInfo(sourcePath);
            var profile = ResolutionProfile.FromVideo(videoWidth, videoHeight, options);
            var marker = BuildMarker(sourceInfo, profile, options);

            if (IsCurrent(targetPath, marker))
            {
                return ProcessResult.Unchanged;
            }

            var content = ReadText(sourcePath);
            var extension = Path.GetExtension(sourcePath);
            string optimized;
            if (extension.Equals(".srt", StringComparison.OrdinalIgnoreCase))
            {
                optimized = SrtSubtitleConverter.Convert(content, profile, options, marker);
            }
            else
            {
                optimized = AssSubtitleOptimizer.Optimize(content, profile, options, marker);
            }

            WriteAtomically(targetPath, optimized);
            return new ProcessResult(true, profile.Name);
        }

        private static string BuildMarker(FileInfo source, ResolutionProfile profile, PluginOptions options)
        {
            var settings = string.Join("|", new object[]
            {
                ProcessingRevision, profile.Name, profile.Width, profile.Height, profile.MaxLineWidth, options.CommonFontSize,
                options.PrimaryFontName, options.SecondaryFontName, options.SrtDefaultFontName,
                options.PrimaryFontSizePercent, options.SecondaryFontSizePercent,
                options.PrimarySubtitleColor, options.SecondarySubtitleColor,
                options.PrimaryFontStyle, options.SecondaryFontStyle,
                options.PrimaryBorderEnabled, options.SecondaryBorderEnabled,
                options.PrimaryBorderWidth, options.SecondaryBorderWidth,
                options.PrimaryBorderColor, options.SecondaryBorderColor,
                options.PrimaryCharacterSpacing, options.SecondaryCharacterSpacing,
                options.BilingualLineSpacing, options.PositionMode, options.BottomDistance1080P
            });

            using (var sha = SHA256.Create())
            {
                var digest = sha.ComputeHash(Encoding.UTF8.GetBytes(settings));
                var hash = BitConverter.ToString(digest, 0, 6).Replace("-", string.Empty);
                return "ESO revision=" + ProcessingRevision + "; source=" + source.LastWriteTimeUtc.Ticks + "; profile=" + profile.Name + "; settings=" + hash;
            }
        }

        private static bool IsCurrent(string targetPath, string marker)
        {
            if (!File.Exists(targetPath))
            {
                return false;
            }

            using (var reader = new StreamReader(targetPath, Encoding.UTF8, true, 1024))
            {
                var buffer = new char[2048];
                var count = reader.Read(buffer, 0, buffer.Length);
                return new string(buffer, 0, count).Contains(marker);
            }
        }

        private static string ReadText(string path)
        {
            using (var reader = new StreamReader(path, Encoding.UTF8, true))
            {
                return reader.ReadToEnd();
            }
        }

        private static void WriteAtomically(string targetPath, string content)
        {
            var temporaryPath = targetPath + ".tmp";
            using (var writer = new StreamWriter(temporaryPath, false, new UTF8Encoding(true)))
            {
                writer.Write(content);
            }

            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            File.Move(temporaryPath, targetPath);
        }
    }

    internal sealed class ProcessResult
    {
        public static readonly ProcessResult Unchanged = new ProcessResult(false, null);

        public ProcessResult(bool changed, string profileName)
        {
            Changed = changed;
            ProfileName = profileName;
        }

        public bool Changed { get; }
        public string ProfileName { get; }
    }
}
