using System.ComponentModel;
using PropertyChanged;

namespace NegativeEncoder.Presets;

[AddINotifyPropertyChangedInterface]
public class Preset : INotifyPropertyChanged
{
    public Preset()
    {
        PropertyChanged += PresetProvider.CurrentPreset_PropertyChanged;
    }

    /// <summary>
    ///     预设标题
    /// </summary>
    public string PresetName { get; set; } = "Default";

    /// <summary>
    ///     编码器
    /// </summary>
    public Encoder Encoder { get; set; } = Encoder.NVENC;

    /// <summary>
    ///     目标编码
    /// </summary>
    public Codec Codec { get; set; } = Codec.AVC;

    /// <summary>
    ///     编码模式
    /// </summary>
    public EncodeMode EncodeMode { get; set; } = EncodeMode.VBR;

    public string CqpParam { get; set; } = "24:26:27";
    public string CbrParam { get; set; } = "6500";
    public string VbrParam { get; set; } = "6500";
    public string LaParam { get; set; } = "6500";
    public string LaicqParam { get; set; } = "23";
    public string QvbrParam { get; set; } = "6500";

    /// <summary>
    ///     编码质量预设
    ///     （预设名-说明=NVENC/QSV/VCE）
    ///     Performance-性能优先=performance/faster/fast
    ///     Balanced-平衡=default/balanced/default
    ///     Quality-质量优先=quality/best/slow
    /// </summary>
    public QualityPreset QualityPreset { get; set; } = QualityPreset.Balanced;

    /// <summary>
    ///     色深
    /// </summary>
    public ColorDepth ColorDepth { get; set; } = ColorDepth.C8Bit;

    /// <summary>
    ///     VBR质量，当使用VBR模式(NVENC)/QVBR模式(QSV)时，可以指定目标质量（0~51，默认23）
    /// </summary>
    public string VbrQuailty { get; set; } = "23";

    /// <summary>
    ///     是否设置最大GOP
    /// </summary>
    public bool IsSetMaxGop { get; set; } = false;

    /// <summary>
    ///     最大GOP的设置值
    /// </summary>
    public string MaxGop { get; set; } = "600";

    /// <summary>
    ///     是否使用固定GOP
    /// </summary>
    public bool IsStrictGop { get; set; } = false;

    /// <summary>
    ///     是否设置显示比例
    /// </summary>
    public bool IsSetDar { get; set; } = false;

    /// <summary>
    ///     显示比例
    /// </summary>
    public string Dar { get; set; } = "16:9";

    /// <summary>
    ///     是否设置限制最大码率
    /// </summary>
    public bool IsSetMaxBitrate { get; set; } = false;

    /// <summary>
    ///     最大码率
    /// </summary>
    public string MaxBitrate { get; set; } = "22000";

    /// <summary>
    ///     解码器 （avhw-avformat+cuvid硬解 avsw-avformat+ffmpeg软解）
    /// </summary>
    public Decoder Decoder { get; set; } = Decoder.AVHW;

    /// <summary>
    ///     D3D显存模式（仅QSV）
    /// </summary>
    public D3DMode D3DMode { get; set; } = D3DMode.Auto;

    /// <summary>
    ///     是否设置音频同步
    /// </summary>
    public bool IsSetAvSync { get; set; } = false;

    /// <summary>
    ///     音频同步
    /// </summary>
    public AVSync AVSync { get; set; } = AVSync.Cfr;

    /// <summary>
    ///     是否启用反交错
    /// </summary>
    public bool IsUseDeInterlace { get; set; } = false;

    /// <summary>
    ///     交错源场顺序
    /// </summary>
    public FieldOrder FieldOrder { get; set; } = FieldOrder.TFF;

    /// <summary>
    ///     硬件反交错模式
    /// </summary>
    public DeInterlaceMethodPreset DeInterlaceMethodPreset { get; set; } = DeInterlaceMethodPreset.HwNormal;

    /// <summary>
    ///     是否设置输出分辨率（调整大小）
    /// </summary>
    public bool IsSetOutputRes { get; set; } = false;

    public string OutputResWidth { get; set; } = "1920";
    public string OUtputResHeight { get; set; } = "1080";

    /// <summary>
    ///     音频编码选项
    /// </summary>
    public AudioEncode AudioEncode { get; set; } = AudioEncode.Copy;

    public string AudioBitrate { get; set; } = "192";

    /// <summary>
    ///     输出格式
    /// </summary>
    public OutputFormat OutputFormat { get; set; } = OutputFormat.MP4;

    /// <summary>
    ///     使用自定义参数
    /// </summary>
    public bool IsUseCustomParameters { get; set; } = false;

    public string CustomParameters { get; set; } = "";

    /// <summary>
    ///     是否输出带有HDR标记的视频
    /// </summary>
    public bool IsOutputHdr { get; set; } = false;

    /// <summary>
    ///     输出HDR格式
    /// </summary>
    public HdrType OutputHdrType { get; set; } = HdrType.HLG;

    /// <summary>
    ///     是否在每个IDR帧重复输出Header
    /// </summary>
    public bool IsRepeatHeaders { get; set; } = false;

    /// <summary>
    ///     是否进行HDR格式转换
    /// </summary>
    public bool IsConvertHdrType { get; set; } = false;

    public HdrType OldHdrType { get; set; } = HdrType.HLG;
    public HdrType NewHdrType { get; set; } = HdrType.HDR10;


    /// <summary>
    ///     HDR转SDR算法
    /// </summary>
    public Hdr2Sdr Hdr2SdrMethod { get; set; } = Hdr2Sdr.None;

    public string Hdr2SdrDeSatStrength { get; set; } = "0.75";

    /// <summary>
    ///     封装格式
    /// </summary>
    public OutputFormat MuxFormat { get; set; } = OutputFormat.MP4;

    public event PropertyChangedEventHandler PropertyChanged;

    private void RaisePropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}