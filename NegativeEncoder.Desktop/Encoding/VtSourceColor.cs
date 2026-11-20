using System.Text.Json;

namespace NegativeEncoder.Desktop.EncodingTasks;

public sealed record VtSourceColor(string Matrix, string Transfer, string Primaries, string Range, string ChromaLocation)
{
    public string Colorspace => $"matrix={Matrix}:auto,transfer={Transfer}:auto,colorprim={Primaries}:auto,range={Range}:auto";

    // Keep the samples written by FFmpeg consistent with the interpretation supplied to VTEnc.
    public string FfmpegScale =>
        $"scale=in_color_matrix={Matrix}:out_color_matrix={Matrix}:in_range={Range}:out_range={Range}" +
        $":in_chroma_loc={ChromaLocation}:out_chroma_loc={ChromaLocation}";

    public string VtChromaLocation => ChromaLocation == "topleft" ? "top-left" : ChromaLocation;

    public static VtSourceColor FromProbe(string json, Action<string> log)
    {
        using var data = JsonDocument.Parse(json);
        var streams = data.RootElement.GetProperty("streams");
        if (streams.GetArrayLength() != 1) throw new InvalidDataException(Loc.Text("VtSourceColor.MissingStream"));
        var stream = streams[0];
        string Read(string field, string defaultValue, params string[] supported)
        {
            var value = stream.TryGetProperty(field, out var property) ? property.GetString() : null;
            if (value is null or "" or "unknown" or "unspecified")
            {
                log(Loc.Format("VtSourceColor.MissingField", field, defaultValue));
                return defaultValue;
            }
            if (!supported.Contains(value))
                throw new InvalidDataException(Loc.Format("VtSourceColor.UnsupportedColor", field, value));
            return value;
        }

        var matrix = Read("color_space", "bt709", "bt709", "smpte170m", "bt470bg", "bt2020nc", "smpte240m");
        var transfer = Read("color_transfer", "bt709", "bt709", "smpte170m", "bt2020-10", "bt2020-12", "linear",
            "iec61966-2-1", "smpte2084", "arib-std-b67");
        var primaries = Read("color_primaries", "bt709", "bt709", "smpte170m", "smpte240m", "bt470bg", "bt2020",
            "smpte431", "smpte432");
        var range = Read("color_range", "tv", "tv", "pc");
        var chroma = Read("chroma_location", "left", "left", "center", "topleft");
        return new(matrix, transfer, primaries, range == "pc" ? "full" : "limited", chroma);
    }
}
