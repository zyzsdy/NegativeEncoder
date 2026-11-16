using NegativeEncoder.Desktop.ViewModels;

namespace NegativeEncoder.Desktop.Models;

// UI-only draft. No dependency on the legacy provider, disk storage or encoder services.
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
        set { if (Set(ref _encoder, value)) { Raise(nameof(IsQsv)); Raise(nameof(IsNvenc)); Raise(nameof(SupportsDepth)); } }
    }

    private Codec _codec = Codec.AVC;
    public Codec Codec
    {
        get => _codec;
        set => Set(ref _codec, value);
    }

    private EncodeMode _encodeMode = EncodeMode.VBR;
    public EncodeMode EncodeMode
    {
        get => _encodeMode;
        set => Set(ref _encodeMode, value);
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

    public bool IsQsv => Encoder == Encoder.QSV;
    public bool IsNvenc => Encoder == Encoder.NVENC;
    public bool SupportsDepth => Encoder != Encoder.VCE;
    public bool IsAudioEncoding => AudioEncode == AudioEncode.Encode;
    public bool IsSdrConversion => IsConvertHdrType && NewHdrType == HdrType.SDR;

    public Preset Copy()
    {
        var copy = new Preset();
        copy.PresetName = PresetName;
        copy.Encoder = Encoder;
        copy.Codec = Codec;
        copy.EncodeMode = EncodeMode;
        copy.CqpParam = CqpParam;
        copy.CbrParam = CbrParam;
        copy.VbrParam = VbrParam;
        copy.LaParam = LaParam;
        copy.LaicqParam = LaicqParam;
        copy.QvbrParam = QvbrParam;
        copy.QualityPreset = QualityPreset;
        copy.ColorDepth = ColorDepth;
        copy.VbrQuailty = VbrQuailty;
        copy.IsSetMaxGop = IsSetMaxGop;
        copy.MaxGop = MaxGop;
        copy.IsStrictGop = IsStrictGop;
        copy.IsSetDar = IsSetDar;
        copy.Dar = Dar;
        copy.IsSetMaxBitrate = IsSetMaxBitrate;
        copy.MaxBitrate = MaxBitrate;
        copy.Decoder = Decoder;
        copy.D3DMode = D3DMode;
        copy.IsSetAvSync = IsSetAvSync;
        copy.AVSync = AVSync;
        copy.IsUseDeInterlace = IsUseDeInterlace;
        copy.FieldOrder = FieldOrder;
        copy.DeInterlaceMethodPreset = DeInterlaceMethodPreset;
        copy.IsSetOutputRes = IsSetOutputRes;
        copy.OutputResWidth = OutputResWidth;
        copy.OUtputResHeight = OUtputResHeight;
        copy.AudioEncode = AudioEncode;
        copy.AudioBitrate = AudioBitrate;
        copy.OutputFormat = OutputFormat;
        copy.IsUseCustomParameters = IsUseCustomParameters;
        copy.CustomParameters = CustomParameters;
        copy.IsOutputHdr = IsOutputHdr;
        copy.OutputHdrType = OutputHdrType;
        copy.IsRepeatHeaders = IsRepeatHeaders;
        copy.IsConvertHdrType = IsConvertHdrType;
        copy.OldHdrType = OldHdrType;
        copy.NewHdrType = NewHdrType;
        copy.Hdr2SdrMethod = Hdr2SdrMethod;
        copy.Hdr2SdrDeSatStrength = Hdr2SdrDeSatStrength;
        copy.MuxFormat = MuxFormat;
        return copy;
    }
}
