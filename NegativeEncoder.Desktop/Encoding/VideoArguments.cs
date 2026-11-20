using System.Globalization;
using System.Text;
using Encoder = NegativeEncoder.Desktop.Models.Encoder;
using NegativeEncoder.Desktop.Models;
using NegativeEncoder.Desktop.ToolLibrary;

namespace NegativeEncoder.Desktop.EncodingTasks;

public static class VideoArguments
{
    public static List<string> Build(Preset p, HostPlatform host, bool hdr, bool pipe)
    {
        if (p.Encoder == Encoder.VTEnc && host.Os != "osx") throw new InvalidOperationException(Loc.Text("VideoArguments.VtMacOnly"));
        if (p.Encoder != Encoder.VTEnc && host.Os == "osx") throw new InvalidOperationException(Loc.Text("VideoArguments.MacRequiresVt"));
        if (p.IsUseCustomParameters) return ParseCustom(p.CustomParameters);
        if (!EncoderCapabilities.Modes(p.Encoder, host).Contains(p.EncodeMode)) throw new InvalidOperationException(Loc.Format("VideoArguments.UnsupportedMode", p.Encoder, p.EncodeMode));
        if (p.IsUseDeInterlace && !EncoderCapabilities.Deinterlacers(p.Encoder).Contains(p.DeInterlaceMethodPreset))
            throw new InvalidOperationException(Loc.Format("VideoArguments.UnsupportedDeinterlacing", p.Encoder));
        var args = p.IsVtEnc ? Vt(p, hdr) : LegacyVideoArguments.Build(p, hdr, pipe, host);
        if (p.IsSetOutputRes) args.AddRange(["--output-res", $"{p.OutputResWidth}x{p.OUtputResHeight}"]);
        if (p.IsSetAvSync)
        {
            args.AddRange(["--avsync", p.AVSync.ToString().ToLowerInvariant()]);
            if (p.IsVtEnc && p.AVSync != AVSync.Vfr)
            {
                var parts = p.FrameRate.Split('/');
                if (parts.Length > 2 || parts.Any(part => !int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n <= 0))
                    throw new InvalidOperationException(Loc.Format("VideoArguments.InvalidFrameRate", p.FrameRate));
                args.AddRange(["--fps", p.FrameRate]);
            }
        }
        if (p.IsVtEnc && p.EncodeMode == EncodeMode.CBR && (!p.IsSetAvSync || p.AVSync == AVSync.Vfr))
            throw new InvalidOperationException(Loc.Text("VideoArguments.CbrRequiresCfr"));
        return args;
    }

