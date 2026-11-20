using System.Collections.ObjectModel;


namespace NegativeEncoder.Desktop.Models;


public class PresetOption
{
    public ObservableCollection<EnumOption<Encoder>> EncoderOptions { get; set; } = new()
    {
        new EnumOption<Encoder> { Value = Encoder.NVENC, Name = Loc.Text("PresetOption.NVENC") },
        new EnumOption<Encoder> { Value = Encoder.QSV, Name = Loc.Text("PresetOption.QuickSync") },
        new EnumOption<Encoder> { Value = Encoder.VCE, Name = Loc.Text("PresetOption.AMDVCE") },
        new EnumOption<Encoder> { Value = Encoder.VTEnc, Name = Loc.Text("PresetOption.VideoToolboxVTEnc") }
    };

    public ObservableCollection<EnumOption<Codec>> CodecOptions { get; set; } = new()
    {
        new EnumOption<Codec> { Value = Codec.AVC, Name = Loc.Text("PresetOption.Codec.AVC") },
        new EnumOption<Codec> { Value = Codec.HEVC, Name = Loc.Text("PresetOption.Codec.HEVC") },
        new EnumOption<Codec> { Value = Codec.AV1, Name = Loc.Text("PresetOption.Codec.AV1") }
    };

    public ObservableCollection<EnumOption<QualityPreset>> QualityPresetOptions { get; set; } = new()
    {
        new EnumOption<QualityPreset> { Value = QualityPreset.Performance, Name = Loc.Text("PresetOption.Performance") },
        new EnumOption<QualityPreset> { Value = QualityPreset.Balanced, Name = Loc.Text("PresetOption.Balanced") },
        new EnumOption<QualityPreset> { Value = QualityPreset.Quality, Name = Loc.Text("PresetOption.Quality") }
    };

    public ObservableCollection<EnumOption<ColorDepth>> ColorDepthOptions { get; set; } = new()
    {
        new EnumOption<ColorDepth> { Value = ColorDepth.C8Bit, Name = Loc.Text("PresetOption.Depth.C8Bit") },
        new EnumOption<ColorDepth> { Value = ColorDepth.C10Bit, Name = Loc.Text("PresetOption.Depth.C10Bit") }
    };

    public ObservableCollection<EnumOption<Decoder>> DecoderOptions { get; set; } = new()
    {
        new EnumOption<Decoder> { Value = Decoder.AVHW, Name = Loc.Text("PresetOption.HardwareDecode") },
        new EnumOption<Decoder> { Value = Decoder.AVSW, Name = Loc.Text("PresetOption.SoftwareDecode") }
    };

    public ObservableCollection<EnumOption<D3DMode>> D3DModeOptions { get; set; } = new()
    {
        new EnumOption<D3DMode> { Value = D3DMode.Auto, Name = Loc.Text("PresetOption.Auto") },
        new EnumOption<D3DMode> { Value = D3DMode.Disable, Name = Loc.Text("PresetOption.Disabled") },
        new EnumOption<D3DMode> { Value = D3DMode.D3D9, Name = Loc.Text("PresetOption.D3d9") },
        new EnumOption<D3DMode> { Value = D3DMode.D3D11, Name = Loc.Text("PresetOption.D3d11") }
    };

    public ObservableCollection<EnumOption<AVSync>> AVSyncOptions { get; set; } = new()
    {
        new EnumOption<AVSync> { Value = AVSync.Cfr, Name = Loc.Text("PresetOption.Cfr") },
        new EnumOption<AVSync> { Value = AVSync.ForceCfr, Name = Loc.Text("PresetOption.ForceCfr") },
        new EnumOption<AVSync> { Value = AVSync.Vfr, Name = Loc.Text("PresetOption.Vfr") }
    };

    public IReadOnlyList<FrameRateOption> FrameRateOptions { get; } =
    [
        new("24000/1001", "23.976"), new("24"), new("25"), new("30000/1001", "29.97"), new("30"),
        new("48"), new("50"), new("60000/1001", "59.94"), new("60"), new("120")
    ];

    public ObservableCollection<EnumOption<FieldOrder>> FieldOrderOptions { get; set; } = new()
    {
        new EnumOption<FieldOrder> { Value = FieldOrder.TFF, Name = Loc.Text("PresetOption.TFF") },
        new EnumOption<FieldOrder> { Value = FieldOrder.BFF, Name = Loc.Text("PresetOption.BFF") }
    };

