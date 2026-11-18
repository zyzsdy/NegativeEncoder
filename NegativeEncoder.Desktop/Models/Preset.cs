using System.Text.Json;
using System.Text.Json.Serialization;
using NegativeEncoder.Desktop.ViewModels;

namespace NegativeEncoder.Desktop.Models;

// A task captures its own copy so subsequent edits cannot change queued work.
public sealed class Preset : ObservableObject
{
    private string _presetName = "Default";
    public string PresetName
    {
        get => _presetName;
        set => Set(ref _presetName, value);
    }

    private Encoder _encoder = Encoder.NVENC;
    public Encoder Encoder
    {
        get => _encoder;
        set
        {
            if (!Set(ref _encoder, value)) return;
            if (!AvailableModes.Contains(EncodeMode)) EncodeMode = IsVtEnc ? EncodeMode.ABR : EncodeMode.VBR;
            if (!AvailableCodecs.Contains(Codec)) Codec = Codec.AVC;
            if (!AvailableDepths.Contains(ColorDepth)) ColorDepth = AvailableDepths[0];
            if (!AvailableDeinterlacers.Contains(DeInterlaceMethodPreset)) DeInterlaceMethodPreset = AvailableDeinterlacers[0];
            RefreshCapabilities();
        }
    }

    private Codec _codec = Codec.AVC;
    public Codec Codec
    {
        get => _codec;
        set { if (Set(ref _codec, value)) { if (IsVtEnc && value == Codec.AVC) ColorDepth = ColorDepth.C8Bit; RefreshCapabilities(); } }
    }

    private EncodeMode _encodeMode = EncodeMode.VBR;
    public EncodeMode EncodeMode
    {
        get => _encodeMode;
        set
        {
            if (!Set(ref _encodeMode, value)) return;
            if (!SupportsMaxBitrate) IsSetMaxBitrate = false;
            Raise(nameof(SupportsMaxBitrate));
        }
    }

    private string _cqpParam = "24:26:27";
    public string CqpParam
    {
        get => _cqpParam;
        set => Set(ref _cqpParam, value);
    }

    private string _cbrParam = "6500";
    public string CbrParam
    {
        get => _cbrParam;
        set => Set(ref _cbrParam, value);
    }

    private string _vbrParam = "6500";
    public string VbrParam
    {
        get => _vbrParam;
        set => Set(ref _vbrParam, value);
    }

    private string _laParam = "6500";
    public string LaParam
    {
        get => _laParam;
        set => Set(ref _laParam, value);
    }

    private string _laicqParam = "23";
    public string LaicqParam
    {
        get => _laicqParam;
        set => Set(ref _laicqParam, value);
    }

    private string _qvbrParam = "6500";
    public string QvbrParam
    {
        get => _qvbrParam;
        set => Set(ref _qvbrParam, value);
    }

    private QualityPreset _qualityPreset = QualityPreset.Balanced;
    public QualityPreset QualityPreset
    {
        get => _qualityPreset;
        set => Set(ref _qualityPreset, value);
    }

    private ColorDepth _colorDepth = ColorDepth.C8Bit;
    public ColorDepth ColorDepth
    {
        get => _colorDepth;
        set => Set(ref _colorDepth, value);
    }

    private string _vbrQuailty = "23";
    public string VbrQuailty
    {
        get => _vbrQuailty;
        set => Set(ref _vbrQuailty, value);
    }

    private bool _isSetMaxGop = false;
    public bool IsSetMaxGop
    {
        get => _isSetMaxGop;
        set => Set(ref _isSetMaxGop, value);
    }

    private string _maxGop = "600";
    public string MaxGop
    {
        get => _maxGop;
        set => Set(ref _maxGop, value);
    }

    private bool _isStrictGop = false;
    public bool IsStrictGop
    {
        get => _isStrictGop;
        set => Set(ref _isStrictGop, value);
    }