    private static List<string> Vt(Preset p, bool hdr)
    {
        if (p.Codec == Codec.AV1 || p.Codec == Codec.AVC && p.ColorDepth == ColorDepth.C10Bit)
            throw new InvalidOperationException(Loc.Text("VideoArguments.VtFormats"));
        var args = new List<string> { "--codec", p.Codec == Codec.AVC ? "h264" : "hevc", "--output-depth", ((int)p.ColorDepth).ToString(CultureInfo.InvariantCulture) };
        var (mode, value) = p.EncodeMode switch
        {
            EncodeMode.ABR => ("--bitrate", p.AbrParam),
            EncodeMode.Quality => ("--quality", p.VtQuality),
            EncodeMode.CBR => ("--cbr", p.CbrParam),
            EncodeMode.VBR => ("--vbr", p.VbrParam),
            _ => throw new InvalidOperationException(Loc.Text("VideoArguments.VtRateControl"))
        };
        if (p.EncodeMode == EncodeMode.Quality && (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var quality) || !double.IsFinite(quality) || quality < 0 || quality > 1))
            throw new InvalidOperationException(Loc.Text("VideoArguments.VtQuality"));
        args.AddRange([mode, value, "--preset", p.QualityPreset switch { QualityPreset.Performance => "performance", QualityPreset.Quality => "quality", _ => "default" }]);
        if (p.IsSetMaxGop) args.AddRange(["--gop-len", p.MaxGop]);
        if (p.IsSetDar) args.AddRange(["--dar", p.Dar]);
        if (p.IsSetMaxBitrate)
        {
            if (p.EncodeMode is not (EncodeMode.ABR or EncodeMode.VBR)) throw new InvalidOperationException(Loc.Text("VideoArguments.VtMaxBitrate"));
            args.AddRange(["--max-bitrate", p.MaxBitrate]);
        }
        if (p.IsUseDeInterlace)
        {
            args.AddRange(["--field-order", p.FieldOrder.ToString().ToLowerInvariant()]);
            args.AddRange(p.DeInterlaceMethodPreset switch
            {
                DeInterlaceMethodPreset.HwBob => ["--deinterlace", "bob"],
                DeInterlaceMethodPreset.HwIt => ["--vpp-ivtc"],
                DeInterlaceMethodPreset.YadifTff => ["--vpp-yadif", "mode=tff"],
                DeInterlaceMethodPreset.YadifBff => ["--vpp-yadif", "mode=bff"],
                DeInterlaceMethodPreset.YadifBob => ["--vpp-yadif", "mode=bob"],
                _ => throw new InvalidOperationException(Loc.Text("VideoArguments.VtDeinterlacing"))
            });
        }
        if (hdr)
        {
            // VTEnc requires an explicit colorspace path for HDR, including passthrough.
            var from = p.IsConvertHdrType ? Colors(p.OldHdrType) : ("auto", "auto", "auto");
            var to = p.IsConvertHdrType ? Colors(p.NewHdrType) : ("auto", "auto", "auto");
            var conversion = $"matrix={from.Item1}:{to.Item1},colorprim={from.Item2}:{to.Item2},transfer={from.Item3}:{to.Item3},range=auto:auto";
            if (p.IsConvertHdrType && p.OldHdrType != HdrType.SDR && p.NewHdrType == HdrType.SDR)
                conversion += $",hdr2sdr={p.Hdr2SdrMethod.ToString().ToLowerInvariant()},desat_strength={p.Hdr2SdrDeSatStrength}";
            args.AddRange(["--vpp-colorspace", conversion]);
            if (p.IsOutputHdr)
            {
                var (matrix, prim, transfer) = Colors(p.OutputHdrType);
                args.AddRange(["--color-tags", $"matrix={matrix},colorprim={prim},transfer={transfer}"]);
            }
        }
        return args;
    }

    public static (string, string, string) Colors(HdrType type) => type switch
    {
        HdrType.SDR => ("bt709", "bt709", "bt709"),
        HdrType.HDR10 => ("bt2020nc", "bt2020", "smpte2084"),
        HdrType.HLG => ("bt2020nc", "bt2020", "arib-std-b67"),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    // Shell-free tokenizer: quotes group tokens, backslashes in Windows paths remain literal.
    public static List<string> ParseCustom(string text)
    {
        var args = new List<string>();
        var word = new StringBuilder();
        char quote = '\0';
        var started = false;
        foreach (var c in text)
        {
            if (c == '\0') throw new ArgumentException(Loc.Text("VideoArguments.NullArgument"));
            if (quote != '\0') { if (c == quote) quote = '\0'; else word.Append(c); started = true; }
            else if (c is '\'' or '"') { quote = c; started = true; }
            else if (char.IsWhiteSpace(c)) { if (started) { args.Add(word.ToString()); word.Clear(); started = false; } }
            else { word.Append(c); started = true; }
        }
        if (quote != '\0') throw new ArgumentException(Loc.Text("VideoArguments.UnclosedQuote"));
        if (started) args.Add(word.ToString());
        if (args.Count == 0) throw new ArgumentException(Loc.Text("VideoArguments.EmptyArguments"));
        if (args.Any(a => a is "-i" or "--input" or "-o" or "--output" or "-f" or "--output-format" || a.StartsWith("--output=", StringComparison.Ordinal) || a.StartsWith("--input=", StringComparison.Ordinal)))
            throw new ArgumentException(Loc.Text("VideoArguments.ReservedArguments"));
        return args;
    }
}