    public ObservableCollection<EnumOption<DeInterlaceMethodPreset>> DeInterlaceMethodPresetOptions { get; set; } =
        new()
        {
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.HwNormal, Name = Loc.Text("PresetOption.HardwareNormal") },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.HwBob, Name = Loc.Text("PresetOption.HardwareBob") },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.HwIt, Name = Loc.Text("PresetOption.HardwareIvtc") },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.AfsDefault, Name = Loc.Text("PresetOption.AFSDefaultNVENCVCE") },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.AfsTriple, Name = Loc.Text("PresetOption.AFSTripleNVENCVCE") },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.AfsDouble, Name = Loc.Text("PresetOption.AFSDoubleNVENCVCE") },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.AfsAnime, Name = Loc.Text("PresetOption.AFSAnimeNVENCVCE") },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.AfsAnime24fps, Name = Loc.Text("PresetOption.AFSAnime24fpsIVTCNVENCVCE") },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.Afs24fps, Name = Loc.Text("PresetOption.AFS24fpsIVTCNVENCVCE") },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.Afs30fps, Name = Loc.Text("PresetOption.AFS30fpsNVENCVCE") },
            new EnumOption<DeInterlaceMethodPreset>
            {
                Value = DeInterlaceMethodPreset.Nnedi64NoPre, Name = Loc.Text("PresetOption.NnediSlow")
            },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.Nnedi64Fast, Name = Loc.Text("PresetOption.Nnedi64Fast") },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.Nnedi32Fast, Name = Loc.Text("PresetOption.Nnedi32Fast") },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.YadifTff, Name = Loc.Text("PresetOption.YadifTFFNVENC") },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.YadifBff, Name = Loc.Text("PresetOption.YadifBFFNVENC") },
            new EnumOption<DeInterlaceMethodPreset>
                { Value = DeInterlaceMethodPreset.YadifBob, Name = Loc.Text("PresetOption.YadifDoubleNVENC") }
        };

    public ObservableCollection<EnumOption<AudioEncode>> AudioEncodeOptions { get; set; } = new()
    {
        new EnumOption<AudioEncode> { Value = AudioEncode.None, Name = Loc.Text("PresetOption.NoAudio") },
        new EnumOption<AudioEncode> { Value = AudioEncode.Copy, Name = Loc.Text("PresetOption.CopyAudio") },
        new EnumOption<AudioEncode> { Value = AudioEncode.Encode, Name = Loc.Text("Operation.Audio") }
    };

    public ObservableCollection<EnumOption<OutputFormat>> OutputFormatOptions { get; set; } = new()
    {
        new EnumOption<OutputFormat> { Value = OutputFormat.MP4, Name = Loc.Text("PresetOption.MP4") },
        new EnumOption<OutputFormat> { Value = OutputFormat.MPEGTS, Name = Loc.Text("PresetOption.MPEGTS") },
        new EnumOption<OutputFormat> { Value = OutputFormat.FLV, Name = Loc.Text("PresetOption.FLV") },
        new EnumOption<OutputFormat> { Value = OutputFormat.MKV, Name = Loc.Text("PresetOption.MKV") }
    };

    public ObservableCollection<EnumOption<HdrType>> HdrTypeOptions { get; set; } = new()
    {
        new EnumOption<HdrType> { Value = HdrType.SDR, Name = Loc.Text("PresetOption.SDR") },
        new EnumOption<HdrType> { Value = HdrType.HDR10, Name = Loc.Text("PresetOption.HDR10") },
        new EnumOption<HdrType> { Value = HdrType.HLG, Name = Loc.Text("PresetOption.HLG") }
    };

    public ObservableCollection<EnumOption<Hdr2Sdr>> Hdr2SdrOptions { get; set; } = new()
    {
        new EnumOption<Hdr2Sdr> { Value = Hdr2Sdr.None, Name = Loc.Text("PresetOption.NoConversion") },
        new EnumOption<Hdr2Sdr> { Value = Hdr2Sdr.Hable, Name = Loc.Text("PresetOption.Hable") },
        new EnumOption<Hdr2Sdr> { Value = Hdr2Sdr.Mobius, Name = Loc.Text("PresetOption.Mobius") },
        new EnumOption<Hdr2Sdr> { Value = Hdr2Sdr.Reinhard, Name = Loc.Text("PresetOption.Reinhard") },
        new EnumOption<Hdr2Sdr> { Value = Hdr2Sdr.Bt2390, Name = Loc.Text("PresetOption.Bt2390") }
    };
}


public class EnumOption<T>
{
    public override string ToString() => Name;
    public T Value { get; set; } = default!;
    public string Name { get; set; } = "";
}

public sealed record FrameRateOption(string Value, string? Approximation = null)
{
    public string Label => Approximation is null ? Value : $"{Value} ({Approximation})";

    // The editable ComboBox writes the exact rate into the preset.
    public override string ToString() => Value;
}
