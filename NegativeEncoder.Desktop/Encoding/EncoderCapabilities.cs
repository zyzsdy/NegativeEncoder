using NegativeEncoder.Desktop.Models;
using NegativeEncoder.Desktop.ToolLibrary;

namespace NegativeEncoder.Desktop.EncodingTasks;

public static class EncoderCapabilities
{
    public static Encoder? Default(HostPlatform host, HardwareReport hardware)
    {
        if (hardware.Error is not null) throw new InvalidOperationException(hardware.Error);
        if (host.Os == "osx") return Encoder.VTEnc;
        if (hardware.Vendors.Contains("NVIDIA")) return Encoder.NVENC;
        if (hardware.Vendors.Contains("Intel")) return Encoder.QSV;
        if (hardware.Vendors.Contains("AMD")) return Encoder.VCE;
        return null;
    }

    public static EncodeMode[] Modes(Encoder encoder, HostPlatform host) => encoder switch
    {
        Encoder.VTEnc when host.Os == "osx" && host.Version.Major >= 26 => [EncodeMode.ABR, EncodeMode.Quality, EncodeMode.CBR, EncodeMode.VBR],
        Encoder.VTEnc => [EncodeMode.ABR, EncodeMode.Quality, EncodeMode.CBR],
        Encoder.QSV => [EncodeMode.VBR, EncodeMode.CBR, EncodeMode.CQP, EncodeMode.LA, EncodeMode.LAICQ, EncodeMode.QVBR],
        Encoder.NVENC or Encoder.VCE => [EncodeMode.VBR, EncodeMode.CBR, EncodeMode.CQP],
        _ => throw new ArgumentOutOfRangeException(nameof(encoder))
    };

    public static DeInterlaceMethodPreset[] Deinterlacers(Encoder encoder) => encoder switch
    {
        Encoder.VTEnc => [DeInterlaceMethodPreset.HwBob, DeInterlaceMethodPreset.HwIt, DeInterlaceMethodPreset.YadifTff, DeInterlaceMethodPreset.YadifBff, DeInterlaceMethodPreset.YadifBob],
        Encoder.QSV => [DeInterlaceMethodPreset.HwNormal, DeInterlaceMethodPreset.HwBob, DeInterlaceMethodPreset.HwIt],
        Encoder.NVENC => Enum.GetValues<DeInterlaceMethodPreset>().Where(v => v != DeInterlaceMethodPreset.HwIt).ToArray(),
        Encoder.VCE => Enum.GetValues<DeInterlaceMethodPreset>().Where(v => v >= DeInterlaceMethodPreset.AfsDefault && v <= DeInterlaceMethodPreset.Nnedi32Fast).ToArray(),
        _ => throw new ArgumentOutOfRangeException(nameof(encoder))
    };

    public static string ToolId(Encoder encoder) => encoder switch
    {
        Encoder.NVENC => "nvenc",
        Encoder.QSV => "qsvenc",
        Encoder.VCE => "vceenc",
        Encoder.VTEnc => "vtenc",
        _ => throw new ArgumentOutOfRangeException(nameof(encoder))
    };
}
