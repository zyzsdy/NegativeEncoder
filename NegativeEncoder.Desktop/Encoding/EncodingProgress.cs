using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using NegativeEncoder.Desktop.ToolLibrary;

namespace NegativeEncoder.Desktop.EncodingTasks;

public sealed record EncodingProgress(double? Percent = null, long? Frames = null, long? TotalFrames = null,
    TimeSpan? Remaining = null, bool EstimatedTotal = false)
{
    // Stages are equally weighted. Reserve 100% until the queue has published the output.
    public EncodingProgress ForTask(int stepIndex, int stepCount, TimeSpan elapsed)
    {
        var percent = (100.0 * stepIndex + (Percent ?? 0)) / stepCount;
        var remaining = Remaining;
        if (stepIndex + 1 < stepCount)
            remaining = EstimateRemaining(percent, elapsed);
        return this with { Percent = stepIndex == 0 && Percent is null ? null : Math.Min(99.9, percent), Remaining = remaining };
    }

    internal static TimeSpan? EstimateRemaining(double? percent, TimeSpan elapsed)
    {
        var seconds = percent > 0 ? elapsed.TotalSeconds * (100 - percent) / percent : null;
        return ToRemaining(seconds);
    }

    internal static TimeSpan? ToRemaining(double? seconds) =>
        seconds is >= 0 && seconds < TimeSpan.MaxValue.TotalSeconds ? TimeSpan.FromSeconds(seconds.Value) : null;
}

public sealed record VideoLength(long? Frames, double? Duration, bool EstimatedFrames = false)
{
    public static async Task<(VideoLength Length, VtSourceColor? Color)> ProbeAsync(string ffprobe, string input,
        bool requireColor, Action<string> log, CancellationToken token)
    {
        string[] args = ["-v", "error", "-select_streams", "v:0", "-show_entries",
            "stream=nb_frames,duration,avg_frame_rate,color_space,color_transfer,color_primaries,color_range,chroma_location:stream_tags=NUMBER_OF_FRAMES:format=duration",
            "-of", "json", "-i", input];
        log(new EncoderCommand(ffprobe, args).Preview + "\n");
        var result = await new ProcessRunner().RunAsync(ffprobe, args, token);
        log(result.Combined + "\n");
        if (requireColor) result.EnsureSuccess(Loc.Text("VtSourceColor.Probe"));
        if (result.ExitCode == 0)
            return (FromProbe(result.Output), requireColor ? VtSourceColor.FromProbe(result.Output, log) : null);

        log(Loc.Format("EncodingRunner.FrameProbeFailed", result.ExitCode) + "\n");
        return (new(null, null), null);
    }

    public static VideoLength FromProbe(string json)
    {
        using var data = JsonDocument.Parse(json);
        var streams = data.RootElement.GetProperty("streams");
        var video = streams.GetArrayLength() > 0 ? streams[0] : default;
        static string? Field(JsonElement element, string key) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var field) ? field.ToString() : null;
        static double? Positive(string? value) =>
            double.TryParse(value, CultureInfo.InvariantCulture, out var n) && n > 0 && double.IsFinite(n) ? n : null;

        var duration = Positive(Field(video, "duration"));
        if (duration is null && data.RootElement.TryGetProperty("format", out var format))
            duration = Positive(Field(format, "duration"));
        var count = Field(video, "nb_frames");
        if (long.TryParse(count, out var frames) && frames > 0) return new(frames, duration);
        if (video.ValueKind == JsonValueKind.Object && video.TryGetProperty("tags", out var tags) &&
            long.TryParse(Field(tags, "NUMBER_OF_FRAMES"), out frames) && frames > 0) return new(frames, duration);

        var rate = Field(video, "avg_frame_rate")?.Split('/');
        var estimate = rate is [var numerator, var denominator] ? duration * Positive(numerator) / Positive(denominator) : null;
        return estimate is >= 1 and < long.MaxValue ? new((long)Math.Round(estimate.Value), duration, true) : new(null, duration);
    }
}

