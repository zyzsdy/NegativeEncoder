using PropertyChanged;
using System.Collections.ObjectModel;

namespace NegativeEncoder.Presets
{
    [AddINotifyPropertyChangedInterface]
    public class PresetOption
    {
        public ObservableCollection<EnumOption<Encoder>> EncoderOptions { get; set; } = new ObservableCollection<EnumOption<Encoder>>
        {
            new EnumOption<Encoder> { Value = Encoder.NVENC, Name = "NVENC" },
            new EnumOption<Encoder> { Value = Encoder.QSV, Name = "QuickSync" },
            new EnumOption<Encoder> { Value = Encoder.VCE, Name = "AMD VCE" }
        };

        public ObservableCollection<EnumOption<Codec>> CodecOptions { get; set; } = new ObservableCollection<EnumOption<Codec>>
        {
            new EnumOption<Codec> { Value = Codec.AVC, Name = "AVC (H.264)" },
            new EnumOption<Codec> { Value = Codec.HEVC, Name = "HEVC (H.265)"}
        };

        public ObservableCollection<EnumOption<QualityPreset>> QualityPresetOptions { get; set; } = new ObservableCollection<EnumOption<QualityPreset>>
        {
            new EnumOption<QualityPreset> { Value = QualityPreset.Performance, Name = "性能优先（快）" },
            new EnumOption<QualityPreset> { Value = QualityPreset.Balanced, Name = "平衡（默认）" },
            new EnumOption<QualityPreset> { Value = QualityPreset.Quality, Name = "质量优先（慢）" }
        };

        public ObservableCollection<EnumOption<ColorDepth>> ColorDepthOptions { get; set; } = new ObservableCollection<EnumOption<ColorDepth>>
        {
            new EnumOption<ColorDepth> { Value = ColorDepth.C8Bit, Name = "8 Bit" },
            new EnumOption<ColorDepth> { Value = ColorDepth.C10Bit, Name = "10 Bit" }
        };

        public ObservableCollection<EnumOption<Decoder>> DecoderOptions { get; set; } = new ObservableCollection<EnumOption<Decoder>>
        {
            new EnumOption<Decoder> { Value = Decoder.AVHW, Name = "硬件解码" },
            new EnumOption<Decoder> { Value = Decoder.AVSW, Name = "软件解码" }
        };

        public ObservableCollection<EnumOption<D3DMode>> D3DModeOptions { get; set; } = new ObservableCollection<EnumOption<D3DMode>>
        {
            new EnumOption<D3DMode> { Value = D3DMode.Auto, Name = "自动" },
            new EnumOption<D3DMode> { Value = D3DMode.Disable, Name = "禁用" },
            new EnumOption<D3DMode> { Value = D3DMode.D3D9, Name = "d3d9" },
            new EnumOption<D3DMode> { Value = D3DMode.D3D11, Name = "d3d11" }
        };
    }

    [AddINotifyPropertyChangedInterface]
    public class EnumOption<T>
    {
        public T Value { get; set; }
        public string Name { get; set; }
    }
}