    private bool _isSetDar = false;
    public bool IsSetDar
    {
        get => _isSetDar;
        set => Set(ref _isSetDar, value);
    }

    private string _dar = "16:9";
    public string Dar
    {
        get => _dar;
        set => Set(ref _dar, value);
    }

    private bool _isSetMaxBitrate = false;
    public bool IsSetMaxBitrate
    {
        get => _isSetMaxBitrate;
        set => Set(ref _isSetMaxBitrate, value);
    }

    private string _maxBitrate = "22000";
    public string MaxBitrate
    {
        get => _maxBitrate;
        set => Set(ref _maxBitrate, value);
    }

    private Decoder _decoder = Decoder.AVHW;
    public Decoder Decoder
    {
        get => _decoder;
        set => Set(ref _decoder, value);
    }

    private D3DMode _d3DMode = D3DMode.Auto;
    public D3DMode D3DMode
    {
        get => _d3DMode;
        set => Set(ref _d3DMode, value);
    }

    private bool _isSetAvSync = false;
    public bool IsSetAvSync
    {
        get => _isSetAvSync;
        set => Set(ref _isSetAvSync, value);
    }

    private AVSync _aVSync = AVSync.Cfr;
    public AVSync AVSync
    {
        get => _aVSync;
        set => Set(ref _aVSync, value);
    }

    private bool _isUseDeInterlace = false;
    public bool IsUseDeInterlace
    {
        get => _isUseDeInterlace;
        set => Set(ref _isUseDeInterlace, value);
    }

    private FieldOrder _fieldOrder = FieldOrder.TFF;
    public FieldOrder FieldOrder
    {
        get => _fieldOrder;
        set => Set(ref _fieldOrder, value);
    }

    private DeInterlaceMethodPreset _deInterlaceMethodPreset = DeInterlaceMethodPreset.HwNormal;
    public DeInterlaceMethodPreset DeInterlaceMethodPreset
    {
        get => _deInterlaceMethodPreset;
        set => Set(ref _deInterlaceMethodPreset, value);
    }

    private bool _isSetOutputRes = false;
    public bool IsSetOutputRes
    {
        get => _isSetOutputRes;
        set => Set(ref _isSetOutputRes, value);
    }

    private string _outputResWidth = "1920";
    public string OutputResWidth
    {
        get => _outputResWidth;
        set => Set(ref _outputResWidth, value);
    }

    private string _oUtputResHeight = "1080";
    public string OUtputResHeight
    {
        get => _oUtputResHeight;
        set => Set(ref _oUtputResHeight, value);
    }

    private AudioEncode _audioEncode = AudioEncode.Copy;
    public AudioEncode AudioEncode
    {
        get => _audioEncode;
        set { if (Set(ref _audioEncode, value)) { Raise(nameof(IsAudioEncoding)); } }
    }

    private string _audioBitrate = "192";
    public string AudioBitrate
    {
        get => _audioBitrate;
        set => Set(ref _audioBitrate, value);
    }

    private OutputFormat _outputFormat = OutputFormat.MP4;
    public OutputFormat OutputFormat
    {
        get => _outputFormat;
        set => Set(ref _outputFormat, value);
    }

    private bool _isUseCustomParameters = false;
    public bool IsUseCustomParameters
    {
        get => _isUseCustomParameters;
        set => Set(ref _isUseCustomParameters, value);
    }

    private string _customParameters = "";
    public string CustomParameters
    {
        get => _customParameters;
        set => Set(ref _customParameters, value);
    }

    private bool _isOutputHdr = false;
    public bool IsOutputHdr
    {
        get => _isOutputHdr;
        set => Set(ref _isOutputHdr, value);
    }

    private HdrType _outputHdrType = HdrType.HLG;
    public HdrType OutputHdrType
    {
        get => _outputHdrType;
        set => Set(ref _outputHdrType, value);
    }

