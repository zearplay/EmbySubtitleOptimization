using System.ComponentModel;

namespace EmbySubtitleOptimization
{
    /// <summary>Controls whether generated subtitles retain or override their original position.</summary>
    public enum SubtitlePositionMode
    {
        [Description("不修改原字幕位置")]
        PreserveOriginal,

        [Description("最底部居中")]
        BottomCenter
    }
}
