using System.Collections.ObjectModel;
using PropertyChanged;

namespace NegativeEncoder.Presets;

[AddINotifyPropertyChangedInterface]
public class PresetOption
{
    public ObservableCollection<EnumOption<Encoder>> EncoderOptions { get; set; } = new()
    {
        new EnumOption<Encoder> { Value = Encoder.NVENC, Name = "NVENC" },
        new EnumOption<Encoder> { Value = Encoder.QSV, Name = "QuickSync" },
        new EnumOption<Encoder> { Value = Encoder.VCE, Name = "AMD VCE" }
    };

    public ObservableCollection<EnumOption<Codec>> CodecOptions { get; set; } = new()
    {
        new EnumOption<Codec> { Value = Codec.AVC, Name = "AVC (H.264)" },
        new EnumOption<Codec> { Value = Codec.HEVC, Name = "HEVC (H.265)" },
        new EnumOption<Codec> { Value = Codec.AV1, Name = "AV1" }
    };

    public ObservableCollection<EnumOption<QualityPreset>> QualityPresetOptions { get; set; } = new()
    {
        new EnumOption<QualityPreset> { Value = QualityPreset.Performance, Name = "性能优先（快）" },
        new EnumOption<QualityPreset> { Value = QualityPreset.Balanced, Name = "平衡（默认）" },
        new EnumOption<QualityPreset> { Value = QualityPreset.Quality, Name = "质量优先（慢）" }
    };

    public ObservableCollection<EnumOption<ColorDepth>> ColorDepthOptions { get; set; } = new()
    {
        new EnumOption<ColorDepth> { Value = ColorDepth.C8Bit, Name = "8 Bit" },
        new EnumOption<ColorDepth> { Value = ColorDepth.C10Bit, Name = "10 Bit" }
    };

    public ObservableCollection<EnumOption<Decoder>> DecoderOptions { get; set; } = new()
    {
        new EnumOption<Decoder> { Value = Decoder.AVHW, Name = "硬件解码" },
        new EnumOption<Decoder> { Value = Decoder.AVSW, Name = "软件解码" }
    };

    public ObservableCollection<EnumOption<D3DMode>> D3DModeOptions { get; set; } = new()
    {
        new EnumOption<D3DMode> { Value = D3DMode.Auto, Name = "自动" },
        new EnumOption<D3DMode> { Value = D3DMode.Disable, Name = "禁用" },
        new EnumOption<D3DMode> { Value = D3DMode.D3D9, Name = "d3d9" },
        new EnumOption<D3DMode> { Value = D3DMode.D3D11, Name = "d3d11" }
    };

    public ObservableCollection<EnumOption<AVSync>> AVSyncOptions { get; set; } = new()
    {
        new EnumOption<AVSync> { Value = AVSync.Cfr, Name = "CFR（默认）" },
        new EnumOption<AVSync> { Value = AVSync.ForceCfr, Name = "Force CFR（强制转换）" },
        new EnumOption<AVSync> { Value = AVSync.Vfr, Name = "VFR（使用时间码）" }
    };

    public ObservableCollection<EnumOption<FieldOrder>> FieldOrderOptions { get; set; } = new()
    {
        new EnumOption<FieldOrder> { Value = FieldOrder.TFF, Name = "TFF" },
        new EnumOption<FieldOrder> { Value = FieldOrder.BFF, Name = "BFF" }
    };

    public ObservableCollection<EnumOption<DeInterlaceMethodPreset>> DeInterlaceMethodPresetOptions { get; set; } =
        new()
        {
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.HwNormal, Name = "硬件反交错 普通模式（NVENC/QuickSync)" },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.HwBob, Name = "硬件反交错 Double（NVENC/QuickSync)" },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.HwIt, Name = "硬件反交错 IVTC (QuickSync)" },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.AfsDefault, Name = "AFS Default (NVENC/VCE)" },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.AfsTriple, Name = "AFS Triple (NVENC/VCE)" },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.AfsDouble, Name = "AFS Double (NVENC/VCE)" },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.AfsAnime, Name = "AFS Anime (NVENC/VCE)" },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.AfsAnime24fps, Name = "AFS Anime 24fps IVTC (NVENC/VCE)" },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.Afs24fps, Name = "AFS 24fps IVTC (NVENC/VCE)" },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.Afs30fps, Name = "AFS 30fps (NVENC/VCE)" },
            new EnumOption<DeInterlaceMethodPreset>
            {
                Value = DeInterlaceMethodPreset.Nnedi64NoPre, Name = "nnedi 64(32x6) no prescreen slow (NVENC/VCE)"
            },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.Nnedi64Fast, Name = "nnedi 64(32x6) fast (NVENC/VCE)" },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.Nnedi32Fast, Name = "nnedi 32(32x4) fast (NVENC/VCE)" },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.YadifTff, Name = "Yadif TFF (NVENC)" },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.YadifBff, Name = "Yadif BFF (NVENC)" },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.YadifBob, Name = "Yadif Double (NVENC)" }
        };

    public ObservableCollection<EnumOption<AudioEncode>> AudioEncodeOptions { get; set; } = new()
    {
        new EnumOption<AudioEncode> { Value = AudioEncode.None, Name = "无音频流" },
        new EnumOption<AudioEncode> { Value = AudioEncode.Copy, Name = "复制音频流" },
        new EnumOption<AudioEncode> { Value = AudioEncode.Encode, Name = "编码音频" }
    };

    public ObservableCollection<EnumOption<OutputFormat>> OutputFormatOptions { get; set; } = new()
    {
        new EnumOption<OutputFormat> { Value = OutputFormat.MP4, Name = "MP4" },
        new EnumOption<OutputFormat> { Value = OutputFormat.MPEGTS, Name = "MPEG TS" },
        new EnumOption<OutputFormat> { Value = OutputFormat.FLV, Name = "FLV" },
        new EnumOption<OutputFormat> { Value = OutputFormat.MKV, Name = "MKV" }
    };

    public ObservableCollection<EnumOption<HdrType>> HdrTypeOptions { get; set; } = new()
    {
        new EnumOption<HdrType> { Value = HdrType.SDR, Name = "SDR" },
        new EnumOption<HdrType> { Value = HdrType.HDR10, Name = "HDR10" },
        new EnumOption<HdrType> { Value = HdrType.HLG, Name = "HLG" }
    };

    public ObservableCollection<EnumOption<Hdr2Sdr>> Hdr2SdrOptions { get; set; } = new()
    {
        new EnumOption<Hdr2Sdr> { Value = Hdr2Sdr.None, Name = "不转换" },
        new EnumOption<Hdr2Sdr> { Value = Hdr2Sdr.Hable, Name = "hable" },
        new EnumOption<Hdr2Sdr> { Value = Hdr2Sdr.Mobius, Name = "mobius" },
        new EnumOption<Hdr2Sdr> { Value = Hdr2Sdr.Reinhard, Name = "reinhard" },
        new EnumOption<Hdr2Sdr> { Value = Hdr2Sdr.Bt2390, Name = "bt2390" }
    };
}

[AddINotifyPropertyChangedInterface]
public class EnumOption<T>
{
    public T Value { get; set; }
    public string Name { get; set; }
}