    private bool _isRepeatHeaders = false;
    public bool IsRepeatHeaders
    {
        get => _isRepeatHeaders;
        set => Set(ref _isRepeatHeaders, value);
    }

    private bool _isConvertHdrType = false;
    public bool IsConvertHdrType
    {
        get => _isConvertHdrType;
        set { if (Set(ref _isConvertHdrType, value)) { Raise(nameof(IsSdrConversion)); } }
    }

    private HdrType _oldHdrType = HdrType.HLG;
    public HdrType OldHdrType
    {
        get => _oldHdrType;
        set => Set(ref _oldHdrType, value);
    }

    private HdrType _newHdrType = HdrType.HDR10;
    public HdrType NewHdrType
    {
        get => _newHdrType;
        set { if (Set(ref _newHdrType, value)) { Raise(nameof(IsSdrConversion)); } }
    }

    private Hdr2Sdr _hdr2SdrMethod = Hdr2Sdr.None;
    public Hdr2Sdr Hdr2SdrMethod
    {
        get => _hdr2SdrMethod;
        set => Set(ref _hdr2SdrMethod, value);
    }

    private string _hdr2SdrDeSatStrength = "0.75";
    public string Hdr2SdrDeSatStrength
    {
        get => _hdr2SdrDeSatStrength;
        set => Set(ref _hdr2SdrDeSatStrength, value);
    }

    private OutputFormat _muxFormat = OutputFormat.MP4;
    public OutputFormat MuxFormat
    {
        get => _muxFormat;
        set => Set(ref _muxFormat, value);
    }

    public override string ToString() => PresetName;

    private string _abrParam = "6500";
    public string AbrParam { get => _abrParam; set => Set(ref _abrParam, value); }
    private string _vtQuality = "0.65";
    public string VtQuality { get => _vtQuality; set => Set(ref _vtQuality, value); }
    private string _frameRate = "30000/1001";
    public string FrameRate { get => _frameRate; set => Set(ref _frameRate, value); }
    private string _pipeMatrix = "bt709";
    public string PipeMatrix { get => _pipeMatrix; set => Set(ref _pipeMatrix, value); }
    private string _pipeTransfer = "bt709";
    public string PipeTransfer { get => _pipeTransfer; set => Set(ref _pipeTransfer, value); }
    private string _pipePrimaries = "bt709";
    public string PipePrimaries { get => _pipePrimaries; set => Set(ref _pipePrimaries, value); }
    private string _pipeRange = "limited";
    public string PipeRange { get => _pipeRange; set => Set(ref _pipeRange, value); }
    public bool IsVtEnc => Encoder == Encoder.VTEnc;
    public bool SupportsVbr => AvailableModes.Contains(EncodeMode.VBR);
    public bool SupportsCqp => AvailableModes.Contains(EncodeMode.CQP);
    public bool SupportsStrictGop => IsNvenc || IsQsv;
    public bool SupportsColorspace => IsNvenc || IsVtEnc;
    public bool SupportsMaxBitrate => !IsVtEnc || EncodeMode is EncodeMode.ABR or EncodeMode.VBR;
    public bool SupportsD3D => IsQsv && OperatingSystem.IsWindows();
    [JsonIgnore]
    public EncodeMode[] AvailableModes => EncodingTasks.EncoderCapabilities.Modes(Encoder, ToolLibrary.HostPlatform.Current);
    [JsonIgnore]
    public Codec[] AvailableCodecs => IsVtEnc ? [Codec.AVC, Codec.HEVC] : Enum.GetValues<Codec>();
    [JsonIgnore]
    public ColorDepth[] AvailableDepths => IsVtEnc && Codec == Codec.AVC || Encoder == Encoder.VCE ? [ColorDepth.C8Bit] : [ColorDepth.C8Bit, ColorDepth.C10Bit];
    [JsonIgnore]
    public DeInterlaceMethodPreset[] AvailableDeinterlacers => EncodingTasks.EncoderCapabilities.Deinterlacers(Encoder);
    private void RefreshCapabilities()
    {
        foreach (var name in new[] { nameof(IsQsv), nameof(IsNvenc), nameof(IsVtEnc), nameof(SupportsDepth), nameof(SupportsVbr),
                     nameof(SupportsCqp), nameof(SupportsStrictGop), nameof(SupportsColorspace), nameof(SupportsMaxBitrate),
                     nameof(SupportsD3D), nameof(AvailableModes), nameof(AvailableCodecs), nameof(AvailableDepths), nameof(AvailableDeinterlacers) }) Raise(name);
    }
    public bool IsQsv => Encoder == Encoder.QSV;
    public bool IsNvenc => Encoder == Encoder.NVENC;
    public bool SupportsDepth => Encoder != Encoder.VCE;
    public bool IsAudioEncoding => AudioEncode == AudioEncode.Encode;
    public bool IsSdrConversion => IsConvertHdrType && NewHdrType == HdrType.SDR;

