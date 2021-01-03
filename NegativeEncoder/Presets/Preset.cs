using PropertyChanged;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace NegativeEncoder.Presets
{
    [AddINotifyPropertyChangedInterface]
    public class Preset : INotifyPropertyChanged
    {
        /// <summary>
        /// 预设标题
        /// </summary>
        public string PresetName { get; set; } = "Default";

        /// <summary>
        /// 编码器
        /// </summary>
        public Encoder Encoder { get; set; } = Encoder.NVENC;

        /// <summary>
        /// 目标编码
        /// </summary>
        public Codec Codec { get; set; } = Codec.AVC;

        /// <summary>
        /// 编码模式
        /// </summary>
        public EncodeMode EncodeMode { get; set; } = EncodeMode.VBR;

        public string CqpParam { get; set; } = "24:26:27";
        public string CbrParam { get; set; } = "6500";
        public string VbrParam { get; set; } = "6500";
        public string LaParam { get; set; } = "6500";
        public string LaicqParam { get; set; } = "23";
        public string QvbrParam { get; set; } = "6500";

        /// <summary>
        /// 编码质量预设
        /// （预设名-说明=NVENC/QSV/VCE）
        /// Performance-性能优先=performance/faster/fast
        /// Balanced-平衡=default/balanced/default
        /// Quality-质量优先=quality/best/slow
        /// </summary>
        public QualityPreset QualityPreset { get; set; } = QualityPreset.Balanced;

        /// <summary>
        /// 色深
        /// </summary>
        public ColorDepth ColorDepth { get; set; } = ColorDepth.C8Bit;

        /// <summary>
        /// VBR质量，当使用VBR模式(NVENC)/QVBR模式(QSV)时，可以指定目标质量（0~51，默认23）
        /// </summary>
        public string VbrQuailty { get; set; } = "23";

        /// <summary>
        /// 是否设置最大GOP
        /// </summary>
        public bool IsSetMaxGop { get; set; } = false;

        /// <summary>
        /// 最大GOP的设置值
        /// </summary>
        public string MaxGop { get; set; } = "600";

        /// <summary>
        /// 是否使用固定GOP
        /// </summary>
        public bool IsStrictGop { get; set; } = false;

        /// <summary>
        /// 是否设置显示比例
        /// </summary>
        public bool IsSetDar { get; set; } = false;

        /// <summary>
        /// 显示比例
        /// </summary>
        public string Dar { get; set; } = "16:9";

        /// <summary>
        /// 是否设置限制最大码率
        /// </summary>
        public bool IsSetMaxBitrate { get; set; } = false;

        /// <summary>
        /// 最大码率
        /// </summary>
        public string MaxBitrate { get; set; } = "22000";

        /// <summary>
        /// 解码器 （avhw-avformat+cuvid硬解 avsw-avformat+ffmpeg软解）
        /// </summary>
        public Decoder Decoder { get; set; } = Decoder.AVHW;

        /// <summary>
        /// D3D显存模式（仅QSV）
        /// </summary>
        public D3DMode D3DMode { get; set; } = D3DMode.Auto;

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