// One tracker per consumer process. Pipe producers may supply a denominator, never encoded frames.
public sealed class StepProgress(VideoLength length, Action<EncodingProgress> publish)
{
    private static readonly Regex Percent = new(@"(?:^\s*|\[\s*|\b(?:progress|encoding|encoded|completed?)\s*[:=]?\s*)(\d+(?:\.\d+)?)\s*%", RegexOptions.IgnoreCase);
    private static readonly Regex Frames = new(@"\bframes?(?:\s*[:=]\s*|\s+)(?<n>\d+)(?:\s*/\s*(?<total>\d+))?\b|\b(?<n>\d+)(?:\s*/\s*(?<total>\d+))?\s+frames?\b", RegexOptions.IgnoreCase);
    private static readonly Regex EncodedSummary = new(@"^\s*encoded (\d+) frames,", RegexOptions.IgnoreCase);
    private static readonly Regex VsFrames = new(@"^Frame:\s*\d+/(\d+)\b");
    private static readonly Regex Remain = new(@"\bremain\s+(\d+:\d{2}:\d{2})");
    private static readonly Regex MediaTime = new(@"\btime=(\d+:\d{2}:\d{2}(?:\.\d+)?)");
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private EncodingProgress _sample = new(TotalFrames: length.Frames, EstimatedTotal: length.EstimatedFrames);
    private double? _reportedPercent;
    public bool VtCompleted { get; private set; }
    public long? CompletedFrames { get; private set; }

    public void ReadVt(string line)
    {
        using var json = JsonDocument.Parse(line);
        var e = json.RootElement;
        if (e.GetProperty("schema_version").GetInt32() != 1)
            throw new InvalidDataException(Loc.Format("EncodingRunner.UnsupportedStatus", e.GetProperty("schema_version")));
        var eventName = e.GetProperty("event").GetString();
        switch (eventName)
        {
            case "start":
                if (e.GetProperty("config").TryGetProperty("frames", out var field) &&
                    field.ValueKind == JsonValueKind.Number && field.TryGetInt64(out var rawFrames) && rawFrames > 0)
                    Update(null, null, rawFrames, null);
                break;
            case "attempt":
                _elapsed.Restart();
                _reportedPercent = null;
                _sample = _sample with { Frames = null, Percent = null, Remaining = null };
                publish(_sample);
                break;
            case "completed":
            case "progress":
                VtCompleted = eventName == "completed" && e.GetProperty("output_complete").GetBoolean();
                static double? Number(JsonElement e, string name) => e.GetProperty(name).ValueKind == JsonValueKind.Null ? null : e.GetProperty(name).GetDouble();
                var frames = e.GetProperty("frames").GetInt64();
                if (VtCompleted && frames > 0) CompletedFrames = frames;
                Update(Number(e, "percent"), frames, CompletedFrames, Number(e, "eta_seconds"));
                break;
        }
    }

    public void ReadVs(string line)
    {
        var match = VsFrames.Match(line);
        if (long.TryParse(match.Groups[1].Value, out var total) && total > 0 && total != _sample.TotalFrames)
            Update(null, null, total, null);
    }

    public void ReadText(string line)
    {
        var percent = Percent.Match(line);
        var frames = Frames.Match(line);
        var time = MediaTime.Match(line);
        if (!percent.Success && !frames.Success && !time.Success) return;
        double? value = double.TryParse(percent.Groups[1].Value, CultureInfo.InvariantCulture, out var p) && p is >= 0 and <= 100 ? p : null;
        long? current = long.TryParse(frames.Groups["n"].Value, out var n) ? n : null;
        long? total = long.TryParse(frames.Groups["total"].Value, out var t) && t > 0 ? t : null;
        if (current > 0 && EncodedSummary.IsMatch(line))
            total = CompletedFrames = current;
        // Audio-only FFmpeg status has a media timestamp but no video frame counter.
        if (value is null && current is null && length.Duration is > 0 && ParseClock(time) is { } position)
            value = position / length.Duration * 100;
        Update(value, current, total, ParseClock(Remain.Match(line)));
    }

    private static double? ParseClock(Match match)
    {
        if (!match.Success) return null;
        var parts = match.Groups[1].Value.Split(':');
        return double.Parse(parts[0], CultureInfo.InvariantCulture) * 3600 +
            int.Parse(parts[1], CultureInfo.InvariantCulture) * 60 + double.Parse(parts[2], CultureInfo.InvariantCulture);
    }

    private void Update(double? percent, long? frames, long? total, double? seconds)
    {
        if (percent is not null || frames is not null) _reportedPercent = percent;
        frames ??= _sample.Frames;
        var estimated = total is null && _sample.EstimatedTotal;
        total ??= _sample.TotalFrames;
        if (frames > total)
        {
            // A filter or a bad container estimate can invalidate the source frame count.
            total = null;
            estimated = false;
        }
        var progress = _reportedPercent ?? (total > 0 ? 100.0 * frames / total : null);
        if (progress is { } p) progress = Math.Clamp(p, 0, 100);
        var remaining = EncodingProgress.ToRemaining(seconds) ?? EncodingProgress.EstimateRemaining(progress, _elapsed.Elapsed);
        _sample = new(progress, frames, total, remaining, estimated);
        publish(_sample);
    }
}