    private bool _processAll;
    public bool ProcessAll { get => _processAll; set => Set(ref _processAll, value); }
    private string? _outputPath;
    public string? OutputPath { get => _outputPath; set => Set(ref _outputPath, value); }
    private string _audioOutput = "";
    public string AudioOutput { get => _audioOutput; set => Set(ref _audioOutput, value); }
    private string _muxAudio = "";
    public string MuxAudio { get => _muxAudio; set => Set(ref _muxAudio, value); }
    private string _muxOutput = "";
    public string MuxOutput { get => _muxOutput; set => Set(ref _muxOutput, value); }
    private string _subtitle = "";
    public string Subtitle { get => _subtitle; set => Set(ref _subtitle, value); }
    private string _subtitleRenderer = "subtext";
    public string SubtitleRenderer { get => _subtitleRenderer; set => Set(ref _subtitleRenderer, value); }
    private bool _useRepeat;
    public bool UseRepeat { get => _useRepeat; set => Set(ref _useRepeat, value); }
    private bool _useQtgmc;
    public bool UseQtgmc { get => _useQtgmc; set => Set(ref _useQtgmc, value); }
    private bool _useIvtc;
    public bool UseIvtc { get => _useIvtc; set => Set(ref _useIvtc, value); }
    private bool _useYadifDouble;
    public bool UseYadifDouble { get => _useYadifDouble; set => Set(ref _useYadifDouble, value); }
    private bool _useYadifNormal;
    public bool UseYadifNormal { get => _useYadifNormal; set => Set(ref _useYadifNormal, value); }
    private bool _useBwdifNormal;
    public bool UseBwdifNormal { get => _useBwdifNormal; set => Set(ref _useBwdifNormal, value); }
    private bool _useBwdifDouble;
    public bool UseBwdifDouble { get => _useBwdifDouble; set => Set(ref _useBwdifDouble, value); }
    private bool _vsResize;
    public bool VsResize { get => _vsResize; set => Set(ref _vsResize, value); }
    private string _vsWidth = "1920";
    public string VsWidth { get => _vsWidth; set => Set(ref _vsWidth, value); }
    private string _vsHeight = "1080";
    public string VsHeight { get => _vsHeight; set => Set(ref _vsHeight, value); }
    private string _script = "";
    public string Script { get => _script; set => Set(ref _script, value); }

    // Only editable properties belong to a preset. Capabilities depend on the current host.
    private static readonly JsonSerializerOptions SnapshotOptions = new() { IgnoreReadOnlyProperties = true };
    internal JsonElement Snapshot() => JsonSerializer.SerializeToElement(this, SnapshotOptions);
    internal static Preset FromSnapshot(JsonElement snapshot) =>
        snapshot.Deserialize<Preset>(SnapshotOptions) ?? throw new InvalidDataException("预设内容为空。");

    public Preset Copy() => FromSnapshot(Snapshot());
}
