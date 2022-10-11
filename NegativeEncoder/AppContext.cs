using NegativeEncoder.About;
using NegativeEncoder.EncodingTask;
using NegativeEncoder.Presets;
using NegativeEncoder.StatusBar;
using NegativeEncoder.SystemOptions;

namespace NegativeEncoder;

public static class AppContext
{
    /// <summary>
    ///     文件选择器
    /// </summary>
    public static FileSelector.FileSelector FileSelector { get; set; } = new();

    /// <summary>
    ///     当前程序版本
    /// </summary>
    public static Version Version { get; set; } = new();

    /// <summary>
    ///     状态栏显示对象
    /// </summary>
    public static Status Status { get; set; } = new();

    /// <summary>
    ///     全局系统设置
    /// </summary>
    public static Config Config { get; set; } = new();

    /// <summary>
    ///     预设全局对象
    /// </summary>
    public static PresetContext PresetContext { get; set; } = new();

    /// <summary>
    ///     编码任务队列
    /// </summary>
    public static EncodingContext EncodingContext { get; set; } = new();
}