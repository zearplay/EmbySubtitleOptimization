using System.ComponentModel;

namespace EmbySubtitleOptimization
{
    /// <summary>Supported subtitle font emphasis styles.</summary>
    public enum SubtitleFontStyle
    {
        [Description("常规")]
        Regular,

        [Description("粗体")]
        Bold,

        [Description("斜体")]
        Italic,

        [Description("粗斜体")]
        BoldItalic
    }
}
